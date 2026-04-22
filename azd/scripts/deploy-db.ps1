#!/usr/bin/env pwsh
# Deploy the database schema using the dacpac produced by the SQL project.
# Runs as a postprovision hook or standalone.
# SQL target platform (DSP) reference:
# https://learn.microsoft.com/en-us/sql/tools/sql-database-projects/concepts/target-platform?view=sql-server-ver17&pivots=sq1-command-line

param(
    [string]$ServerName,
    [string]$DatabaseName,
    [string]$TargetPlatform
)

Write-Host "Deploying database schema via dacpac..."

$resourceGroup = $null
if (-not $ServerName -or -not $DatabaseName) {
    $envValues = azd env get-values --output json | ConvertFrom-Json
    if (-not $ServerName) { $ServerName = $envValues.AZURE_SQL_SERVER_NAME }
    if (-not $DatabaseName) { $DatabaseName = $envValues.AZURE_SQL_DATABASE_NAME }
    if (-not $TargetPlatform) { $TargetPlatform = $envValues.SQL_TARGET_PLATFORM }
    $resourceGroup = $envValues.AZURE_RESOURCE_GROUP
}

if (-not $TargetPlatform) {
    $TargetPlatform = $env:SQL_TARGET_PLATFORM
}

if (-not $TargetPlatform) {
    $TargetPlatform = "Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider"
}

if (-not $ServerName -or -not $DatabaseName) {
    Write-Error "Missing SQL server or database name. Provide via parameters or azd env."
    exit 1
}

if (-not $resourceGroup) {
    $resourceGroup = az sql server show --name $ServerName --query resourceGroup -o tsv 2>$null
}

$sqlFqdn = if ($ServerName -like '*.database.windows.net') { $ServerName } else { "${ServerName}.database.windows.net" }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot (Join-Path ".." ".."))).Path
$dacpacPath = Join-Path $repoRoot "src\MCPRegistryDatabase\bin\Release\MCPRegistryDatabase.dacpac"
if ($TargetPlatform) {
    Write-Host "Using SQL target platform (DSP): $TargetPlatform"
}

$shouldBuildDacpac = (-not (Test-Path $dacpacPath)) -or [bool]$TargetPlatform
if ($shouldBuildDacpac) {
    if (Test-Path $dacpacPath) {
        Write-Host "Target platform override provided, rebuilding dacpac..."
    }
    else {
        Write-Host "Dacpac not found, building..."
    }

    $buildArgs = @(
        (Join-Path $repoRoot "src\MCPRegistryDatabase\MCPRegistryDatabase.sqlproj"),
        "-c",
        "Release"
    )
    if ($TargetPlatform) {
        $buildArgs += "/p:DSP=$TargetPlatform"
    }

    dotnet build @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build SQL project."
        exit 1
    }
}

# Add temporary firewall rule for deployer's IP
$myIp = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 10)
$firewallRuleName = "azd-deploy-$($myIp.Replace('.', '-'))"
Write-Host "Adding temporary firewall rule for $myIp..."
az sql server firewall-rule create --resource-group $resourceGroup --server $ServerName --name $firewallRuleName --start-ip-address $myIp --end-ip-address $myIp --output none 2>$null

# Also set connection policy to Proxy to avoid TLS redirect issues from outside Azure
az sql server conn-policy update --server $ServerName --resource-group $resourceGroup --connection-type Proxy --output none 2>$null

Write-Host "Server: $sqlFqdn"
Write-Host "Database: $DatabaseName"
Write-Host "Dacpac: $dacpacPath"

$token = az account get-access-token --resource "https://database.windows.net/" --query accessToken -o tsv
if (-not $token) {
    Write-Error "Failed to get SQL access token. Ensure you are logged in with 'az login'."
    exit 1
}

sqlpackage /Action:Publish `
    /SourceFile:"$dacpacPath" `
    /TargetServerName:"$sqlFqdn" `
    /TargetDatabaseName:"$DatabaseName" `
    /AccessToken:"$token" `
    /p:BlockOnPossibleDataLoss=False

$deployResult = $LASTEXITCODE

# Restore connection policy and remove temporary firewall rule
Write-Host "Restoring connection policy and removing temporary firewall rule..."
az sql server conn-policy update --server $ServerName --resource-group $resourceGroup --connection-type Default --output none 2>$null
az sql server firewall-rule delete --resource-group $resourceGroup --server $ServerName --name $firewallRuleName --output none 2>$null

if ($deployResult -ne 0) {
    Write-Error "dacpac deployment failed."
    exit 1
}

Write-Host "Database schema deployed successfully."
