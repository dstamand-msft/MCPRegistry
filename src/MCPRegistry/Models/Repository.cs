using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MCPRegistry.Models;

public class Repository
{
    [JsonPropertyName("url")]
    [Required]
    public required string Url { get; set; }

    [JsonPropertyName("source")]
    [Required]
    public required string Source { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("subfolder")]
    public string? Subfolder { get; set; }
}
