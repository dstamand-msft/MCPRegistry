using System.Text.Json.Serialization;

namespace MCPRegistry.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$transport-type")]
[JsonDerivedType(typeof(StdioTransport), "stdio")]
[JsonDerivedType(typeof(StreamableHttpTransport), "streamable-http")]
[JsonDerivedType(typeof(SseTransport), "sse")]
public abstract class Transport
{
    public abstract string Type { get; }
}

public class StdioTransport : Transport
{
    [JsonPropertyName("type")]
    public override string Type => "stdio";
}

public class StreamableHttpTransport : Transport
{
    [JsonPropertyName("type")]
    public override string Type => "streamable-http";

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonPropertyName("headers")]
    public List<KeyValueInput>? Headers { get; set; }
}

public class SseTransport : Transport
{
    [JsonPropertyName("type")]
    public override string Type => "sse";

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonPropertyName("headers")]
    public List<KeyValueInput>? Headers { get; set; }
}
