targetScope = 'resourceGroup'

@minLength(3)
@maxLength(44)
param healthModelName string = 'alpinehuts-prod-health'

param location string = 'westeurope'

@description('Production managed-API AppRoleName, verified by correlating production browser dependencies with AppRequests before deployment.')
@minLength(5)
param productionApiRole string

var readerRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'acdd72a7-3385-48ef-bd42-f606fba81ae7'
)
resource crawler 'Microsoft.Web/sites@2024-11-01' existing = {
  name: 'alpinehutscrawler-prod'
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: 'sthutscrawlerprod'
}

resource website 'Microsoft.Web/staticSites@2024-11-01' existing = {
  name: 'alpinehuts'
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' existing = {
  scope: resourceGroup('alpinehutsV2-rg')
  name: 'alpinehutsserver/alpinehutsdb'
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  scope: resourceGroup('server-loganalytics-rg')
  name: 'silenced-eu-loganalytics'
}

var insightsId = resourceId('alpinehutsV2-rg', 'Microsoft.Insights/components', 'alpinehutsfunctions')
var actionGroupIds = [
  resourceId('alpinehutsV2-rg', 'Microsoft.Insights/actionGroups', 'Email Me')
]

// Persist the arranged layout so deployments do not stack entities at the origin.
var canvasPositions = {
  root: {
    x: 1250
    y: 0
  }
  'e-u': {
    x: 375
    y: 193
  }
  'c-u-web': {
    x: 0
    y: 386
  }
  'c-u-fresh': {
    x: 250
    y: 386
  }
  'c-u-db': {
    x: 500
    y: 386
  }
  'c-u-api': {
    x: 750
    y: 386
  }
  'e-u-swa': {
    x: 0
    y: 587
  }
  'e-u-sql': {
    x: 500
    y: 587
  }
  'e-u-api-swa': {
    x: 750
    y: 579
  }
  'e-h': {
    x: 1250
    y: 193
  }
  'c-h-source': {
    x: 1000
    y: 386
  }
  'c-h-job': {
    x: 1250
    y: 386
  }
  'c-h-db': {
    x: 1625
    y: 386
  }
  'e-h-func': {
    x: 1125
    y: 579
  }
  'e-h-storage': {
    x: 1375
    y: 579
  }
  'e-h-sql': {
    x: 1625
    y: 587
  }
  'e-a': {
    x: 2375
    y: 193
  }
  'c-a-report': {
    x: 1875
    y: 386
  }
  'c-a-job': {
    x: 2125
    y: 386
  }
  'c-a-hh': {
    x: 2375
    y: 386
  }
  'c-a-db': {
    x: 2625
    y: 386
  }
  'c-a-av': {
    x: 2875
    y: 386
  }
  'e-a-func': {
    x: 2000
    y: 579
  }
  'e-a-storage': {
    x: 2250
    y: 579
  }
  'e-a-sql': {
    x: 2625
    y: 587
  }
}

