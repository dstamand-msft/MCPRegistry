#!/bin/bash
# Deploy the database schema using the dacpac produced by the SQL project.
# Runs as a postprovision hook or standalone.
# SQL target platform (DSP) reference:
# https://learn.microsoft.com/en-us/sql/tools/sql-database-projects/concepts/target-platform?view=sql-server-ver17&pivots=sq1-command-line
set -euo pipefail

echo "Deploying database schema via dacpac..."

if [ -z "${1:-}" ] || [ -z "${2:-}" ]; then
    eval "$(azd env get-values)"
    SERVER_NAME="${1:-${AZURE_SQL_SERVER_NAME:-}}"
    DATABASE_NAME="${2:-${AZURE_SQL_DATABASE_NAME:-}}"
    TARGET_PLATFORM="${3:-${SQL_TARGET_PLATFORM:-}}"
else
    SERVER_NAME="$1"
    DATABASE_NAME="$2"
    TARGET_PLATFORM="${3:-${SQL_TARGET_PLATFORM:-}}"
fi

if [ -z "$SERVER_NAME" ] || [ -z "$DATABASE_NAME" ]; then
    echo "ERROR: Missing SQL server or database name."
    exit 1
fi

case "$SERVER_NAME" in
    *.database.windows.net) SQL_FQDN="$SERVER_NAME" ;;
    *) SQL_FQDN="${SERVER_NAME}.database.windows.net" ;;
esac

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DACPAC_PATH="$SCRIPT_DIR/../../src/MCPRegistryDatabase/bin/Release/MCPRegistryDatabase.dacpac"

if [ -n "${TARGET_PLATFORM:-}" ]; then
    echo "Using SQL target platform (DSP): $TARGET_PLATFORM"
fi

if [ -z "${TARGET_PLATFORM:-}" ]; then
    TARGET_PLATFORM="Microsoft.Data.Tools.Schema.Sql.SqlAzureV12DatabaseSchemaProvider"
    echo "Defaulting SQL target platform (DSP): $TARGET_PLATFORM"
fi

if [ ! -f "$DACPAC_PATH" ] || [ -n "${TARGET_PLATFORM:-}" ]; then
    if [ -f "$DACPAC_PATH" ] && [ -n "${TARGET_PLATFORM:-}" ]; then
        echo "Target platform override provided, rebuilding dacpac..."
    else
        echo "Dacpac not found, building..."
    fi

    BUILD_ARGS=(-c Release)
    if [ -n "${TARGET_PLATFORM:-}" ]; then
        BUILD_ARGS+=("/p:DSP=$TARGET_PLATFORM")
    fi

    dotnet build "$SCRIPT_DIR/../../src/MCPRegistryDatabase/MCPRegistryDatabase.sqlproj" "${BUILD_ARGS[@]}"
fi

echo "Server: $SQL_FQDN"
echo "Database: $DATABASE_NAME"
echo "Dacpac: $DACPAC_PATH"

TOKEN=$(az account get-access-token --resource "https://database.windows.net/" --query accessToken -o tsv)
if [ -z "$TOKEN" ]; then
    echo "ERROR: Failed to get SQL access token."
    exit 1
fi

sqlpackage /Action:Publish \
    /SourceFile:"$DACPAC_PATH" \
    /TargetServerName:"$SQL_FQDN" \
    /TargetDatabaseName:"$DATABASE_NAME" \
    /AccessToken:"$TOKEN" \
    /p:BlockOnPossibleDataLoss=False

echo "Database schema deployed successfully."
