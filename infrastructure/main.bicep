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

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${namePrefix}-asp-${uniqueSuffix}'
  location: location
  tags: tags
  sku: {
    name: 'B1'
    tier: 'Basic'
    size: 'B1'
    family: 'B'
    capacity: 1
  }
  properties: {
    perSiteScaling: false
    elasticScaleEnabled: false
    maximumElasticWorkerCount: 1
    isSpot: false
    reserved: false
    isXenon: false
    hyperV: false
    targetWorkerCount: 0
    targetWorkerSizeId: 0
    zoneRedundant: false
  }
}

// Core API Web App
resource coreApiWebApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${namePrefix}-coreapi-${uniqueSuffix}'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      scmType: 'None'
      use32BitWorkerProcess: false
      alwaysOn: true
      managedPipelineMode: 'Integrated'
      virtualApplications: [
        {
          virtualPath: '/'
          physicalPath: 'site\\wwwroot'
          preloadEnabled: true
        }
      ]
      loadBalancing: 'LeastRequests'
      experiments: {
        rampUpRules: []
      }
      autoHealEnabled: false
      vnetRouteAllEnabled: false
      vnetPrivatePortsCount: 0
      localMySqlEnabled: false
      ipSecurityRestrictions: [
        {
          ipAddress: 'Any'
          action: 'Allow'
          priority: 2147483647
          name: 'Allow all'
          description: 'Allow all access'
        }
      ]
      scmIpSecurityRestrictions: [
        {
          ipAddress: 'Any'
          action: 'Allow'
          priority: 2147483647
          name: 'Allow all'
          description: 'Allow all access'
        }
      ]
      scmIpSecurityRestrictionsUseMain: false
      http20Enabled: false
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'FtpsOnly'
      preWarmedInstanceCount: 0
      functionAppScaleLimit: 0
      functionsRuntimeScaleMonitoringEnabled: false
      minimumElasticInstanceCount: 0
      azureStorageAccounts: {}
      appSettings: [
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'Recommended'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'ConnectionStrings__CosmosDb'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/CosmosDbConnectionString/)'
        }
        {
          name: 'ConnectionStrings__ServiceBus'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/ServiceBusConnectionString/)'
        }
        {
          name: 'ConnectionStrings__IoTHub'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/IoTHubConnectionString/)'
        }
        {
          name: 'ConnectionStrings__EventGrid'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/EventGridConnectionString/)'
        }
        {
          name: 'ConnectionStrings__Storage'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/StorageConnectionString/)'
        }
        {
          name: 'AzureAd__TenantId'
          value: subscription().tenantId
        }
        {
          name: 'AzureAd__ClientId'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/AzureAdClientId/)'
        }
        {
          name: 'AzureAd__ClientSecret'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/AzureAdClientSecret/)'
        }
        {
          name: 'AzureAd__Authority'
          value: '${az.environment().authentication.loginEndpoint}${subscription().tenantId}'
        }
        {
          name: 'AzureAd__Audience'
          value: 'api://natureos-core-api'
        }
        {
          name: 'ConnectionStrings__Redis'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/RedisConnectionString/)'
        }
      ]
      connectionStrings: []
    }
    scmSiteAlsoStopped: false
    clientAffinityEnabled: false
    clientCertEnabled: false
    clientCertMode: 'Required'
    hostNamesDisabled: false
    containerSize: 0
    dailyMemoryTimeQuota: 0
    redundancyMode: 'None'
    storageAccountRequired: false
    keyVaultReferenceIdentity: 'SystemAssigned'
  }
}

// Assign Key Vault access to Web App and Function App
resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: coreApiWebApp.identity.principalId
        permissions: {
          secrets: ['Get']
          certificates: ['Get']
        }
      }
      {
        tenantId: subscription().tenantId
        objectId: functionApp.identity.principalId
        permissions: {
          secrets: ['Get']
          certificates: ['Get']
        }
      }
    ]
  }
}

// Store secrets in Key Vault
resource cosmosDbConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'CosmosDbConnectionString'
  properties: {
    value: cosmosDbAccount.listConnectionStrings().connectionStrings[0].connectionString
  }
}

