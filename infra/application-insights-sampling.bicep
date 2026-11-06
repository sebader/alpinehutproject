targetScope = 'resourceGroup'

param componentName string
param location string
param componentTags object
param componentProperties object

// Preserve the existing component; rare-job health checks need complete traces.
resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: componentName
  location: location
  kind: 'web'
  tags: componentTags
  properties: union(componentProperties, {
    SamplingPercentage: 100
  })
}
