using System.Text.Json.Serialization;

namespace MCPRegistry.Models;

public class Package
{
    [JsonPropertyName("registryType")]
    public required string RegistryType { get; set; }

    [JsonPropertyName("registryBaseUrl")]
    public string? RegistryBaseUrl { get; set; }

    [JsonPropertyName("identifier")]
    public required string Identifier { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("fileSha256")]
    public string? FileSha256 { get; set; }

    [JsonPropertyName("runtimeHint")]
    public string? RuntimeHint { get; set; }

    [JsonPropertyName("transport")]
    public required Transport Transport { get; set; }

    [JsonPropertyName("runtimeArguments")]
    public List<object>? RuntimeArguments { get; set; }

    [JsonPropertyName("packageArguments")]
    public List<object>? PackageArguments { get; set; }

    [JsonPropertyName("environmentVariables")]
    public List<KeyValueInput>? EnvironmentVariables { get; set; }
}
