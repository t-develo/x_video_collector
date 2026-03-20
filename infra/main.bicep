// X動画コレクター — メインインフラ Bicep テンプレート
// デプロイスコープ: サブスクリプション

targetScope = 'subscription'

@description('アプリケーション名（リソース名のプレフィックス）')
param appName string = 'xvc'

@description('デプロイ環境')
@allowed(['dev', 'stg', 'prod'])
param environment string = 'prod'

@description('デプロイリージョン')
param location string = 'japaneast'

@description('SQL Server 管理者ユーザー名')
param sqlAdminLogin string = 'xvcadmin'

@description('SQL Server 管理者パスワード')
@secure()
param sqlAdminPassword string

var resourcePrefix = '${appName}-${environment}'
var tags = {
  application: 'x-video-collector'
  environment: environment
  managedBy: 'bicep'
}

// リソースグループ
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${resourcePrefix}'
  location: location
  tags: tags
}

// Application Insights
module appInsights 'modules/appinsights.bicep' = {
  name: 'appinsights'
  scope: rg
  params: {
    location: location
    appName: resourcePrefix
    tags: tags
  }
}

// SQL Database
module sql 'modules/sql.bicep' = {
  name: 'sql'
  scope: rg
  params: {
    location: location
    appName: resourcePrefix
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    tags: tags
  }
}

// Blob Storage
module storage 'modules/storage.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    location: location
    appName: resourcePrefix
    tags: tags
  }
}

// Azure Functions
module functions 'modules/functions.bicep' = {
  name: 'functions'
  scope: rg
  params: {
    location: location
    appName: resourcePrefix
    storageConnectionString: storage.outputs.connectionString
    sqlConnectionString: sql.outputs.connectionString
    appInsightsConnectionString: appInsights.outputs.connectionString
    tags: tags
  }
}

// Static Web Apps
module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp'
  scope: rg
  params: {
    location: location
    appName: resourcePrefix
    functionsAppHostname: functions.outputs.functionAppHostname
    tags: tags
  }
  dependsOn: [functions]
}

// Outputs
output resourceGroupName string = rg.name
output functionsAppName string = functions.outputs.functionAppName
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
output staticWebAppHostname string = staticWebApp.outputs.staticWebAppHostname
output staticWebAppApiKey string = staticWebApp.outputs.apiKey
