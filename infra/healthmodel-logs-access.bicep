targetScope = 'resourceGroup'

param healthModelId string
param principalId string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: 'silenced-eu-loganalytics'
}

var monitoringReaderRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '43d0d8ad-25c7-4714-9337-8ba259a9fe05'
)

resource workspaceReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: workspace
  name: guid(workspace.id, healthModelId, monitoringReaderRoleId)
  properties: {
    roleDefinitionId: monitoringReaderRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
