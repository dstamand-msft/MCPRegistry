# MCP Registry Management UI

A Blazor Server web application for managing the MCP Server Registry. Provides a visual interface to browse, search, add, and delete MCP server entries.

## Features

- **Browse Servers** — View all registered MCP servers with name, title, version, and description
- **Search** — Filter servers by name, title, or description (real-time search)
- **Add Servers** — Form-based server registration with validation (name format, semver version)
- **View Versions** — Browse all versions of a specific server
- **Delete Versions** — Soft-delete server versions with confirmation dialog
- **Responsive** — Bootstrap-based layout works on desktop and mobile

## Running Locally

### Prerequisites
- .NET 10 SDK
- MCP Registry API running on `http://localhost:5103`

### Start the API first
```powershell
dotnet run --project ../MCPRegistry
```

### Start the UI
```powershell
dotnet run --project .
```

The UI will be available at **http://localhost:5207**

### Configuration

The API base URL is configured in `appsettings.json` or via environment variable:

```json
{
  "ApiBaseUrl": "http://localhost:5103"
}
```

For Azure deployment, this is automatically set to the API Container App's URL via Bicep.

## Pages

| Route | Page | Description |
|-------|------|-------------|
| `/` | Server List | Browse and search all servers, delete with confirmation |
| `/servers/add` | Add Server | Form to register a new MCP server version |
| `/servers/{name}` | Server Versions | View all versions of a specific server |

## Usage Patterns

### Adding a server
1. Navigate to **Add Server** (sidebar or `+ Add Server` button)
2. Enter the server name in `domain/name` format (e.g., `com.microsoft/azure`)
3. Enter the version in semver format (e.g., `1.0.0`, `2.0.0-beta.1`)
4. Optionally add title, description, and website URL
5. Click **Add Server**

### Searching
- Type in the search box and press Enter or click Search
- Searches across server name, title, and description
- Click **Clear** to reset the filter

### Deleting a version
- Click the **Delete** button next to any server version
- Confirm in the modal dialog
- This is a soft-delete — sets the server status to "deleted" per MCP spec

### Viewing all versions
- Click **Versions** next to any server to see all registered versions
- Each version can be individually deleted

## Project Structure

```
MCPRegistry.UI/
├── Components/
│   ├── Layout/
│   │   └── NavMenu.razor          # Sidebar navigation
│   ├── Pages/
│   │   ├── Home.razor              # Server list with search & delete
│   │   ├── AddServer.razor         # Add server form
│   │   └── ServerVersions.razor    # Version list per server
│   ├── App.razor
│   └── Routes.razor
├── Models/
│   └── ServerModels.cs             # DTOs for API responses
├── Services/
│   └── McpRegistryClient.cs        # HTTP client for MCP Registry API
├── Dockerfile
├── Program.cs
└── appsettings.json
```
