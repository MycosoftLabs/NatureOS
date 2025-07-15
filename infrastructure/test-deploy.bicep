@description('Environment name')
param environment string = 'prod'

@description('Location for resources')
param location string = resourceGroup().location

@description('Resource prefix')
param resourcePrefix string = 'natureos'

// Variables
var uniqueSuffix = uniqueString(resourceGroup().id)
var namePrefix = '${resourcePrefix}${environment}'

// Storage Account for testing
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: '${namePrefix}st${uniqueSuffix}'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

output storageAccountName string = storageAccount.name 