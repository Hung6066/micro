@description('Stable prefix for Azure resource names (lowercase alphanumeric).')
param prefix string = 'hishop'

@description('Azure region; default Southeast Asia for VN proximity.')
param location string = 'southeastasia'

@secure()
@description('PostgreSQL flexible server administrator password.')
param postgresAdminPassword string

param postgresAdminUser string = 'hishop_admin'

@description('Optional client IPv4 address allowed to reach PostgreSQL (empty disables public rule).')
param postgresClientIp string = ''

param tags object = {
  environment: 'azure-staging'
  phase: '0'
  workload: 'identity-platform'
}

var sanitizedPrefix = toLower(replace(prefix, '-', ''))
var postgresServerName = '${sanitizedPrefix}pg'
var redisName = '${sanitizedPrefix}-redis'
var keyVaultName = take('${sanitizedPrefix}-kv-${uniqueString(resourceGroup().id)}', 24)
var acrName = take('${sanitizedPrefix}acr', 50)
var storageName = take('${sanitizedPrefix}bk${uniqueString(resourceGroup().id)}', 24)
var logAnalyticsName = '${sanitizedPrefix}-logs'
var appInsightsName = '${sanitizedPrefix}-ai'

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: '${prefix}-vnet'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: ['10.40.0.0/16']
    }
    subnets: [
      {
        name: 'identity'
        properties: {
          addressPrefix: '10.40.1.0/24'
          delegations: []
        }
      }
      {
        name: 'data'
        properties: {
          addressPrefix: '10.40.2.0/24'
          delegations: []
        }
      }
    ]
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: postgresServerName
  location: location
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminUser
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

resource postgresFirewallAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource postgresFirewallClient 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = if (!empty(postgresClientIp)) {
  parent: postgres
  name: 'AllowClientIp'
  properties: {
    startIpAddress: postgresClientIp
    endIpAddress: postgresClientIp
  }
}

resource postgresDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = {
  parent: postgres
  name: 'identitydb'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource redis 'Microsoft.Cache/redis@2023-08-01' = {
  name: redisName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableRbacAuthorization: true
    publicNetworkAccess: 'Enabled'
    softDeleteRetentionInDays: 7
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
    policies: {
      quarantinePolicy: {
        status: 'disabled'
      }
      retentionPolicy: {
        status: 'enabled'
        days: 14
      }
    }
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

resource backupContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storage.name}/default/identity-backups'
  properties: {
    publicAccess: 'None'
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
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
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

output resourceGroupName string = resourceGroup().name
output vnetId string = vnet.id
output postgresFqdn string = postgres.properties.fullyQualifiedDomainName
output postgresDatabase string = postgresDb.name
output postgresAdminUser string = postgresAdminUser
output redisHostName string = redis.properties.hostName
output redisSslPort int = redis.properties.sslPort
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output acrLoginServer string = acr.properties.loginServer
output acrName string = acr.name
output backupStorageAccount string = storage.name
output logAnalyticsWorkspaceId string = logAnalytics.id
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
