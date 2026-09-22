using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QpdfGui.Core.Jobs;

/// <summary>
/// QPDF Job JSON 根模型
/// </summary>
public class QpdfJob
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    [JsonPropertyName("inputFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputFile { get; set; }

    [JsonPropertyName("empty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Empty { get; set; }

    [JsonPropertyName("outputFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputFile { get; set; }

    [JsonPropertyName("password")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }

    [JsonPropertyName("pages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PagesSpec>? Pages { get; set; }

    [JsonPropertyName("splitPages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SplitPages { get; set; }

    [JsonPropertyName("decrypt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Decrypt { get; set; }

    [JsonPropertyName("encrypt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EncryptOptions? Encrypt { get; set; }

    [JsonPropertyName("rotate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Rotate { get; set; }

    [JsonPropertyName("objectStreams")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ObjectStreams { get; set; }

    [JsonPropertyName("linearize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Linearize { get; set; }

    [JsonPropertyName("progress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Progress { get; set; }

    /// <summary>
    /// 将当前 Job 序列化为符合 QPDF Schema 的 JSON 字符串
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, SerializerOptions);
    }
}
