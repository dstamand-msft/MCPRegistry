using MCPRegistry.Models;
using System.Collections.Concurrent;

namespace MCPRegistry.Services;

public class ServerRegistryService : IServerRegistryService
{
    private readonly ConcurrentDictionary<string, List<ServerResponse>> _servers = new();

    public ServerRegistryService()
    {
        SeedSampleData();
    }

    public Task<(List<ServerResponse> servers, string? nextCursor, int count)> GetServersAsync(
        string? cursor,
        int? limit,
        string? search,
        DateTime? updatedSince,
        string? version)
    {
        var allServers = _servers.Values.SelectMany(v => v).ToList();

        if (!string.IsNullOrEmpty(search))
        {
            allServers = allServers.Where(s =>
                s.Server.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (s.Server.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                s.Server.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        if (updatedSince.HasValue)
        {
            allServers = allServers.Where(s => s.Meta.Official?.UpdatedAt >= updatedSince.Value).ToList();
        }

        if (!string.IsNullOrEmpty(version))
        {
            allServers = version == "latest"
                ? allServers.Where(s => s.Meta.Official?.IsLatest == true).ToList()
                : allServers.Where(s => s.Server.Version == version).ToList();
        }

        allServers = allServers
            .OrderByDescending(s => s.Meta.Official?.UpdatedAt ?? DateTime.MinValue)
            .ToList();

        var skip = 0;
        if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out var cursorValue))
        {
            skip = cursorValue;
        }

        var pageSize = limit ?? 30;
        var pagedServers = allServers.Skip(skip).Take(pageSize).ToList();
        var hasMore = allServers.Count > skip + pageSize;
        var nextCursor = hasMore ? (skip + pageSize).ToString() : null;

        return Task.FromResult((pagedServers, nextCursor, pagedServers.Count));
    }

    public Task<(List<ServerResponse> versions, int count)> GetServerVersionsAsync(string serverName)
    {
        if (_servers.TryGetValue(serverName, out var versions))
        {
            var sortedVersions = versions
                .OrderByDescending(v => v.Meta.Official?.PublishedAt ?? DateTime.MinValue)
                .ToList();

            return Task.FromResult((sortedVersions, sortedVersions.Count));
        }

        return Task.FromResult((new List<ServerResponse>(), 0));
    }

    public Task<ServerResponse?> GetServerVersionAsync(string serverName, string version)
    {
        if (!_servers.TryGetValue(serverName, out var versions))
        {
            return Task.FromResult<ServerResponse?>(null);
        }

        if (version.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            var latestVersion = versions.FirstOrDefault(v => v.Meta.Official?.IsLatest == true);
            return Task.FromResult(latestVersion);
        }

        var specificVersion = versions.FirstOrDefault(v => v.Server.Version == version);
        return Task.FromResult(specificVersion);
    }

    public Task<bool> DeleteServerVersionAsync(string serverName, string version)
    {
        if (!_servers.TryGetValue(serverName, out var versions))
        {
            return Task.FromResult(false);
        }

        var versionToDelete = versions.FirstOrDefault(v => v.Server.Version == version);
        if (versionToDelete == null)
        {
            return Task.FromResult(false);
        }

        versions.Remove(versionToDelete);

        if (versions.Count == 0)
        {
            _servers.TryRemove(serverName, out _);
        }
        else if (versionToDelete.Meta.Official?.IsLatest == true)
        {
            var newLatest = versions
                .OrderByDescending(v => v.Meta.Official?.PublishedAt ?? DateTime.MinValue)
                .FirstOrDefault();

            if (newLatest?.Meta.Official != null)
            {
                newLatest.Meta.Official.IsLatest = true;
            }
        }

        return Task.FromResult(true);
    }

    private void SeedSampleData()
    {
        var filesystemServer = new ServerResponse
        {
            Server = new ServerDetail
            {
                Name = "io.modelcontextprotocol/filesystem",
                Description = "Node.js server implementing Model Context Protocol (MCP) for filesystem operations.",
                Title = "Filesystem",
                Version = "1.0.2",
                Repository = new Repository
                {
                    Url = "https://github.com/modelcontextprotocol/servers",
                    Source = "github"
                },
                Packages = new List<Package>
                {
                    new()
                    {
                        RegistryType = "npm",
                        RegistryBaseUrl = "https://registry.npmjs.org",
                        Identifier = "@modelcontextprotocol/server-filesystem",
                        Version = "1.0.2",
                        Transport = new StdioTransport()
                    }
                }
            },
            Meta = new ServerResponseMeta
            {
                Official = new OfficialRegistryMeta
                {
                    Status = ServerStatus.Active,
                    PublishedAt = DateTime.UtcNow.AddDays(-30),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5),
                    IsLatest = true
                }
            }
        };

        var filesystemServerOld = new ServerResponse
        {
            Server = new ServerDetail
            {
                Name = "io.modelcontextprotocol/filesystem",
                Description = "Node.js server implementing Model Context Protocol (MCP) for filesystem operations.",
                Title = "Filesystem",
                Version = "1.0.1",
                Repository = new Repository
                {
                    Url = "https://github.com/modelcontextprotocol/servers",
                    Source = "github"
                },
                Packages = new List<Package>
                {
                    new()
                    {
                        RegistryType = "npm",
                        RegistryBaseUrl = "https://registry.npmjs.org",
                        Identifier = "@modelcontextprotocol/server-filesystem",
                        Version = "1.0.1",
                        Transport = new StdioTransport()
                    }
                }
            },
            Meta = new ServerResponseMeta
            {
                Official = new OfficialRegistryMeta
                {
                    Status = ServerStatus.Active,
                    PublishedAt = DateTime.UtcNow.AddDays(-45),
                    UpdatedAt = DateTime.UtcNow.AddDays(-45),
                    IsLatest = false
                }
            }
        };

        _servers.TryAdd("io.modelcontextprotocol/filesystem", new List<ServerResponse> { filesystemServer, filesystemServerOld });

        var braveSearchServer = new ServerResponse
        {
            Server = new ServerDetail
            {
                Name = "io.modelcontextprotocol/brave-search",
                Description = "MCP server for Brave Search API integration",
                Title = "Brave Search",
                Version = "0.1.0",
                Repository = new Repository
                {
                    Url = "https://github.com/modelcontextprotocol/servers",
                    Source = "github",
                    Subfolder = "src/brave-search"
                },
                WebsiteUrl = "https://modelcontextprotocol.io",
                Packages = new List<Package>
                {
                    new()
                    {
                        RegistryType = "npm",
                        RegistryBaseUrl = "https://registry.npmjs.org",
                        Identifier = "@modelcontextprotocol/server-brave-search",
                        Version = "0.1.0",
                        Transport = new StdioTransport()
                    }
                }
            },
            Meta = new ServerResponseMeta
            {
                Official = new OfficialRegistryMeta
                {
                    Status = ServerStatus.Active,
                    PublishedAt = DateTime.UtcNow.AddDays(-10),
                    UpdatedAt = DateTime.UtcNow.AddDays(-2),
                    IsLatest = true
                }
            }
        };

        _servers.TryAdd("io.modelcontextprotocol/brave-search", new List<ServerResponse> { braveSearchServer });
    }
}
