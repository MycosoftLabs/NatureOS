@description('Environment name (e.g., dev, staging, prod)')
param environment string = 'dev'

@description('Primary Azure region for deployment')
param location string = resourceGroup().location

@description('Prefix for all resource names')
param resourcePrefix string = 'natureos'

@description('Tags to apply to all resources')
param tags object = {
  project: 'NatureOS'
  environment: environment
  deployedBy: 'Bicep'
}

// Variables
var uniqueSuffix = uniqueString(resourceGroup().id)
var namePrefix = '${resourcePrefix}-${environment}'

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${namePrefix}-kv-${uniqueSuffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: []
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: false
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
  }
}

// Cosmos DB Account (MINDEX)
resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: '${namePrefix}-cosmos-${uniqueSuffix}'
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableMultipleWriteLocations: false
    enableFreeTier: environment == 'dev'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
      {
        name: 'EnableGremlin'
      }
    ]
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 240
        backupRetentionIntervalInHours: 8
        backupStorageRedundancy: 'Local'
      }
    }
  }
}

// Cosmos DB Database
resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-11-15' = {
  parent: cosmosDbAccount
  name: 'mindex'
  properties: {
    resource: {
      id: 'mindex'
    }
  }
}

// Cosmos DB Containers
resource fungaContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDb
  name: 'funga'
  properties: {
    resource: {
      id: 'funga'
      partitionKey: {
        paths: ['/kingdom_domain']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/signal_vector/*'
          }
        ]
      }
      defaultTtl: -1
    }
  }
}

resource eventsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  parent: cosmosDb
  name: 'events'
  properties: {
    resource: {
      id: 'events'
      partitionKey: {
        paths: ['/source_device']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
      defaultTtl: 7776000 // 90 days
    }
  }
}

// IoT Hub
resource iotHub 'Microsoft.Devices/IotHubs@2023-06-30' = {
  name: '${namePrefix}-iothub-${uniqueSuffix}'
  location: location
  tags: tags
  sku: {
    name: 'S1'
    capacity: 1
  }
  properties: {
    eventHubEndpoints: {
      events: {
        retentionTimeInDays: 1
        partitionCount: 2
      }
    }
    routing: {
      endpoints: {
        eventHubs: []
        serviceBusQueues: []
        serviceBusTopics: []
        storageContainers: []
      }
      routes: []
      fallbackRoute: {
        name: '$fallback'
        source: 'DeviceMessages'
        condition: 'true'
        endpointNames: ['events']
        isEnabled: true
      }
    }
    messagingEndpoints: {
      fileNotifications: {
        lockDurationAsIso8601: 'PT1M'
        ttlAsIso8601: 'PT1H'
        maxDeliveryCount: 10
      }
    }
    enableFileUploadNotifications: false
    cloudToDevice: {
      maxDeliveryCount: 10
      defaultTtlAsIso8601: 'PT1H'
      feedback: {
        lockDurationAsIso8601: 'PT1M'
        ttlAsIso8601: 'PT1H'
        maxDeliveryCount: 10
      }
    }
  }
}

// Event Grid Topic
resource eventGridTopic 'Microsoft.EventGrid/topics@2023-12-15-preview' = {
  name: '${namePrefix}-eventgrid-${uniqueSuffix}'
  location: location
  tags: tags
  properties: {
    inputSchema: 'EventGridSchema'
    publicNetworkAccess: 'Enabled'
  }
}

// Service Bus Namespace
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: '${namePrefix}-servicebus-${uniqueSuffix}'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

// Service Bus Queue for Mycorrhizae Protocol
resource mycorrhizaeQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'mycorrhizae-events'
  properties: {
    lockDuration: 'PT5M'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    requiresSession: false
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    maxDeliveryCount: 10
    enablePartitioning: false
    enableExpress: false
  }
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${resourcePrefix}${environment}st${uniqueSuffix}'
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    networkAcls: {
      bypass: 'AzureServices'
      virtualNetworkRules: []
      ipRules: []
      defaultAction: 'Allow'
    }
    supportsHttpsTrafficOnly: true
    encryption: {
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-appinsights-${uniqueSuffix}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Redfield'
    Request_Source: 'IbizaAIExtension'
    RetentionInDays: 90
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${namePrefix}-logs-${uniqueSuffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 90
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: -1
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${namePrefix}-containerenv-${uniqueSuffix}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

// API Management
resource apiManagement 'Microsoft.ApiManagement/service@2023-05-01-preview' = {
  name: '${namePrefix}-apim-${uniqueSuffix}'
  location: location
  tags: tags
  sku: {
    name: 'Developer'
    capacity: 1
  }
  properties: {
    publisherName: 'Mycosoft Labs'
    publisherEmail: 'admin@mycosoft.com'
    notificationSenderEmail: 'apimgmt-noreply@mail.windowsazure.com'
    hostnameConfigurations: [
      {
        type: 'Proxy'
        hostName: '${namePrefix}-apim-${uniqueSuffix}.azure-api.net'
        negotiateClientCertificate: false
        defaultSslBinding: true
        certificateSource: 'BuiltIn'
      }
    ]
    customProperties: {
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls10': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls11': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Ssl30': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls10': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls11': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Ssl30': 'False'
    }
    virtualNetworkType: 'None'
    disableGateway: false
    natGatewayState: 'Unsupported'
  }
}

// Outputs
output cosmosDbAccountName string = cosmosDbAccount.name
output cosmosDbEndpoint string = cosmosDbAccount.properties.documentEndpoint
output cosmosDbConnectionString string = cosmosDbAccount.listConnectionStrings().connectionStrings[0].connectionString
output iotHubName string = iotHub.name
output iotHubConnectionString string = iotHub.listKeys().value[0].connectionString
output eventGridTopicEndpoint string = eventGridTopic.properties.endpoint
output serviceBusConnectionString string = serviceBusNamespace.listKeys().value[0].connectionString
output storageAccountName string = storageAccount.name
output storageConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
output applicationInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey
output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
output containerAppsEnvironmentId string = containerAppsEnvironment.id
output apiManagementGatewayUrl string = 'https://${apiManagement.properties.gatewayUrl}'
output keyVaultName string = keyVault.name
output resourceGroupName string = resourceGroup().name 