using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCPRegistry.UI.Models;

public class ServerListResponse
{
    [JsonPropertyName("servers")]
    public List<ServerResponseItem> Servers { get; set; } = [];

    [JsonPropertyName("metadata")]
    public ServerListMetadata Metadata { get; set; } = new();
}

public class ServerResponseItem
{
    [JsonPropertyName("server")]
    public ServerDetail Server { get; set; } = new();

    [JsonPropertyName("_meta")]
    public Dictionary<string, object>? Meta { get; set; }

    public string Status
    {
        get
        {
            if (Meta?.TryGetValue("io.modelcontextprotocol.registry/official", out var officialObj) == true
                && officialObj is JsonElement element
                && element.TryGetProperty("status", out var statusProp))
            {
                return statusProp.GetString() ?? "active";
            }
            return "active";
        }
    }

    public bool IsDeleted => string.Equals(Status, "deleted", StringComparison.OrdinalIgnoreCase);
}

public class ServerListMetadata
{
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class ServerDetail
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    [JsonPropertyName("repository")]
    public RepositoryInfo? Repository { get; set; }

    [JsonPropertyName("packages")]
    public List<PackageInfo>? Packages { get; set; }

    [JsonPropertyName("remotes")]
    public List<RemoteInfo>? Remotes { get; set; }
}

public class RepositoryInfo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("subfolder")]
    public string? Subfolder { get; set; }
}

public class PackageInfo
{
    [JsonPropertyName("registryType")]
    public string RegistryType { get; set; } = "npm";

    [JsonPropertyName("registryBaseUrl")]
    public string? RegistryBaseUrl { get; set; }

    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("runtimeHint")]
    public string? RuntimeHint { get; set; }

    [JsonPropertyName("transport")]
    public TransportInfo? Transport { get; set; }
}

public class RemoteInfo
{
    [JsonPropertyName("$transport-type")]
    public string TransportType { get; set; } = "streamable-http";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "streamable-http";

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class TransportInfo
{
    [JsonPropertyName("$transport-type")]
    public string TransportType { get; set; } = "stdio";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "stdio";
}
