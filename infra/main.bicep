// BPO Platform - Azure Infrastructure as Code (Bicep)
// Deploy with: az deployment group create --resource-group <rg> --template-file main.bicep --parameters @main.parameters.json

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Unique suffix for globally unique resource names')
param uniqueSuffix string = uniqueString(resourceGroup().id)

// ── Derived names ─────────────────────────────────────────────────────────────
var prefix = 'bpo-${environment}'
var storageAccountName = 'bpo${environment}${uniqueSuffix}'
var sqlServerName = '${prefix}-sql-${uniqueSuffix}'
var appServicePlanName = '${prefix}-plan'
var apiAppName = '${prefix}-api-${uniqueSuffix}'
var functionAppName = '${prefix}-fn-${uniqueSuffix}'
var keyVaultName = '${prefix}-kv-${uniqueSuffix}'
var openAiName = '${prefix}-oai-${uniqueSuffix}'
var appInsightsName = '${prefix}-ai'
var logWorkspaceName = '${prefix}-logs'

// ── Log Analytics Workspace ───────────────────────────────────────────────────
resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logWorkspaceName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── Application Insights ──────────────────────────────────────────────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logWorkspace.id
  }
}

// ── Storage Account (Blob) ────────────────────────────────────────────────────
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    networkAcls: { defaultAction: 'Allow' }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource artifactsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'process-artifacts'
  properties: { publicAccess: 'None' }
}

// ── Azure SQL Server & Database ───────────────────────────────────────────────
@secure()
param sqlAdminPassword string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'sqladmin'
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'BPOPlatformDB'
  location: location
  sku: { name: 'S1', tier: 'Standard' }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    requestedBackupStorageRedundancy: 'Local'
  }
}

// ── Azure OpenAI ──────────────────────────────────────────────────────────────
resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: openAiName
  location: location
  kind: 'OpenAI'
  sku: { name: 'S0' }
  properties: {
    customSubDomainName: openAiName
    publicNetworkAccess: 'Enabled'
  }
}

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: 'gpt-4o'
  sku: { name: 'Standard', capacity: 10 }
  properties: {
    model: { format: 'OpenAI', name: 'gpt-4o', version: '2024-08-06' }
  }
}

// ── Key Vault ─────────────────────────────────────────────────────────────────
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// ── App Service Plan ──────────────────────────────────────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: environment == 'prod' ? 'P2v3' : 'B1'
    tier: environment == 'prod' ? 'PremiumV3' : 'Basic'
  }
  kind: 'linux'
  properties: { reserved: true }
}

// ── API App Service ───────────────────────────────────────────────────────────
resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppName
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      appSettings: [
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'AzureStorage__ServiceUri', value: storageAccount.properties.primaryEndpoints.blob }
        { name: 'AzureOpenAI__Endpoint', value: openAi.properties.endpoint }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=BPOPlatformDB;Authentication=Active Directory Managed Identity;'
          type: 'SQLAzure'
        }
      ]
    }
  }
}

// ── Azure Functions ───────────────────────────────────────────────────────────
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'AzureWebJobsStorage__accountName', value: storageAccountName }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'AzureStorage__ServiceUri', value: storageAccount.properties.primaryEndpoints.blob }
        { name: 'AzureOpenAI__Endpoint', value: openAi.properties.endpoint }
      ]
    }
  }
}

// ── RBAC: Managed Identity → Storage (Blob Contributor) ──────────────────────
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource apiStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, apiApp.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: apiApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource fnStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionApp.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output apiUrl string = 'https://${apiApp.properties.defaultHostName}'
output storageAccountName string = storageAccount.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output openAiEndpoint string = openAi.properties.endpoint
output appInsightsConnectionString string = appInsights.properties.ConnectionString
