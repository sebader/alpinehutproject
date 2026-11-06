targetScope = 'resourceGroup'

param healthModelId string
param principalId string

resource database 'Microsoft.Sql/servers/databases@2023-08-01' existing = {
  name: 'alpinehutsserver/alpinehutsdb'
}

var readerRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'acdd72a7-3385-48ef-bd42-f606fba81ae7'
)

resource databaseReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: database
  name: guid(database.id, healthModelId, readerRoleId)
  properties: {
    roleDefinitionId: readerRoleId
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
