using MCPRegistry.Models;

namespace MCPRegistry.Services;

public interface IServerRegistryService
{
    Task<(List<ServerResponse> servers, string? nextCursor, int count)> GetServersAsync(
        string? cursor,
        int? limit,
        string? search,
        DateTime? updatedSince,
        string? version);

    Task<(List<ServerResponse> versions, int count)> GetServerVersionsAsync(string serverName);

    Task<ServerResponse?> GetServerVersionAsync(string serverName, string version);

    Task<bool> DeleteServerVersionAsync(string serverName, string version);
    Task AddServerAsync(ServerDetail server);
}