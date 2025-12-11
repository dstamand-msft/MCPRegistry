using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MCPRegistry.Models;

public class Icon
{
    [JsonPropertyName("src")]
    [Required]
    [MaxLength(255)]
    public required string Src { get; set; }

    [JsonPropertyName("mimeType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IconMimeType? MimeType { get; set; }

    [JsonPropertyName("sizes")]
    public List<string>? Sizes { get; set; }

    [JsonPropertyName("theme")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IconTheme? Theme { get; set; }
}

public enum IconMimeType
{
    [JsonPropertyName("image/png")]
    ImagePng,
    [JsonPropertyName("image/jpeg")]
    ImageJpeg,
    [JsonPropertyName("image/jpg")]
    ImageJpg,
    [JsonPropertyName("image/svg+xml")]
    ImageSvgXml,
    [JsonPropertyName("image/webp")]
    ImageWebp
}

public enum IconTheme
{
    [JsonPropertyName("light")]
    Light,
    [JsonPropertyName("dark")]
    Dark
}
