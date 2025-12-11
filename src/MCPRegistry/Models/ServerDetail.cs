using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MCPRegistry.Models;

public class ServerDetail
{
    [JsonPropertyName("name")]
    [Required]
    [MinLength(3)]
    [MaxLength(200)]
    [RegularExpression("^[a-zA-Z0-9.-]+/[a-zA-Z0-9._-]+$")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public required string Description { get; set; }

    [JsonPropertyName("title")]
    [MinLength(1)]
    [MaxLength(100)]
    public string? Title { get; set; }

    [JsonPropertyName("repository")]
    public Repository? Repository { get; set; }

    [JsonPropertyName("version")]
    [Required]
    [MaxLength(255)]
    public required string Version { get; set; }

    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    [JsonPropertyName("icons")]
    public List<Icon>? Icons { get; set; }

    [JsonPropertyName("$schema")]
    [JsonPropertyOrder(-1)]
    public string? Schema { get; set; } = "https://static.modelcontextprotocol.io/schemas/2025-10-17/server.schema.json";

    [JsonPropertyName("packages")]
    public List<Package>? Packages { get; set; }

    [JsonPropertyName("remotes")]
    public List<Transport>? Remotes { get; set; }

    [JsonPropertyName("_meta")]
    public Dictionary<string, object>? Meta { get; set; }
}
