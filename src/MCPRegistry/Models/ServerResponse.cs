using System.Text.Json.Serialization;

namespace MCPRegistry.Models;

public class ServerResponse
{
    [JsonPropertyName("server")]
    public required ServerDetail Server { get; set; }

    [JsonPropertyName("_meta")]
    public Dictionary<string, object> Meta { get; set; } = new();
}

public class OfficialRegistryMeta
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ServerStatus Status { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("isLatest")]
    public bool IsLatest { get; set; }
}

public enum ServerStatus
{
    [JsonPropertyName("active")]
    Active,
    [JsonPropertyName("deprecated")]
    Deprecated,
    [JsonPropertyName("deleted")]
    Deleted
}
