using System.Text.RegularExpressions;

namespace QpdfGui.Core.Jobs;

/// <summary>
/// 表示与校验 QPDF 页面范围表达式（如 "1-5", "z", "1-z:even", "r1-r5"）
/// </summary>
public static partial class PageRange
{
    // 单项匹配：单个页码(12, z, r1)、区间(1-5, z-1, 1-z)、带步长修饰(1-z:even, 1-z:odd)
    [GeneratedRegex(@"^([1-9]\d*|z|r[1-9]\d*)(-[1-9]\d*|-z|-r[1-9]\d*)?(:even|:odd)?$", RegexOptions.IgnoreCase)]
    private static partial Regex SingleRangeRegex();

    /// <summary>
    /// 校验范围表达式是否符合 QPDF 语法
    /// </summary>
    /// <param name="expression">用逗号或空格分隔的范围字符串，如 "1-5, 8, z-1:odd"</param>
    /// <param name="errorMessage">错误说明</param>
    /// <returns>是否合法</returns>
    public static bool TryValidate(string? expression, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            errorMessage = "页面范围不能为空";
            return false;
        }

        var parts = expression.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            errorMessage = "页面范围不能为空";
            return false;
        }

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (!SingleRangeRegex().IsMatch(trimmed))
            {
                errorMessage = $"无效的页码范围表达式：'{trimmed}'。支持格式如 1-5, z, 1-z:even, r1。";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// 规整化表达式，去除多余空白并以逗号拼接
    /// </summary>
    public static string Normalize(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "1-z";
        }

        var parts = expression.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(",", parts);
    }
}
