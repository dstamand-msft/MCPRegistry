# MCP Registry API

This is an implementation of the Model Context Protocol (MCP) Server Registry API based on the official OpenAPI specification.
For more information, see [GitHub MCP registry documentation](https://docs.github.com/en/copilot/how-tos/administer-copilot/manage-mcp-usage/configure-mcp-registry#option-1-self-hosting-an-mcp-registry).

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

The API comes with the following sample servers for testing (see `data/sample-seed-data.json`):

1. **Azure MCP Server** (`com.microsoft/azure`)
   - Title: Azure MCP Server
   - Description: All Azure MCP tools to create a seamless connection between AI agents and Azure services.
   - Version: 2.0.0-beta.6
   - Website: https://azure.microsoft.com/
   - Packages: npm (@azure/mcp), nuget (Azure.Mcp)

2. **Azure DevOps MCP Server** (`com.microsoft/azure-devops-mcp`)
   - Title: Azure DevOps MCP Server
   - Description: Azure DevOps—work items, repositories, pipelines, test plans, wiki, search, and more.
   - Version: 2.2.2
   - Website: https://github.com/microsoft/azure-devops-mcp
   - Packages: npm (@azure-devops/mcp)

3. **Microsoft Learn MCP** (`com.microsoft/microsoft-learn-mcp`)
   - Title: Microsoft Learn MCP
   - Description: Official Microsoft Learn MCP Server – real-time, trusted docs & code samples for AI and LLMs.
   - Version: 1.0.0
   - Website: https://github.com/MicrosoftDocs/mcp
   - Remotes: https://learn.microsoft.com/api/mcp

4. **Atlassian MCP Server** (`com.atlassian/atlassian-mcp-server`)
   - Description: Connect to Jira and Confluence for issue tracking and documentation.
   - Version: 1.0.0
   - Remotes: https://mcp.atlassian.com/v1/sse

5. **GitHub MCP Server** (`io.github.github/github-mcp-server`)
   - Description: Official GitHub Remote MCP Server offering the default toolset for GitHub integrations.
   - Version: 1.0.0
   - Website: https://github.com/github/github-mcp-server
   - Remotes: https://api.githubcopilot.com/mcp/

## Adding a New Server Example

To add a new server, you can use the format in `data/new-version-data.json`. For example, to add a new Azure MCP Server version:

```json
[
  {
    "$schema": "https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json",
    "name": "com.microsoft/azure",
    "description": "All Azure MCP tools to create a seamless connection between AI agents and Azure services.",
    "title": "Azure MCP Server",
    "repository": {
      "url": "https://github.com/microsoft/mcp",
      "source": "github",
      "subfolder": "servers/Azure.Mcp.Server"
    },
    "version": "2.0.0-beta.7",
    "websiteUrl": "https://azure.microsoft.com/",
    "packages": [
      {
        "registryType": "npm",
        "registryBaseUrl": "https://registry.npmjs.org",
        "identifier": "@azure/mcp",
        "version": "2.0.0-beta.7",
        "transport": {
          "$transport-type": "stdio",
          "type": "stdio"
        },
        "packageArguments": [
          { "value": "server", "type": "positional" },
          { "value": "start", "type": "positional" }
        ]
      },
      {
        "registryType": "nuget",
        "identifier": "Azure.Mcp",
        "version": "2.0.0-beta.7",
        "transport": {
          "$transport-type": "stdio",
          "type": "stdio"
        },
        "packageArguments": [
          { "value": "server", "type": "positional" },
          { "value": "start", "type": "positional" }
        ]
      }
    ]
  }
]
```

You can POST this JSON to the appropriate endpoint (when implemented) or use it as a template for adding new servers to the registry's data store.


## Architecture

- **Models/** - Data transfer objects (DTOs) matching the OpenAPI schema
- **Services/** - Business logic for server registry management
- **Controllers/** - API endpoints implementation
- **Data/** - Data access layer, including repository interfaces and implementations
- **SQLProject/** - Contains the SQL Server database project for schema and migrations

### Data Store Implementation

The current implementation uses a SQL Server-based data store for server registry persistence. The data access is handled through the `IServerRepository` interface (`MCPRegistry.Data.IServerRepository`), which defines all required operations for managing servers and their versions. The default implementation, `SqlServerServerRepository`, provides a concrete integration with SQL Server.

The solution includes a `SQLProject` directory containing the SQL Server database project, which manages the schema and migrations for the registry data.

#### Extensibility

To support other data stores (e.g., PostgreSQL, MongoDB, in-memory, etc.), implement the `IServerRepository` interface with your own data access logic. Register your implementation in the dependency injection container to replace or extend the default SQL Server-based repository. This design allows for easy swapping or extension of the data storage backend without changing the business logic or API controllers.

**Key extensibility point:**

- Implement the `IServerRepository` interface to add support for a new data store.

## Notes

- The POST `/v0.1/publish` endpoint is **not implemented**
- All endpoints use `/v0.1` prefix instead of `/v0`
- The DELETE endpoint is optional and returns 200 on success (registry supports deletion, in soft mode)
- Server names in URLs must be URL-encoded (forward slashes become `%2F`)
- If you want to test locally using VSCode, set the `chat.mcp.gallery.serviceUrl` setting in your VSCode settings to point to your local instance (e.g., `https://localhost:7160/v0.1/servers`)