using System.Text.Json.Serialization;

namespace MCPRegistry.Models;

public class ServerList
{
    [JsonPropertyName("servers")]
    public required List<ServerResponse> Servers { get; set; }

    [JsonPropertyName("metadata")]
    public ServerListMetadata? Metadata { get; set; }
}

public class ServerListMetadata
{
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
