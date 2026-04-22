#!/bin/bash
# Grant the managed identity data-plane access to the SQL database.
# Runs as a postprovision hook after azd provision.
set -euo pipefail

echo "Granting SQL data-plane access to managed identity..."

eval "$(azd env get-values)"

if [ -z "${AZURE_SQL_SERVER_NAME:-}" ] || [ -z "${AZURE_SQL_DATABASE_NAME:-}" ] || [ -z "${AZURE_RESOURCE_GROUP:-}" ]; then
    echo "ERROR: Missing required azd env values (AZURE_SQL_SERVER_NAME, AZURE_SQL_DATABASE_NAME, AZURE_RESOURCE_GROUP)."
    exit 1
fi

IDENTITY_NAME="${SERVICE_WEB_NAME:-}"
if [ -z "$IDENTITY_NAME" ]; then
    echo "ERROR: Missing SERVICE_WEB_NAME in azd env."
    exit 1
fi

MANAGED_IDENTITY_RESOURCE=$(az containerapp show --name "$IDENTITY_NAME" --resource-group "$AZURE_RESOURCE_GROUP" --query "identity.userAssignedIdentities | keys(@) | [0]" -o tsv)
MANAGED_IDENTITY_DISPLAY_NAME=$(echo "$MANAGED_IDENTITY_RESOURCE" | awk -F'/' '{print $NF}')

SQL_FQDN="${AZURE_SQL_SERVER_NAME}.database.windows.net"

echo "SQL Server: $SQL_FQDN"
echo "Database: $AZURE_SQL_DATABASE_NAME"
echo "Identity: $MANAGED_IDENTITY_DISPLAY_NAME"

TOKEN=$(az account get-access-token --resource "https://database.windows.net/" --query accessToken -o tsv)
if [ -z "$TOKEN" ]; then
    echo "ERROR: Failed to get SQL access token."
    exit 1
fi

SQL_CMD="
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$MANAGED_IDENTITY_DISPLAY_NAME')
BEGIN
    CREATE USER [$MANAGED_IDENTITY_DISPLAY_NAME] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_datareader ADD MEMBER [$MANAGED_IDENTITY_DISPLAY_NAME];
ALTER ROLE db_datawriter ADD MEMBER [$MANAGED_IDENTITY_DISPLAY_NAME];
"

sqlcmd -S "$SQL_FQDN" -d "$AZURE_SQL_DATABASE_NAME" --authentication-method=ActiveDirectoryDefault -Q "$SQL_CMD"

echo "SQL data-plane access granted successfully."
