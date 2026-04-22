// Naming conventions based on Azure CAF resource abbreviations
// https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations
// Customers can override any resource name via parameters.

@export()
func getDefaultName(prefix string, workload string, suffix string) string =>
  '${prefix}-${workload}-${suffix}'

@export()
func getDefaultNameNoDashes(prefix string, workload string, suffix string) string =>
  '${prefix}${workload}${suffix}'

@export()
func generateSuffix(subscriptionId string, environmentName string, location string) string =>
  take(uniqueString(subscriptionId, environmentName, location), 6)
