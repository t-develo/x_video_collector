# Skill: azure-serverless-deploy

Azure サーバーレス構成のデプロイ自動化を標準化するスキル。

## アーキテクチャ概要

```
[Azure Static Web Apps]
├── フロントエンド（HTML/CSS/JS）
├── 組み込み Entra ID 認証
└── API バックエンド → [Azure Functions (Linked)]
                           ├── [Azure SQL Database (Free Tier)]
                           └── [Azure Blob Storage]
```

## リソース構成

| リソース | SKU/Tier | 目的 |
|---------|----------|------|
| Azure Static Web Apps | Free | フロントエンド + 認証 |
| Azure Functions | Windows Consumption | API バックエンド |
| Azure SQL Database | Free Tier | データ永続化 |
| Azure Blob Storage | Standard LRS | 動画・サムネイル保存 |
| Application Insights | Free 枠 | 監視・ログ |

## Bicep テンプレート構成

```
infra/
├── main.bicep             # メインテンプレート（モジュール呼び出し）
├── parameters.json        # パラメータファイル
└── modules/
    ├── static-web-app.bicep
    ├── function-app.bicep
    ├── sql-database.bicep
    ├── storage-account.bicep
    └── app-insights.bicep
```

### main.bicep パターン

```bicep
targetScope = 'resourceGroup'

@description('環境名（dev, prod）')
param environmentName string = 'dev'

@description('Azure リージョン')
param location string = resourceGroup().location

@description('SQL Server 管理者パスワード')
@secure()
param sqlAdminPassword string

var prefix = 'xvc-${environmentName}'

module storage 'modules/storage-account.bicep' = {
  name: 'storage'
  params: {
    name: '${replace(prefix, '-', '')}storage'
    location: location
  }
}

module sql 'modules/sql-database.bicep' = {
  name: 'sql'
  params: {
    serverName: '${prefix}-sql'
    databaseName: 'xvideocollector'
    location: location
    adminPassword: sqlAdminPassword
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'appinsights'
  params: {
    name: '${prefix}-insights'
    location: location
  }
}

module functionApp 'modules/function-app.bicep' = {
  name: 'function-app'
  params: {
    name: '${prefix}-func'
    location: location
    storageConnectionString: storage.outputs.connectionString
    sqlConnectionString: sql.outputs.connectionString
    appInsightsConnectionString: appInsights.outputs.connectionString
  }
}

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'static-web-app'
  params: {
    name: '${prefix}-swa'
    location: location
    functionAppResourceId: functionApp.outputs.id
  }
}
```

### Function App モジュール

```bicep
// modules/function-app.bicep
param name string
param location string
param storageConnectionString string
param sqlConnectionString string
param appInsightsConnectionString string

resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${name}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      appSettings: [
        { name: 'AzureWebJobsStorage', value: storageConnectionString }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: sqlConnectionString
          type: 'SQLAzure'
        }
      ]
    }
  }
}

output id string = functionApp.id
output defaultHostName string = functionApp.properties.defaultHostName
```

### SQL Database モジュール（Free Tier）

```bicep
// modules/sql-database.bicep
param serverName string
param databaseName string
param location string

@secure()
param adminPassword string

param adminLogin string = 'xvcadmin'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '12.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Free'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 33554432 // 32 MB
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

// Azure サービスからの接続を許可
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};Persist Security Info=False;User ID=${adminLogin};Password=${adminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
```

## GitHub Actions CI/CD

### ワークフロー構成

```
.github/workflows/
├── ci.yml                 # PR 時の CI（ビルド + テスト）
└── deploy.yml             # main マージ時のデプロイ
```

### CI ワークフロー

```yaml
# .github/workflows/ci.yml
name: CI

on:
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal

  js-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install dependencies
        run: npm ci

      - name: Run JS tests
        run: npx vitest run
```

### デプロイワークフロー

```yaml
# .github/workflows/deploy.yml
name: Deploy

on:
  push:
    branches: [main]

permissions:
  id-token: write
  contents: read

jobs:
  deploy-infra:
    runs-on: ubuntu-latest
    outputs:
      staticWebAppName: ${{ steps.deploy.outputs.staticWebAppName }}
      functionAppName: ${{ steps.deploy.outputs.functionAppName }}
    steps:
      - uses: actions/checkout@v4

      - name: Azure Login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Deploy Bicep
        id: deploy
        uses: azure/arm-deploy@v2
        with:
          resourceGroupName: ${{ secrets.AZURE_RESOURCE_GROUP }}
          template: infra/main.bicep
          parameters: infra/parameters.json sqlAdminPassword=${{ secrets.SQL_ADMIN_PASSWORD }}

  deploy-functions:
    runs-on: ubuntu-latest
    needs: deploy-infra
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish Functions
        run: dotnet publish src/api/XVideoCollector.Functions -c Release -o ./publish

      - name: Azure Login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Deploy to Azure Functions
        uses: Azure/functions-action@v1
        with:
          app-name: ${{ needs.deploy-infra.outputs.functionAppName }}
          package: ./publish

  deploy-frontend:
    runs-on: ubuntu-latest
    needs: deploy-infra
    steps:
      - uses: actions/checkout@v4

      - name: Deploy to Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.SWA_DEPLOYMENT_TOKEN }}
          action: upload
          app_location: src/frontend
          skip_api_build: true
```

## ローカル開発

### 必要ツール

- Azure Functions Core Tools v4
- Azure Static Web Apps CLI（`@azure/static-web-apps-cli`）
- .NET 10 SDK

### ローカル起動手順

```bash
# 1. Functions をローカル起動
cd src/api/XVideoCollector.Functions
func start

# 2. SWA CLI で統合起動（別ターミナル）
cd src/frontend
swa start . --api-location http://localhost:7071
```

### local.settings.json テンプレート

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "BlobStorage__ConnectionString": "UseDevelopmentStorage=true",
    "BlobStorage__ContainerName": "videos",
    "YtDlp__ExecutablePath": "yt-dlp",
    "YtDlp__FfmpegPath": "ffmpeg",
    "YtDlp__TimeoutSeconds": "300",
    "YtDlp__MaxFileSizeMB": "500"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=XVideoCollector;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

## 注意事項

- `local.settings.json` は `.gitignore` に含め、リポジトリにコミットしない
- Bicep パラメータファイルにシークレットを含めない（`@secure()` パラメータで CI/CD から注入）
- Free Tier の制限を意識する（SQL: 32MB、Functions: 月100万回実行）
