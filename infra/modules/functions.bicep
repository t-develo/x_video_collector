// Azure Functions App (Consumption Plan, Windows, Isolated Worker)

param location string
param appName string
param storageConnectionString string
param sqlConnectionString string
param appInsightsConnectionString string
param tags object = {}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false // Windows
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${appName}-func'
  location: location
  tags: tags
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: storageConnectionString
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower('${appName}-func')
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ConnectionStrings__SqlDb'
          value: sqlConnectionString
        }
        {
          name: 'BlobStorage__ConnectionString'
          value: storageConnectionString
        }
        {
          name: 'BlobStorage__VideoContainerName'
          value: 'videos'
        }
        {
          name: 'BlobStorage__ThumbnailContainerName'
          value: 'thumbnails'
        }
        {
          name: 'YtDlp__ExecutablePath'
          value: 'D:\\home\\site\\wwwroot\\yt-dlp.exe'
        }
        {
          name: 'YtDlp__FfmpegPath'
          value: 'D:\\home\\site\\wwwroot\\ffmpeg.exe'
        }
        {
          name: 'YtDlp__TimeoutSeconds'
          value: '300'
        }
        {
          name: 'YtDlp__MaxFileSizeMB'
          value: '500'
        }
      ]
    }
  }
}

output functionAppName string = functionApp.name
output functionAppHostname string = functionApp.properties.defaultHostName
