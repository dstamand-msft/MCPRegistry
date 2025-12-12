using MCPRegistry.Data;
using MCPRegistry.Models;

namespace MCPRegistry.Services;

public class ServerRegistryService : IServerRegistryService
{
    private readonly IServerRepository _repository;

    public ServerRegistryService(IServerRepository repository)
    {
        _repository = repository;
    }

    public async Task<(List<ServerDetail> servers, string? nextCursor)> GetServersAsync(
        string? cursor,
        int? limit,
        string? search,
        DateTime? updatedSince,
        string? version)
    {
        string? cursorServerName = null;
        string? cursorVersion = null;

        // Parse composite cursor of format "serverName:version"
        // Fallback for malformed cursor: treat entire value as server name only
        if (!string.IsNullOrEmpty(cursor))
        {
            var parts = cursor.Split(':');
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            {
                cursorServerName = parts[0];
                cursorVersion = parts[1];
            }
            else
            {
                cursorServerName = cursor;
            }
        }

        var pageSize = limit ?? 30;

        var servers = await _repository.GetServersAsync(cursorServerName, cursorVersion, pageSize, search, updatedSince, version);

        // Compute nextCursor: if we filled the page, use last item's serverName:version
        var nextCursor = servers.Count == pageSize
            ? $"{servers[^1].Name}:{servers[^1].Version}"
            : null;

        return (servers, nextCursor);
    }

    public async Task<List<ServerDetail>> GetServerVersionsAsync(string serverName)
    {
        var versions = await _repository.GetServerVersionsAsync(serverName);
        return versions;
    }

    public async Task<ServerDetail?> GetServerVersionAsync(string serverName, string version)
    {
        return await _repository.GetServerVersionAsync(serverName, version);
    }

    public async Task<bool> DeleteServerVersionAsync(string serverName, string version)
    {
        return await _repository.DeleteServerVersionAsync(serverName, version);
    }

    public async Task AddServerAsync(ServerDetail server)
    {
        await _repository.AddServerAsync(server);
    }
}

