# MCP Registry API

This is an implementation of the Model Context Protocol (MCP) Server Registry API based on the official OpenAPI specification.

## Features

- **List MCP Servers** - Browse all registered MCP servers with pagination, filtering, and search
- **List Server Versions** - View all available versions of a specific MCP server
- **Get Server Details** - Retrieve detailed information about a specific server version
- **Delete Server Version** - Optional endpoint to delete a specific server version
- **Sample Data** - Includes sample servers for testing (Filesystem and Brave Search)

## API Endpoints

All endpoints are prefixed with `/v0.1` (not `/v0` as in the original spec):

### GET /v0.1/servers
List all MCP servers with optional filtering and pagination.

**Query Parameters:**
- `cursor` - Pagination cursor for next page of results
- `limit` - Maximum number of items to return (default: 30)
- `search` - Search servers by name (substring match)
- `updated_since` - Filter servers updated since timestamp (RFC3339 datetime)
- `version` - Filter by version ('latest' for latest version, or exact version)

**Example:**
```
GET /v0.1/servers?search=filesystem&limit=10
```

### GET /v0.1/servers/{serverName}/versions
List all versions of a specific MCP server, ordered by publication date (newest first).

**Path Parameters:**
- `serverName` - URL-encoded server name (e.g., `io.modelcontextprotocol%2Ffilesystem`)

**Example:**
```
GET /v0.1/servers/io.modelcontextprotocol%2Ffilesystem/versions
```

### GET /v0.1/servers/{serverName}/versions/{version}
Get detailed information about a specific version of an MCP server.

**Path Parameters:**
- `serverName` - URL-encoded server name
- `version` - Version number or `latest` for the latest version

**Example:**
```
GET /v0.1/servers/io.modelcontextprotocol%2Ffilesystem/versions/1.0.2
GET /v0.1/servers/io.modelcontextprotocol%2Ffilesystem/versions/latest
```

### DELETE /v0.1/servers/{serverName}/versions/{version}
Delete a specific version of an MCP server (optional endpoint).

**Path Parameters:**
- `serverName` - URL-encoded server name
- `version` - Version number to delete

**Example:**
```
DELETE /v0.1/servers/io.modelcontextprotocol%2Ffilesystem/versions/1.0.1
```

## Running the Application

### Prerequisites
- .NET 10.0 SDK

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

The API will be available at:
- HTTPS: `https://localhost:7000`
- Swagger UI: `https://localhost:7000/swagger` (in Development mode)

## Testing

Use the included `MCPRegistry.http` file to test the API endpoints directly from VS Code (requires REST Client extension).

## Sample Data

The API comes with sample data for testing:

1. **Filesystem Server** (`io.modelcontextprotocol/filesystem`)
   - Version 1.0.2 (latest)
   - Version 1.0.1 (older)

2. **Brave Search Server** (`io.modelcontextprotocol/brave-search`)
   - Version 0.1.0 (latest)

## Architecture

- **Models/** - Data transfer objects (DTOs) matching the OpenAPI schema
- **Services/** - Business logic for server registry management
- **Controllers/** - API endpoints implementation

The current implementation uses an in-memory data store (`ServerRegistryService`) with sample data. In production, this would be replaced with a persistent data store.

## Notes

- The POST `/v0.1/publish` endpoint is **not implemented** as per requirements
- All endpoints use `/v0.1` prefix instead of `/v0`
- The DELETE endpoint is optional and returns 200 on success (registry supports deletion)
- Server names in URLs must be URL-encoded (forward slashes become `%2F`)
