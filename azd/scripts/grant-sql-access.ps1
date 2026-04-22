#!/usr/bin/env pwsh
# Grant the managed identity data-plane access to the SQL database.
# Runs as a postprovision hook after azd provision.

Write-Host "Granting SQL data-plane access to managed identity..."

$envValues = azd env get-values --output json | ConvertFrom-Json
$sqlServer = $envValues.AZURE_SQL_SERVER_NAME
$sqlDatabase = $envValues.AZURE_SQL_DATABASE_NAME
$resourceGroup = $envValues.AZURE_RESOURCE_GROUP

if (-not $sqlServer -or -not $sqlDatabase -or -not $resourceGroup) {
    Write-Error "Missing required azd env values (AZURE_SQL_SERVER_NAME, AZURE_SQL_DATABASE_NAME, AZURE_RESOURCE_GROUP)."
    exit 1
}

$identityName = $envValues.SERVICE_WEB_NAME
if (-not $identityName) {
    Write-Error "Missing SERVICE_WEB_NAME in azd env."
    exit 1
}

$managedIdentityResource = az containerapp show --name $identityName --resource-group $resourceGroup --query "identity.userAssignedIdentities | keys(@) | [0]" -o tsv
$managedIdentityDisplayName = ($managedIdentityResource -split '/')[-1]

$sqlFqdn = "${sqlServer}.database.windows.net"

Write-Host "SQL Server: $sqlFqdn"
Write-Host "Database: $sqlDatabase"
Write-Host "Identity: $managedIdentityDisplayName"

$token = az account get-access-token --resource "https://database.windows.net/" --query accessToken -o tsv
if (-not $token) {
    Write-Error "Failed to get SQL access token. Ensure you are logged in with 'az login'."
    exit 1
}

# Add temporary firewall rule and set Proxy connection policy
$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 10)
$firewallRuleName = "azd-grant-$($myIp.Replace('.', '-'))"
az sql server firewall-rule create --resource-group $resourceGroup --server $sqlServer --name $firewallRuleName --start-ip-address $myIp --end-ip-address $myIp --output none 2>$null
az sql server conn-policy update --server $sqlServer --resource-group $resourceGroup --connection-type Proxy --output none 2>$null

# Grant access using SqlClient with token auth
$grantSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$managedIdentityDisplayName')
BEGIN
    CREATE USER [$managedIdentityDisplayName] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_datareader ADD MEMBER [$managedIdentityDisplayName];
ALTER ROLE db_datawriter ADD MEMBER [$managedIdentityDisplayName];
"@

$connStr = "Server=tcp:${sqlFqdn},1433;Database=${sqlDatabase};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.AccessToken = $token
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $grantSql
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host "Granted db_datareader + db_datawriter to $managedIdentityDisplayName"
}
catch {
    Write-Error "Failed to grant SQL access: $_"
    exit 1
}
finally {
    $conn.Close()
}

# Restore and clean up
az sql server conn-policy update --server $sqlServer --resource-group $resourceGroup --connection-type Default --output none 2>$null
az sql server firewall-rule delete --resource-group $resourceGroup --server $sqlServer --name $firewallRuleName --output none 2>$null

Write-Host "SQL data-plane access granted successfully."
