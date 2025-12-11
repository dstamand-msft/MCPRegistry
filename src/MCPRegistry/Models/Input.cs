using System.Text.Json.Serialization;

namespace MCPRegistry.Models;

public class Input
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; } = false;

    [JsonPropertyName("format")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InputFormat Format { get; set; } = InputFormat.String;

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("isSecret")]
    public bool IsSecret { get; set; } = false;

    [JsonPropertyName("default")]
    public string? Default { get; set; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; set; }

    [JsonPropertyName("choices")]
    public List<string>? Choices { get; set; }
}

public enum InputFormat
{
    [JsonPropertyName("string")]
    String,
    [JsonPropertyName("number")]
    Number,
    [JsonPropertyName("boolean")]
    Boolean,
    [JsonPropertyName("filepath")]
    Filepath
}

public class InputWithVariables : Input
{
    [JsonPropertyName("variables")]
    public Dictionary<string, Input>? Variables { get; set; }
}

public class PositionalArgument : InputWithVariables
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "positional";

    [JsonPropertyName("valueHint")]
    public string? ValueHint { get; set; }

    [JsonPropertyName("isRepeated")]
    public bool IsRepeated { get; set; } = false;
}

public class NamedArgument : InputWithVariables
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "named";

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("isRepeated")]
    public bool IsRepeated { get; set; } = false;
}

public class KeyValueInput : InputWithVariables
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
