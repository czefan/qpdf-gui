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

    /// <summary>
    /// 标准化加密选项：
    /// 1. 若 OwnerPassword 为空且 UserPassword 非空，将 OwnerPassword 回退为与 UserPassword 相同；
    /// 2. 若两者皆为空白，返回 null，表示无有效密码不可执行；
    /// 3. 返回规范化后的新配置实例。
    /// </summary>
    public EncryptOptions? Normalize()
    {
        var u = UserPassword ?? string.Empty;
        var o = !string.IsNullOrWhiteSpace(OwnerPassword) ? OwnerPassword : u;

        if (string.IsNullOrWhiteSpace(u) && string.IsNullOrWhiteSpace(o))
        {
            return null;
        }

        return this with
        {
            UserPassword = u,
            OwnerPassword = o
        };
    }
}