resource serviceBusConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ServiceBusConnectionString'
  properties: {
    value: serviceBusNamespace.listKeys().value[0].connectionString
  }
}

resource iotHubConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'IoTHubConnectionString'
  properties: {
    value: iotHub.listKeys().value[0].connectionString
  }
}

resource eventGridConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'EventGridConnectionString'
  properties: {
    value: eventGridTopic.listKeys().key1
  }
}

resource storageConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'StorageConnectionString'
  properties: {
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
  }
}

// Function App for IoT ingestion
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${namePrefix}-func-${uniqueSuffix}'
  location: location
  tags: tags
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      scmType: 'None'
      use32BitWorkerProcess: false
      alwaysOn: true
      managedPipelineMode: 'Integrated'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower('${namePrefix}-func-${uniqueSuffix}')
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
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'CosmosDbConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/CosmosDbConnectionString/)'
        }
        {
          name: 'ServiceBusConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/ServiceBusConnectionString/)'
        }
        {
          name: 'IoTHubConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/IoTHubConnectionString/)'
        }
        {
          name: 'EventGridConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/EventGridConnectionString/)'
        }
      ]
    }
    keyVaultReferenceIdentity: 'SystemAssigned'
  }
}

// Metric Alerts for monitoring
resource cosmosDbRuAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${namePrefix}-cosmosdb-ru-alert'
  location: 'global'
  tags: tags
  properties: {
    description: 'Alert when Cosmos DB RU consumption is high'
    severity: 2
    enabled: true
    scopes: [
      cosmosDbAccount.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HighRUConsumption'
          metricName: 'TotalRequestUnits'
          operator: 'GreaterThan'
          threshold: 1000
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
  }
}

resource webAppCpuAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${namePrefix}-webapp-cpu-alert'
  location: 'global'
  tags: tags
  properties: {
    description: 'Alert when Web App CPU usage is high'
    severity: 2
    enabled: true
    scopes: [
      coreApiWebApp.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HighCPUUsage'
          metricName: 'CpuPercentage'
          operator: 'GreaterThan'
          threshold: 80
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
  }
}

resource webAppErrorAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${namePrefix}-webapp-error-alert'
  location: 'global'
  tags: tags
  properties: {
    description: 'Alert when Web App has high error rate'
    severity: 1
    enabled: true
    scopes: [
      coreApiWebApp.id
    ]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HighErrorRate'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
  }
}

// Redis Cache for intelligent caching
resource redisCache 'Microsoft.Cache/redis@2023-08-01' = {
  name: '${namePrefix}-redis-${uniqueSuffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'Standard'
      family: 'C'
      capacity: 1
    }
    redisConfiguration: {
      'maxclients': '256'
      'maxmemory-reserved': '50'
      'maxfragmentationmemory-reserved': '50'
      'maxmemory-delta': '50'
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    redisVersion: '6'
  }
}

// Store Redis connection string in Key Vault
resource redisConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'RedisConnectionString'
  properties: {
    value: '${redisCache.properties.hostName}:${redisCache.properties.sslPort},password=${redisCache.listKeys().primaryKey},ssl=True,abortConnect=False'
  }
  dependsOn: [
    keyVaultAccessPolicy
  ]
}

// Outputs (non-sensitive only)
output cosmosDbAccountName string = cosmosDbAccount.name
output cosmosDbEndpoint string = cosmosDbAccount.properties.documentEndpoint
output iotHubName string = iotHub.name
output eventGridTopicEndpoint string = eventGridTopic.properties.endpoint
output storageAccountName string = storageAccount.name
output applicationInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey
output applicationInsightsConnectionString string = applicationInsights.properties.ConnectionString
output coreApiWebAppName string = coreApiWebApp.name
output coreApiWebAppUrl string = 'https://${coreApiWebApp.properties.defaultHostName}'
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output containerAppsEnvironmentId string = containerAppsEnvironment.id
output apiManagementGatewayUrl string = 'https://${apiManagement.properties.gatewayUrl}'
output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
output redisCacheName string = redisCache.name
output redisCacheHostName string = redisCache.properties.hostName
output resourceGroupName string = resourceGroup().name