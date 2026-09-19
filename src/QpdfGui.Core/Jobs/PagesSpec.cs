using System.Text.Json.Serialization;

namespace QpdfGui.Core.Jobs;

/// <summary>
/// QPDF Job JSON 中 pages 数组的一项规范
/// </summary>
public record PagesSpec
{
    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("range")]
    public string Range { get; init; } = "1-z";

    [JsonPropertyName("password")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; init; }
}
