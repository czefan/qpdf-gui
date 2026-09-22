using QpdfGui.Core.Process;
using Xunit;

namespace QpdfGui.Core.Tests;

public class StderrParserTests
{
    [Theory]
    [InlineData("WARNING: file.pdf: file is damaged", "file is damaged")]
    [InlineData("WARNING: C:\\sample\\doc.pdf (offset 634): xref not found", "(offset 634) xref not found")]
    [InlineData("qpdf: WARNING: test.pdf: some warning text", "some warning text")]
    [InlineData("WARNING: pure warning without path colon", "pure warning without path colon")]
    public void ExtractWarning_ValidWarning_ExtractsAndCleans(string input, string expected)
    {
        var result = QpdfRunner.ExtractWarning(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("qpdf: operation succeeded with warnings")]
    [InlineData("qpdf: operation succeeded with warnings; resulting file may have some problems")]
    [InlineData("Normal output line")]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractWarning_SummaryOrNormalLine_ReturnsNull(string input)
    {
        var result = QpdfRunner.ExtractWarning(input);
        Assert.Null(result);
    }
}
