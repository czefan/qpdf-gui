using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QpdfGui.Core.Jobs;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(QpdfJob))]
internal partial class QpdfJobJsonContext : JsonSerializerContext
{
}

/// <summary>
/// QPDF Job JSON 根模型
/// </summary>
public class QpdfJob
{
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
        return JsonSerializer.Serialize(this, typeof(QpdfJob), QpdfJobJsonContext.Default);
    }

    /// <summary>
    /// 依据当前 Job 的配置生成等效的 qpdf 命令行调用字符串（单一真源）
    /// </summary>
    public string ToCommandLine()
    {
        var parts = new List<string> { "qpdf" };

        if (!string.IsNullOrEmpty(Password))
        {
            parts.Add($"--password={Quote(Password)}");
        }

        if (Linearize != null)
        {
            parts.Add("--linearize");
        }

        if (Decrypt != null)
        {
            parts.Add("--decrypt");
        }

        if (Empty != null)
        {
            parts.Add("--empty");
        }
        else if (!string.IsNullOrEmpty(InputFile))
        {
            parts.Add(Quote(InputFile));
        }

        if (Encrypt != null)
        {
            var norm = Encrypt.Normalize() ?? Encrypt;
            var u = Quote(norm.UserPassword ?? string.Empty);
            var o = Quote(norm.OwnerPassword ?? string.Empty);
            parts.Add($"--encrypt {u} {o} 256");

            if (norm.Aes256 != null)
            {
                if (!string.IsNullOrEmpty(norm.Aes256.Print) && norm.Aes256.Print != "full")
                {
                    parts.Add($"--print={norm.Aes256.Print}");
                }
                if (!string.IsNullOrEmpty(norm.Aes256.Modify) && norm.Aes256.Modify != "all")
                {
                    parts.Add($"--modify={norm.Aes256.Modify}");
                }
                if (norm.Aes256.Extract == "n")
                {
                    parts.Add("--extract=n");
                }
                if (norm.Aes256.Annotate == "n")
                {
                    parts.Add("--annotate=n");
                }
                if (norm.Aes256.Accessibility == "n")
                {
                    parts.Add("--accessibility=n");
                }
                if (!string.IsNullOrEmpty(norm.Aes256.CleartextMetadata))
                {
                    parts.Add("--cleartext-metadata");
                }
            }
            parts.Add("--");
        }

        if (Rotate != null && Rotate.Count > 0)
        {
            foreach (var r in Rotate)
            {
                parts.Add($"--rotate={r}");
            }
        }

        if (Pages != null && Pages.Count > 0)
        {
            parts.Add("--pages");
            foreach (var page in Pages)
            {
                if (!string.IsNullOrEmpty(page.Password))
                {
                    parts.Add($"--password={Quote(page.Password)}");
                }
                parts.Add(Quote(page.File));
                if (!string.IsNullOrEmpty(page.Range))
                {
                    parts.Add(page.Range);
                }
            }
            parts.Add("--");
        }

        if (!string.IsNullOrEmpty(SplitPages))
        {
            parts.Add($"--split-pages={SplitPages}");
        }

        if (!string.IsNullOrEmpty(OutputFile))
        {
            parts.Add(Quote(OutputFile));
        }

        return string.Join(" ", parts);
    }

    private static string Quote(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "\"\"";
        }
        if (text.Contains(' ') || text.Contains('\t') || text.Contains('"'))
        {
            return $"\"{text.Replace("\"", "\\\"")}\"";
        }
        return text;
    }
}
