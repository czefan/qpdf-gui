using System.Text.Json.Serialization;

namespace QpdfGui.Core.Jobs;

/// <summary>
/// 256 位 AES 加密选项配置
/// </summary>
public record Encrypt256BitOptions
{
    [JsonPropertyName("print")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Print { get; init; } = "full"; // "full", "low", "none"

    [JsonPropertyName("modify")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Modify { get; init; } = "all"; // "all", "annotate", "form", "assembly", "none"

    [JsonPropertyName("extract")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Extract { get; init; } = "y"; // "y", "n"

    [JsonPropertyName("annotate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Annotate { get; init; } = "y"; // "y", "n"

    [JsonPropertyName("accessibility")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Accessibility { get; init; } = "y";

    [JsonPropertyName("cleartextMetadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CleartextMetadata { get; init; }

    [JsonPropertyName("allowInsecure")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AllowInsecure { get; init; }
}

/// <summary>
/// QPDF Job JSON 中 encrypt 对象
/// </summary>
public record EncryptOptions
{
    [JsonPropertyName("userPassword")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserPassword { get; init; }

    [JsonPropertyName("ownerPassword")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OwnerPassword { get; init; }

    [JsonPropertyName("256bit")]
    public Encrypt256BitOptions Aes256 { get; init; } = new();
}
