// Application Insights + Log Analytics Workspace + Alert Rules

param location string
param appName string
param tags object = {}

@description('アラート通知先メールアドレス（省略可）')
param alertEmailAddress string = ''

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${appName}-law'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-ai'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    RetentionInDays: 30
  }
}

// アラート通知先アクション グループ（メールアドレスが指定された場合のみ作成）
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmailAddress)) {
  name: '${appName}-ag'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'xvc-alert'
    enabled: true
    emailReceivers: [
      {
        name: 'Primary'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ]
  }
}

// アラートルール: ダウンロード失敗イベントが 5 分間で 3 件以上
resource downloadFailureAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${appName}-alert-download-failure'
  location: location
  tags: tags
  properties: {
    displayName: '[${appName}] Video download failure rate high'
    description: '5分間でダウンロード失敗イベントが3件以上発生した場合にアラートを発火する'
    enabled: true
    severity: 2
    scopes: [appInsights.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'customEvents | where name == "VideoDownloadFailure" | summarize FailureCount = count()'
          timeAggregation: 'Count'
          operator: 'GreaterThanOrEqual'
          threshold: 3
          failingPeriods: {
            minFailingPeriodsToAlert: 1
            numberOfEvaluationPeriods: 1
          }
        }
      ]
    }
    actions: empty(alertEmailAddress) ? {} : {
      actionGroups: [actionGroup.id]
    }
  }
}

// アラートルール: サーバーエラー率が 5% 以上（1 分間）
resource serverErrorAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${appName}-alert-server-errors'
  location: location
  tags: tags
  properties: {
    displayName: '[${appName}] Server error rate high'
    description: '1分間のサーバーエラー（5xx）率が5%を超えた場合にアラートを発火する'
    enabled: true
    severity: 1
    scopes: [appInsights.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: '''
requests
| summarize
    TotalRequests = count(),
    ServerErrors = countif(resultCode startswith "5")
| where TotalRequests > 0
| extend ErrorRate = todouble(ServerErrors) / todouble(TotalRequests) * 100
| where ErrorRate >= 5
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            minFailingPeriodsToAlert: 1
            numberOfEvaluationPeriods: 1
          }
        }
      ]
    }
    actions: empty(alertEmailAddress) ? {} : {
      actionGroups: [actionGroup.id]
    }
  }
}

output connectionString string = appInsights.properties.ConnectionString
output appInsightsId string = appInsights.id
output appInsightsName string = appInsights.name
