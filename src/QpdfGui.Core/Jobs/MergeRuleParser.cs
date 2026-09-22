using System.Text.RegularExpressions;

namespace QpdfGui.Core.Jobs;

/// <summary>
/// 表示合并规则中的单个抽取片段
/// </summary>
public record MergeRuleSegment
{
    /// <summary>
    /// 1-based 文件序号（如 1 代表第一个文件）
    /// </summary>
    public int FileIndex { get; init; }

    /// <summary>
    /// 抽取的目标页码范围（如 "1-11", "1", "8-55", "1-z"）
    /// </summary>
    public string Range { get; init; } = "1-z";

    /// <summary>
    /// 友好的人性化描述（用于界面实时预览）
    /// </summary>
    public string DisplayText => Range == "1-z" ? $"[{FileIndex}] All" : $"[{FileIndex}] Pages {Range}";
}

/// <summary>
/// 合并规则语法解析器
/// 支持用户自定义多文件交叉与分段抽取语法（例如 "1.1-1.11, 2.1, 1.8-1.55" 或 "1.1-11, 2.1, 1.8-55"）
/// </summary>
public static class MergeRuleParser
{
    private static readonly char[] Separators = [',', '，', ';', '；'];

    /// <summary>
    /// 解析合并规则表达式
    /// </summary>
    /// <param name="expression">用户输入的表达式</param>
    /// <param name="fileCount">当前文件列表中的总文件数</param>
    /// <param name="segments">解析得到的片段规范列表</param>
    /// <param name="errorMessage">解析失败时的错误说明</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParse(
        string? expression,
        int fileCount,
        out List<MergeRuleSegment> segments,
        out string? errorMessage)
    {
        segments = [];
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            return true;
        }

        if (fileCount <= 0)
        {
            errorMessage = "Please add PDF files first.";
            return false;
        }

        var tokens = expression.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return true;
        }

        foreach (var rawToken in tokens)
        {
            var token = rawToken.Trim();
            if (string.IsNullOrEmpty(token)) continue;

            // 1. 纯数字：如 "1" 或 "2"，表示取该文件的全部页面
            if (int.TryParse(token, out int pureFileIndex))
            {
                if (pureFileIndex < 1 || pureFileIndex > fileCount)
                {
                    errorMessage = $"File index [{pureFileIndex}] out of range (total {fileCount} files).";
                    return false;
                }
                segments.Add(new MergeRuleSegment { FileIndex = pureFileIndex, Range = "1-z" });
                continue;
            }

            // 2. 带点表达式：如 "1.1-1.11", "1.1-11", "2.1", "1.8-1.55", "1.1-z"
            int dotIndex = token.IndexOf('.');
            if (dotIndex <= 0 || dotIndex >= token.Length - 1)
            {
                errorMessage = $"Invalid merge rule syntax: '{token}'. Expected format: 1.1-11, 2.1, 1.8-55";
                return false;
            }

            var fileIndexStr = token[..dotIndex].Trim();
            if (!int.TryParse(fileIndexStr, out int fileIndex) || fileIndex < 1 || fileIndex > fileCount)
            {
                errorMessage = $"File index [{fileIndexStr}] is invalid or out of range (total {fileCount} files).";
                return false;
            }

            var rawRange = token[(dotIndex + 1)..].Trim();
            var normalizedRange = NormalizeRange(fileIndex, rawRange);

            if (!PageRange.TryValidate(normalizedRange, out var pageRangeErr))
            {
                errorMessage = $"Invalid page range '{rawRange}' for file [{fileIndex}]: {pageRangeErr}";
                return false;
            }

            segments.Add(new MergeRuleSegment
            {
                FileIndex = fileIndex,
                Range = normalizedRange
            });
        }

        if (segments.Count == 0)
        {
            errorMessage = "No valid merge rules found.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 规整化页码范围，智能处理 "1.1-1.11" 这类在短横线后重复带有文件编号的习惯写法
    /// </summary>
    private static string NormalizeRange(int fileIndex, string rawRange)
    {
        // 若包含短横线（如 "1-1.11" 或 "8-1.55"）
        int dashIndex = rawRange.IndexOf('-');
        if (dashIndex > 0 && dashIndex < rawRange.Length - 1)
        {
            var left = rawRange[..dashIndex].Trim();
            var right = rawRange[(dashIndex + 1)..].Trim();

            // 若右侧以 "{fileIndex}." 开头（如 "1.11"），则自动剥离重复的文件序号前缀
            var prefix = $"{fileIndex}.";
            if (right.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                right = right[prefix.Length..].Trim();
            }

            return $"{left}-{right}";
        }

        return rawRange;
    }

    /// <summary>
    /// 生成人性化的合并流程预览文本
    /// </summary>
    public static string GeneratePreview(IEnumerable<MergeRuleSegment> segments)
    {
        return string.Join(" ➔ ", segments.Select(s => s.DisplayText));
    }
}
