// Azure SQL Server + Database (Free Tier)

param location string
param appName string
param sqlAdminLogin string
@secure()
param sqlAdminPassword string
@description('Free Tier を使用するか。サブスクリプションに既存の Free Tier DB がある場合は false にし、Basic SKU を使用する')
param useFreeLimit bool = true
param tags object = {}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${appName}-sql'
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Allow Azure services to access SQL Server
resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Free Tier: General Purpose Serverless Gen5, max 1 vCore, 32GB
// useFreeLimit=false の場合は Basic (5 DTU) にフォールバック
resource sqlDatabaseFree 'Microsoft.Sql/servers/databases@2023-08-01-preview' = if (useFreeLimit) {
  parent: sqlServer
  name: '${appName}-db'
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    collation: 'Japanese_CI_AS'
    maxSizeBytes: 34359738368 // 32 GB
    autoPauseDelay: 60
    minCapacity: '0.5'
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

resource sqlDatabaseBasic 'Microsoft.Sql/servers/databases@2023-08-01-preview' = if (!useFreeLimit) {
  parent: sqlServer
  name: '${appName}-db'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'Japanese_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB (Basic tier limit)
  }
}

output serverName string = sqlServer.name
output databaseName string = '${appName}-db'
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${appName}-db;Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
