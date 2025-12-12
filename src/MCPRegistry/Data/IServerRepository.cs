using MCPRegistry.Models;

namespace MCPRegistry.Data;

public interface IServerRepository
{
    Task<List<ServerDetail>> GetServersAsync(
        string? cursorServerName,
        string? cursorVersion,
        int take,
        string? search,
        DateTime? updatedSince,
        string? version);

    Task<List<ServerDetail>> GetServerVersionsAsync(string serverName);

    Task<ServerDetail?> GetServerVersionAsync(string serverName, string version);

    Task<bool> DeleteServerVersionAsync(string serverName, string version);

    Task AddServerAsync(ServerDetail server);
}
