// Main bicep template for the expense management system
targetScope = 'resourceGroup'

param location string = resourceGroup().location
param deployGenAI bool = false
param adminLogin string
param adminObjectId string

// Generate unique names using resource group ID
var uniqueSuffix = uniqueString(resourceGroup().id)
var appServiceName = 'app-expensemgmt-${uniqueSuffix}'
var appServicePlanName = 'plan-expensemgmt-${uniqueSuffix}'
var sqlServerName = 'sql-expensemgmt-${uniqueSuffix}'
var managedIdentityName = 'mid-expensemgmt-${uniqueSuffix}'
var openAIAccountName = 'openai-expensemgmt-${uniqueSuffix}'
var searchServiceName = 'srch-expensemgmt-${uniqueSuffix}'

// Deploy App Service with Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    appServiceName: appServiceName
    appServicePlanName: appServicePlanName
    managedIdentityName: managedIdentityName
  }
}

// Deploy Azure SQL Database
module azureSQL 'azure-sql.bicep' = {
  name: 'azureSQLDeployment'
  params: {
    location: location
    sqlServerName: sqlServerName
    databaseName: 'Northwind'
    adminLogin: adminLogin
    adminObjectId: adminObjectId
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Conditionally deploy Gen AI resources
module genAI 'genai.bicep' = if (deployGenAI) {
  name: 'genAIDeployment'
  params: {
    location: location
    openAIAccountName: openAIAccountName
    searchServiceName: searchServiceName
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceHostName string = appService.outputs.appServiceHostName
output appServiceUrl string = 'https://${appService.outputs.appServiceHostName}'
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityName string = appService.outputs.managedIdentityName
output sqlServerName string = azureSQL.outputs.sqlServerName
output sqlServerFqdn string = azureSQL.outputs.sqlServerFqdn
output databaseName string = azureSQL.outputs.databaseName
output connectionString string = azureSQL.outputs.connectionString

// Gen AI outputs (only when deployed)
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''
output openAIName string = deployGenAI ? genAI.outputs.openAIName : ''
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint : ''
output searchServiceName string = deployGenAI ? genAI.outputs.searchServiceName : ''
