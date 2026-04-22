targetScope = 'resourceGroup'

param location string = resourceGroup().location
param tags object = {}

@description('Resolved resource names from main.bicep')
param names object

@description('Entra ID principal ID for SQL admin')
param principalId string

@description('Entra ID principal display name for SQL admin')
param principalName string

@description('Container image for the API app')
param apiContainerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('Container image for the UI app')
param uiContainerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('ASP.NET Core environment (Development, Production)')
param aspnetEnvironment string = 'Production'

module logAnalytics 'br/public:avm/res/operational-insights/workspace:0.15.0' = {
  name: 'logAnalytics'
  params: {
    name: names.logAnalytics
    location: location
    tags: tags
    skuName: 'PerGB2018'
    dataRetention: 30
  }
}

module containerRegistry 'br/public:avm/res/container-registry/registry:0.12.0' = {
  name: 'containerRegistry'
  params: {
    name: names.containerRegistry
    location: location
    tags: tags
    acrSku: 'Basic'
    acrAdminUserEnabled: false
    roleAssignments: [
      {
        principalId: managedIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: '7f951dda-4ed3-4680-a7ca-43fe172d538d' // AcrPull — container apps use managed identity to pull
      }
    ]
  }
}

module managedIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.5.0' = {
  name: 'managedIdentity'
  params: {
    name: names.managedIdentity
    location: location
    tags: tags
  }
}

module containerAppsEnv 'br/public:avm/res/app/managed-environment:0.13.1' = {
  name: 'containerAppsEnv'
  params: {
    name: names.containerAppsEnv
    location: location
    tags: tags
    zoneRedundant: false
    publicNetworkAccess: 'Enabled'
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsWorkspaceResourceId: logAnalytics.outputs.resourceId
    }
  }
}

module sqlServer 'br/public:avm/res/sql/server:0.21.1' = {
  name: 'sqlServer'
  params: {
    name: names.sqlServer
    location: location
    tags: tags
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: principalName
      sid: principalId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    databases: [
      {
        name: names.sqlDatabase
        collation: 'SQL_Latin1_General_CP1_CI_AS'
        maxSizeBytes: 2147483648
        availabilityZone: 1
        sku: {
          name: 'GP_S_Gen5'
          tier: 'GeneralPurpose'
          family: 'Gen5'
          capacity: 2
        }
        autoPauseDelay: 60
        minCapacity: '0.5'
      }
    ]
    firewallRules: [
      {
        name: 'AllowAzureServices'
        startIpAddress: '0.0.0.0'
        endIpAddress: '0.0.0.0'
      }
    ]
  }
}

module containerApp 'br/public:avm/res/app/container-app:0.22.0' = {
  name: 'containerApp'
  dependsOn: [containerRegistry]
  params: {
    name: names.containerApp
    location: location
    tags: union(tags, { 'azd-service-name': 'web' })
    environmentResourceId: containerAppsEnv.outputs.resourceId
    managedIdentities: {
      userAssignedResourceIds: [
        managedIdentity.outputs.resourceId
      ]
    }
    activeRevisionsMode: 'Single'
    ingressExternal: true
    ingressTargetPort: 8080
    ingressTransport: 'auto'
    ingressAllowInsecure: false
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: managedIdentity.outputs.resourceId
      }
    ]
    containers: [
      {
        name: 'mcpregistry'
        image: apiContainerImage
        resources: {
          cpu: '0.5'
          memory: '1Gi'
        }
        env: [
          {
            name: 'ASPNETCORE_ENVIRONMENT'
            value: aspnetEnvironment
          }
          {
            name: 'ConnectionStrings__DefaultConnection'
            value: 'Server=tcp:${sqlServer.outputs.fullyQualifiedDomainName},1433;Database=${names.sqlDatabase};Authentication=Active Directory Default;TrustServerCertificate=False;Encrypt=True;User Id=${managedIdentity.outputs.clientId}'
          }
          {
            name: 'AZURE_CLIENT_ID'
            value: managedIdentity.outputs.clientId
          }
        ]
      }
    ]
    scaleSettings: {
      minReplicas: 0
      maxReplicas: 3
      rules: [
        {
          name: 'http-rule'
          http: {
            metadata: {
              concurrentRequests: '100'
            }
          }
        }
      ]
    }
  }
}

module containerAppUi 'br/public:avm/res/app/container-app:0.22.0' = {
  name: 'containerAppUi'
  dependsOn: [containerRegistry]
  params: {
    name: names.containerAppUi
    location: location
    tags: union(tags, { 'azd-service-name': 'ui' })
    environmentResourceId: containerAppsEnv.outputs.resourceId
    managedIdentities: {
      userAssignedResourceIds: [
        managedIdentity.outputs.resourceId
      ]
    }
    activeRevisionsMode: 'Single'
    ingressExternal: true
    ingressTargetPort: 8080
    ingressTransport: 'auto'
    ingressAllowInsecure: false
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: managedIdentity.outputs.resourceId
      }
    ]
    containers: [
      {
        name: 'mcpregistry-ui'
        image: uiContainerImage
        resources: {
          cpu: '0.25'
          memory: '0.5Gi'
        }
        env: [
          {
            name: 'ASPNETCORE_ENVIRONMENT'
            value: aspnetEnvironment
          }
          {
            name: 'ApiBaseUrl'
            value: 'https://${containerApp.outputs.fqdn}'
          }
        ]
      }
    ]
    scaleSettings: {
      minReplicas: 0
      maxReplicas: 2
      rules: [
        {
          name: 'http-rule'
          http: {
            metadata: {
              concurrentRequests: '50'
            }
          }
        }
      ]
    }
  }
}

output containerRegistryEndpoint string = containerRegistry.outputs.loginServer
output containerRegistryName string = containerRegistry.outputs.name
output sqlServerName string = sqlServer.outputs.name
output sqlDatabaseName string = names.sqlDatabase
output apiUrl string = 'https://${containerApp.outputs.fqdn}'
output uiUrl string = 'https://${containerAppUi.outputs.fqdn}'
output containerAppName string = containerApp.outputs.name
output containerAppUiName string = containerAppUi.outputs.name
output managedIdentityName string = managedIdentity.outputs.name
output managedIdentityClientId string = managedIdentity.outputs.clientId
