using QpdfGui.Core.Jobs;
using Xunit;

namespace QpdfGui.Core.Tests;

public class MergeRuleParserTests
{
    [Fact]
    public void TryParse_UserScenario_ParsesSuccessfully()
    {
        // 测试用户提出的核心语法："1.1-1.11, 2.1, 1.8-1.55"
        var expr = "1.1-1.11, 2.1, 1.8-1.55";
        var success = MergeRuleParser.TryParse(expr, 3, out var segments, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(3, segments.Count);

        // 第 1 段：文件 1，页码 1-11
        Assert.Equal(1, segments[0].FileIndex);
        Assert.Equal("1-11", segments[0].Range);

        // 第 2 段：文件 2，单页 1
        Assert.Equal(2, segments[1].FileIndex);
        Assert.Equal("1", segments[1].Range);

        // 第 3 段：文件 1，页码 8-55
        Assert.Equal(1, segments[2].FileIndex);
        Assert.Equal("8-55", segments[2].Range);

        // 预览文本校验
        var preview = MergeRuleParser.GeneratePreview(segments);
        Assert.Equal("[1] 第 1-11 页 ➔ [2] 第 1 页 ➔ [1] 第 8-55 页", preview);
    }

    [Fact]
    public void TryParse_StandardSyntax_ParsesSuccessfully()
    {
        // 测试常见写法："1.1-11，2.1；1.8-55"
        var expr = "1.1-11，2.1；1.8-55";
        var success = MergeRuleParser.TryParse(expr, 2, out var segments, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(3, segments.Count);
        Assert.Equal("1-11", segments[0].Range);
        Assert.Equal("1", segments[1].Range);
        Assert.Equal("8-55", segments[2].Range);
    }

    [Fact]
    public void TryParse_WholeDocument_ParsesSuccessfully()
    {
        // 测试整篇文档合并："1, 2.1-z, 1"
        var expr = "1, 2.1-z, 1";
        var success = MergeRuleParser.TryParse(expr, 2, out var segments, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(3, segments.Count);
        Assert.Equal("1-z", segments[0].Range);
        Assert.Equal("1-z", segments[1].Range);
        Assert.Equal("1-z", segments[2].Range);
    }

    [Fact]
    public void TryParse_EmptyExpression_ReturnsEmptySuccess()
    {
        var success = MergeRuleParser.TryParse("", 2, out var segments, out var error);
        Assert.True(success);
        Assert.Empty(segments);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_FileIndexOutOfRange_FailsWithDescriptiveError()
    {
        // 仅有 2 个文件，却请求了第 3 个文件
        var success = MergeRuleParser.TryParse("3.1-5", 2, out var segments, out var error);
        Assert.False(success);
        Assert.Contains("当前仅有 2 个文件", error);
    }

    [Fact]
    public void TryParse_InvalidRange_FailsWithError()
    {
        // 范围非法
        var success = MergeRuleParser.TryParse("1.abc", 2, out var segments, out var error);
        Assert.False(success);
        Assert.Contains("不合法", error);
    }
}