resource healthModel 'Microsoft.CloudHealth/healthmodels@2026-11-01' = {
  name: healthModelName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

// Configure the model-created root under its existing name, not a second root.
resource rootEntity 'Microsoft.CloudHealth/healthmodels/entities@2026-11-01' = {
  parent: healthModel
  name: healthModelName
  properties: {
    displayName: healthModelName
    impact: 'Standard'
    canvasPosition: canvasPositions.root
  }
}

resource crawlerReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: crawler
  name: guid(crawler.id, healthModel.id, readerRoleId)
  properties: {
    roleDefinitionId: readerRoleId
    principalId: healthModel.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, healthModel.id, readerRoleId)
  properties: {
    roleDefinitionId: readerRoleId
    principalId: healthModel.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource websiteReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: website
  name: guid(website.id, healthModel.id, readerRoleId)
  properties: {
    roleDefinitionId: readerRoleId
    principalId: healthModel.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

module databaseAccess 'healthmodel-sql-access.bicep' = {
  scope: resourceGroup('alpinehutsV2-rg')
  name: '${healthModelName}-sql-access'
  params: {
    healthModelId: healthModel.id
    principalId: healthModel.identity.principalId
  }
}

module workspaceAccess 'healthmodel-logs-access.bicep' = {
  scope: resourceGroup('server-loganalytics-rg')
  name: '${healthModelName}-logs-access'
  params: {
    healthModelId: healthModel.id
    principalId: healthModel.identity.principalId
  }
}

resource authentication 'Microsoft.CloudHealth/healthmodels/authenticationsettings@2026-11-01' = {
  parent: healthModel
  name: 'system-assigned'
  properties: {
    displayName: 'Health model managed identity'
    authenticationKind: 'ManagedIdentity'
    managedIdentityName: 'SystemAssigned'
  }
  dependsOn: [
    crawlerReader
    storageReader
    websiteReader
    databaseAccess
    workspaceAccess
  ]
}

// The API requires an unhealthy rule. Counts and percentages cannot be negative.
var warningOnlyRules = {
  degradedRule: {
    operator: 'GreaterThanOrEqual'
    threshold: 1
  }
  unhealthyRule: {
    operator: 'LessThan'
    threshold: 0
  }
}

var overdueRules = {
  degradedRule: {
    operator: 'LessThan'
    threshold: 1
  }
  unhealthyRule: {
    operator: 'LessThan'
    threshold: 0
  }
}

var missedRules = {
  unhealthyRule: {
    operator: 'LessThan'
    threshold: 1
  }
}

var requestFailureRules = {
  degradedRule: {
    operator: 'GreaterThanOrEqual'
    threshold: 1
  }
  unhealthyRule: {
    operator: 'GreaterThanOrEqual'
    threshold: 5
  }
}

var telemetryScope = 'let ai = "${insightsId}";\nlet crawler = "${crawler.name}";\nlet productionApi = "${productionApiRole}";\nlet productionHosts = dynamic(["alpinehuts.silenced.eu", "white-pond-0976e3603.1.azurestaticapps.net"]);\n'

var appErrorsQuery = '''
union
    (AppTraces
    | where TimeGenerated > ago(lookback) and _ResourceId =~ ai and AppRoleName == crawler
    | where SeverityLevel >= 3
    | project Properties),
    (AppExceptions
    | where TimeGenerated > ago(lookback) and _ResourceId =~ ai and AppRoleName == crawler
    | project Properties)
| where tostring(Properties.CategoryName) in (categories)
| where tostring(Properties.OriginalFormat) !in (
    "Exception in executing stored procedure to update availability reporting",
    "Availability reporting failed after hut updates completed")
| summarize value = count()
'''

var cycleQuery = '''
AppRequests
| where TimeGenerated > ago(lookback + 1d) and _ResourceId =~ ai and AppRoleName == crawler
| where Name == cycle and Success == true
| extend CompletedAt = TimeGenerated + DurationMs * 1ms
| where CompletedAt > ago(lookback) and CompletedAt <= now()
| summarize value = count()
'''

// Bed refresh ends before optional reporting and can outlive an orchestration span.
var bedCompletionQuery = '''
AppTraces
| where TimeGenerated > ago(lookback) and _ResourceId =~ ai and AppRoleName == crawler
| where tostring(Properties.CategoryName) == category
    and tostring(Properties.ActivityName) == activity
| where Message startswith "All " and Message endswith " activities finished"
| summarize value = count()
'''

var sourceQuery = '''
AppTraces
| where TimeGenerated > ago(lookback) and _ResourceId =~ ai and AppRoleName == crawler
| where SeverityLevel >= 3 and tostring(Properties.CategoryName) == category
| where Message startswith "Error fetching hut info"
    or Message startswith "Error deserializing hut info"
    or Message startswith "Could not get availability"
    or Message startswith "Could not parse availability"
    or Message startswith "Could not get hut info"
    or Message startswith "Could not parse hut info"
    or Message startswith "Failed to fetch initial cookies"
    or Message startswith "Failed to extract cookies"
    or Message startswith "Failed to fetch availability from HuettenHoliday"
| summarize value = count()
'''

var staleQuery = '''
let completedProviders = toscalar(AppTraces
    | where TimeGenerated > ago(lookback) and _ResourceId =~ ai and AppRoleName == crawler
    | extend Activity = tostring(Properties.ActivityName), Category = tostring(Properties.CategoryName)
    | where (Activity == "UpdateHutAvailability" and Category == "FetchDataFunctions.UpdateAvailabilityFunctions")
        or (Activity == "HuettenHolidayUpdateAvailabilityActivityTrigger"
            and Category == "FetchDataFunctions.Functions.HuettenHoliday.HuettenHolidayUpdateAvailabilityFromProvider")
    | where Message startswith "All " and Message endswith " activities finished"
    | summarize count_distinct(Activity));
print value = 2 - completedProviders
'''

var logSignalSpecifications = [
  {
    name: 'hut-errors'
    displayName: 'Hut-update application errors'
    query: '${telemetryScope}let lookback = 26h;\nlet categories = dynamic(["FetchDataFunctions.UpdateHutsFunctions"]);\n${appErrorsQuery}'
    timeGrain: 'PT26H'
    refreshInterval: 'PT15M'
    rules: warningOnlyRules
  }
  {
    name: 'hut-checks-overdue'
    displayName: 'Completed hut checks in 26 hours'
    query: '${telemetryScope}let lookback = 26h;\nlet cycle = "orchestration:UpdateHutsOrchestrator";\n${cycleQuery}'
    timeGrain: 'PT50H'
    refreshInterval: 'PT15M'
    rules: overdueRules
  }
  {
    name: 'hut-checks-missed'
    displayName: 'Completed hut checks in 30 hours'
    query: '${telemetryScope}let lookback = 30h;\nlet cycle = "orchestration:UpdateHutsOrchestrator";\n${cycleQuery}'
    timeGrain: 'PT54H'
    refreshInterval: 'PT15M'
    rules: missedRules
  }
  {
    name: 'hut-provider-errors'
    displayName: 'Hut provider fetch and parse errors'
    query: '${telemetryScope}let lookback = 26h;\nlet category = "FetchDataFunctions.UpdateHutsFunctions";\n${sourceQuery}'
    timeGrain: 'PT26H'
    refreshInterval: 'PT15M'
    rules: warningOnlyRules
  }
  {
    name: 'crawler-host-failures'
    displayName: 'Failed crawler invocations'
    query: '${telemetryScope}${'''
AppRequests
| where TimeGenerated > ago(15m) and _ResourceId =~ ai and AppRoleName == crawler
| where not(Name contains "/admin/")
| where Name != "activity:UpdateAvailabilityReporting"
| where Success == false
| where (isempty(Url) and (isempty(ResultCode) or ResultCode == "0"))
    or toint(ResultCode) >= 500 or ResultCode == "429"
| summarize value = count()
'''}'
    timeGrain: 'PT15M'
    refreshInterval: 'PT5M'
    rules: requestFailureRules
  }
  {
    name: 'availability-errors'
    displayName: 'Availability-update application errors'
    query: '${telemetryScope}let lookback = 17h;\nlet categories = dynamic(["FetchDataFunctions.UpdateAvailabilityFunctions", "FetchDataFunctions.Functions.HuettenHoliday.HuettenHolidayUpdateAvailabilityFromProvider"]);\n${appErrorsQuery}'
    timeGrain: 'PT17H'
    refreshInterval: 'PT15M'
    rules: warningOnlyRules
  }
  {
    name: 'av-checks-overdue'
    displayName: 'Completed alpine-club checks in 17 hours'
    query: '${telemetryScope}let lookback = 17h;\nlet category = "FetchDataFunctions.UpdateAvailabilityFunctions";\nlet activity = "UpdateHutAvailability";\n${bedCompletionQuery}'
    timeGrain: 'PT17H'
    refreshInterval: 'PT15M'
    rules: overdueRules
  }
  {
    name: 'av-checks-missed'
    displayName: 'Completed alpine-club checks in 26 hours'
    query: '${telemetryScope}let lookback = 26h;\nlet category = "FetchDataFunctions.UpdateAvailabilityFunctions";\nlet activity = "UpdateHutAvailability";\n${bedCompletionQuery}'
    timeGrain: 'PT26H'
    refreshInterval: 'PT15M'
    rules: missedRules
  }
  {
    name: 'holiday-checks-overdue'
    displayName: 'Completed holiday-provider checks in 17 hours'
    query: '${telemetryScope}let lookback = 17h;\nlet category = "FetchDataFunctions.Functions.HuettenHoliday.HuettenHolidayUpdateAvailabilityFromProvider";\nlet activity = "HuettenHolidayUpdateAvailabilityActivityTrigger";\n${bedCompletionQuery}'
    timeGrain: 'PT17H'
    refreshInterval: 'PT15M'
    rules: overdueRules
  }
  {
    name: 'holiday-checks-missed'
    displayName: 'Completed holiday-provider checks in 26 hours'
    query: '${telemetryScope}let lookback = 26h;\nlet category = "FetchDataFunctions.Functions.HuettenHoliday.HuettenHolidayUpdateAvailabilityFromProvider";\nlet activity = "HuettenHolidayUpdateAvailabilityActivityTrigger";\n${bedCompletionQuery}'
    timeGrain: 'PT26H'
    refreshInterval: 'PT15M'
    rules: missedRules
  }
  {
    name: 'av-provider-errors'
    displayName: 'Alpine-club fetch and parse errors'
    query: '${telemetryScope}let lookback = 17h;\nlet category = "FetchDataFunctions.UpdateAvailabilityFunctions";\n${sourceQuery}'
    timeGrain: 'PT17H'
    refreshInterval: 'PT15M'
    rules: warningOnlyRules
  }
  {
    name: 'holiday-provider-errors'
    displayName: 'Holiday-provider fetch and cookie errors'
    query: '${telemetryScope}let lookback = 17h;\nlet category = "FetchDataFunctions.Functions.HuettenHoliday.HuettenHolidayUpdateAvailabilityFromProvider";\n${sourceQuery}'
    timeGrain: 'PT17H'
    refreshInterval: 'PT15M'
    rules: warningOnlyRules
  }
  {
    name: 'reporting-errors'
    displayName: 'Availability reporting errors'
    query: '${telemetryScope}${'''
AppExceptions
| where TimeGenerated > ago(17h) and _ResourceId =~ ai and AppRoleName == crawler
| where tostring(Properties.CategoryName) == "FetchDataFunctions.UpdateAvailabilityFunctions"
| where tostring(Properties.OriginalFormat) == "Exception in executing stored procedure to update availability reporting"
| summarize value = count()
'''}'
    timeGrain: 'PT17H'
    refreshInterval: 'PT15M'
    rules: {
      unhealthyRule: {
        operator: 'GreaterThanOrEqual'
        threshold: 1
      }
    }
  }
  {
    name: 'browser-errors'
    displayName: 'Production browser errors'
    query: '${telemetryScope}${'''
let productionOperations = AppPageViews
    | where TimeGenerated > ago(1h) and _ResourceId =~ ai
    | where tostring(parse_url(Url).Host) in~ (productionHosts)
    | where isnotempty(OperationId)
    | distinct OperationId;
AppExceptions
| where TimeGenerated > ago(15m) and _ResourceId =~ ai
| where ClientType == "Browser" and OperationId in (productionOperations)
| summarize value = count()
'''}'
    timeGrain: 'PT1H'
    refreshInterval: 'PT5M'
    rules: requestFailureRules
  }
  {
    name: 'swa-host-failures'
    displayName: 'Production managed-host server errors'
    query: '${telemetryScope}${'''
AppRequests
| where TimeGenerated > ago(15m) and _ResourceId =~ ai and AppRoleName == productionApi
| where toint(ResultCode) >= 500
| summarize value = count()
'''}'
    timeGrain: 'PT15M'
    refreshInterval: 'PT5M'
    rules: requestFailureRules
  }
  {
    name: 'public-read-failures'
    displayName: 'Failed public availability reads'
    query: '${telemetryScope}${'''
AppRequests
| where TimeGenerated > ago(15m) and _ResourceId =~ ai and AppRoleName == productionApi
| where Name startswith "GET api/"
| where Name startswith "GET api/huts"
    or Name startswith "GET api/availability/"
    or Name == "GET api/bedcategories"
| where Success == false and ResultCode != "404"
| summarize value = count()
'''}'
    timeGrain: 'PT15M'
    refreshInterval: 'PT5M'
    rules: requestFailureRules
  }
  {
    name: 'providers-overdue'
    displayName: 'Providers without completed checks in 17 hours'
    query: '${telemetryScope}let lookback = 17h;\n${staleQuery}'
    timeGrain: 'PT17H'
    refreshInterval: 'PT15M'
    rules: warningOnlyRules
  }
  {
    name: 'providers-stale'
    displayName: 'Providers without completed checks in 26 hours'
    query: '${telemetryScope}let lookback = 26h;\n${staleQuery}'
    timeGrain: 'PT26H'
    refreshInterval: 'PT15M'
    rules: {
      degradedRule: {
        operator: 'GreaterThanOrEqual'
        threshold: 1
      }
      unhealthyRule: {
        operator: 'GreaterThanOrEqual'
        threshold: 2
      }
    }
  }
]

resource logDefinitions 'Microsoft.CloudHealth/healthmodels/signaldefinitions@2026-11-01' = [
  for signal in logSignalSpecifications: {
    parent: healthModel
    name: signal.name
    properties: {
      signalKind: 'LogAnalyticsQuery'
      displayName: signal.displayName
      queryText: signal.query
      valueColumnName: 'value'
      timeGrain: signal.timeGrain
      refreshInterval: signal.refreshInterval
      dataUnit: 'Count'
      evaluationRules: signal.rules
    }
  }
]

var metricSignalSpecifications = [
  {
    name: 'storage-availability'
    displayName: 'Job-storage availability'
    metricNamespace: 'Microsoft.Storage/storageAccounts'
    metricName: 'Availability'
    rules: {
      degradedRule: {
        operator: 'LessThan'
        threshold: 99
      }
      unhealthyRule: {
        operator: 'LessThan'
        threshold: 95
      }
    }
  }
  {
    name: 'sql-availability'
    displayName: 'Database availability'
    metricNamespace: 'Microsoft.Sql/servers/databases'
    metricName: 'availability'
    rules: {
      degradedRule: {
        operator: 'LessThan'
        threshold: 100
      }
      unhealthyRule: {
        operator: 'LessThan'
        threshold: 99
      }
    }
  }
  {
    name: 'sql-capacity'
    displayName: 'Database DTU consumption'
    metricNamespace: 'Microsoft.Sql/servers/databases'
    metricName: 'dtu_consumption_percent'
    rules: {
      degradedRule: {
        operator: 'GreaterThan'
        threshold: 80
      }
      unhealthyRule: {
        operator: 'GreaterThan'
        threshold: 100
      }
    }
  }
]

resource metricDefinitions 'Microsoft.CloudHealth/healthmodels/signaldefinitions@2026-11-01' = [
  for signal in metricSignalSpecifications: {
    parent: healthModel
    name: signal.name
    properties: {
      signalKind: 'AzureResourceMetric'
      displayName: signal.displayName
      metricNamespace: signal.metricNamespace
      metricName: signal.metricName
      aggregationType: 'Average'
      timeGrain: 'PT5M'
      refreshInterval: 'PT5M'
      dataUnit: 'Percent'
      evaluationRules: signal.rules
    }
  }
]

var flowSpecifications = [
  {
    id: 'E_H'
    name: 'e-h'
    displayName: 'Update huts'
    objective: 99
  }
  {
    id: 'E_A'
    name: 'e-a'
    displayName: 'Update availability'
    objective: 99
  }
  {
    id: 'E_U'
    name: 'e-u'
    displayName: 'View hut availability'
    objective: json('99.9')
  }
]

resource flows 'Microsoft.CloudHealth/healthmodels/entities@2026-11-01' = [
  for flow in flowSpecifications: {
    parent: healthModel
    name: flow.name
    properties: {
      displayName: flow.displayName
      canvasPosition: canvasPositions[flow.name]
      icon: {
        iconName: 'UserFlow'
      }
      healthObjective: flow.objective
      tags: {
        modelSpecId: flow.id
      }
      alerts: {
        degraded: {
          severity: 'Sev3'
          description: '${flow.displayName} is degraded. Inspect the flow dependencies and signals.'
          actionGroupIds: actionGroupIds
        }
        unhealthy: {
          severity: 'Sev2'
          description: '${flow.displayName} is unhealthy. Inspect the flow dependencies and signals.'
          actionGroupIds: actionGroupIds
        }
      }
      signalGroups: {
        dependencies: {
          aggregationType: 'WorstOf'
        }
      }
    }
  }
]

var componentSpecifications = [
  {
    id: 'C_H_JOB'
    name: 'c-h-job'
    parent: 'e-h'
    displayName: 'Run hut checks'
    impact: 'Standard'
    signals: [
      'hut-errors'
      'hut-checks-overdue'
      'hut-checks-missed'
    ]
  }
  {
    id: 'C_H_SOURCE'
    name: 'c-h-source'
    parent: 'e-h'
    displayName: 'Read hut details'
    impact: 'Standard'
    signals: [
      'hut-provider-errors'
    ]
  }
  {
    id: 'C_H_DB'
    name: 'c-h-db'
    parent: 'e-h'
    displayName: 'Store hut details'
    impact: 'Standard'
    signals: []
  }
  {
    id: 'C_A_JOB'
    name: 'c-a-job'
    parent: 'e-a'
    displayName: 'Run availability checks'
    impact: 'Standard'
    signals: [
      'availability-errors'
      'av-checks-overdue'
      'av-checks-missed'
      'holiday-checks-overdue'
      'holiday-checks-missed'
    ]
  }
  {
    id: 'C_A_AV'
    name: 'c-a-av'
    parent: 'e-a'
    displayName: 'Read alpine-club availability'
    impact: 'Standard'
    signals: [
      'av-provider-errors'
    ]
  }
  {
    id: 'C_A_HH'
    name: 'c-a-hh'
    parent: 'e-a'
    displayName: 'Read holiday-provider availability'
    impact: 'Standard'
    signals: [
      'holiday-provider-errors'
    ]
  }
  {
    id: 'C_A_DB'
    name: 'c-a-db'
    parent: 'e-a'
    displayName: 'Store availability'
    impact: 'Standard'
    signals: []
  }
  {
    id: 'C_A_REPORT'
    name: 'c-a-report'
    parent: 'e-a'
    displayName: 'Build reporting totals'
    impact: 'Limited'
    signals: [
      'reporting-errors'
    ]
  }
  {
    id: 'C_U_WEB'
    name: 'c-u-web'
    parent: 'e-u'
    displayName: 'Display website'
    impact: 'Standard'
    signals: [
      'browser-errors'
    ]
  }
  {
    id: 'C_U_API'
    name: 'c-u-api'
    parent: 'e-u'
    displayName: 'Read availability'
    impact: 'Standard'
    signals: [
      'public-read-failures'
    ]
  }
  {
    id: 'C_U_DB'
    name: 'c-u-db'
    parent: 'e-u'
    displayName: 'Read stored data'
    impact: 'Standard'
    signals: []
  }
  {
    id: 'C_U_FRESH'
    name: 'c-u-fresh'
    parent: 'e-u'
    displayName: 'Recent availability checks'
    impact: 'Standard'
    signals: [
      'providers-overdue'
      'providers-stale'
    ]
  }
]

resource components 'Microsoft.CloudHealth/healthmodels/entities@2026-11-01' = [
  for component in componentSpecifications: {
    parent: healthModel
    name: component.name
    properties: {
      displayName: component.displayName
      canvasPosition: canvasPositions[component.name]
      impact: component.impact
      icon: {
        iconName: 'SystemComponent'
      }
      tags: {
        modelSpecId: component.id
      }
      signalGroups: union(
        {
          dependencies: {
            aggregationType: 'WorstOf'
          }
        },
        empty(component.signals)
          ? {}
          : {
              azureLogAnalytics: {
                authenticationSetting: authentication.name
                logAnalyticsWorkspaceResourceId: workspace.id
                signals: map(component.signals, signal => {
                  name: signal
                  signalKind: 'LogAnalyticsQuery'
                  signalDefinitionName: signal
                })
              }
            }
      )
    }
    dependsOn: [
      logDefinitions
    ]
  }
]

var resourceSpecifications = [
  {
    id: 'E_H_FUNC'
    name: 'e-h-func'
    parent: 'c-h-job'
    displayName: 'Functions: ${crawler.name} (hut checks)'
    resourceId: crawler.id
    logSignals: [
      'crawler-host-failures'
    ]
    metricSignals: []
  }
  {
    id: 'E_H_STORAGE'
    name: 'e-h-storage'
    parent: 'c-h-job'
    displayName: 'Storage: ${storage.name} (hut checks)'
    resourceId: storage.id
    logSignals: []
    metricSignals: [
      'storage-availability'
    ]
  }
  {
    id: 'E_H_SQL'
    name: 'e-h-sql'
    parent: 'c-h-db'
    displayName: 'SQL: ${last(split(database.name, '/'))} (hut details)'
    resourceId: database.id
    logSignals: []
    metricSignals: [
      'sql-availability'
      'sql-capacity'
    ]
  }
  {
    id: 'E_A_FUNC'
    name: 'e-a-func'
    parent: 'c-a-job'
    displayName: 'Functions: ${crawler.name} (availability checks)'
    resourceId: crawler.id
    logSignals: [
      'crawler-host-failures'
    ]
    metricSignals: []
  }
  {
    id: 'E_A_STORAGE'
    name: 'e-a-storage'
    parent: 'c-a-job'
    displayName: 'Storage: ${storage.name} (availability checks)'
    resourceId: storage.id
    logSignals: []
    metricSignals: [
      'storage-availability'
    ]
  }
  {
    id: 'E_A_SQL'
    name: 'e-a-sql'
    parent: 'c-a-db'
    displayName: 'SQL: ${last(split(database.name, '/'))} (availability writes)'
    resourceId: database.id
    logSignals: []
    metricSignals: [
      'sql-availability'
      'sql-capacity'
    ]
  }
  {
    id: 'E_U_SWA'
    name: 'e-u-swa'
    parent: 'c-u-web'
    displayName: 'Static Web App: ${website.name} (website)'
    resourceId: website.id
    logSignals: [
      'swa-host-failures'
    ]
    metricSignals: []
  }
  {
    id: 'E_U_API_SWA'
    name: 'e-u-api-swa'
    parent: 'c-u-api'
    displayName: 'Static Web App: ${website.name} (managed API)'
    resourceId: website.id
    logSignals: [
      'swa-host-failures'
    ]
    metricSignals: []
  }
  {
    id: 'E_U_SQL'
    name: 'e-u-sql'
    parent: 'c-u-db'
    displayName: 'SQL: ${last(split(database.name, '/'))} (website reads)'
    resourceId: database.id
    logSignals: []
    metricSignals: [
      'sql-availability'
      'sql-capacity'
    ]
  }
]

resource resourceEntities 'Microsoft.CloudHealth/healthmodels/entities@2026-11-01' = [
  for entity in resourceSpecifications: {
    parent: healthModel
    name: entity.name
    properties: {
      displayName: entity.displayName
      canvasPosition: canvasPositions[entity.name]
      tags: {
        modelSpecId: entity.id
      }
      signalGroups: union(
        {
          azureResource: {
            authenticationSetting: authentication.name
            azureResourceId: entity.resourceId
            resourceHealth: {
              enabled: 'Disabled'
            }
            signals: map(entity.metricSignals, signal => {
              name: signal
              signalKind: 'AzureResourceMetric'
              signalDefinitionName: signal
            })
          }
        },
        empty(entity.logSignals)
          ? {}
          : {
              azureLogAnalytics: {
                authenticationSetting: authentication.name
                logAnalyticsWorkspaceResourceId: workspace.id
                signals: map(entity.logSignals, signal => {
                  name: signal
                  signalKind: 'LogAnalyticsQuery'
                  signalDefinitionName: signal
                })
              }
            }
      )
    }
    dependsOn: [
      logDefinitions
      metricDefinitions
    ]
  }
]

var flowEdges = [
  for flow in flowSpecifications: {
    parent: healthModelName
    child: flow.name
  }
]

var componentEdges = [
  for component in componentSpecifications: {
    parent: component.parent
    child: component.name
  }
]

var resourceEdges = [
  for entity in resourceSpecifications: {
    parent: entity.parent
    child: entity.name
  }
]

var edges = concat(flowEdges, componentEdges, resourceEdges)

resource relationships 'Microsoft.CloudHealth/healthmodels/relationships@2026-11-01' = [
  for edge in edges: {
    parent: healthModel
    name: '${edge.parent}-${edge.child}'
    properties: {
      parentEntityName: edge.parent
      childEntityName: edge.child
    }
    dependsOn: [
      rootEntity
      flows
      components
      resourceEntities
    ]
  }
]

output healthModelId string = healthModel.id
output principalId string = healthModel.identity.principalId
