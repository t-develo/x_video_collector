// Azure Storage Account (Blob Storage + Functions backend)

param location string
param appName string
param tags object = {}

// Storage name: lowercase, alphanumeric, max 24 chars
var storageName = replace(replace(toLower('${appName}stor'), '-', ''), '_', '')

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: length(storageName) > 24 ? substring(storageName, 0, 24) : storageName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
  }
}

resource videosContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccount.name}/default/videos'
  properties: {
    publicAccess: 'None'
  }
}

resource thumbnailsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storageAccount.name}/default/thumbnails'
  properties: {
    publicAccess: 'None'
  }
}

output storageAccountName string = storageAccount.name
output connectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
