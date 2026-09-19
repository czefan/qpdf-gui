using QpdfGui.Core.Jobs;
using Xunit;

namespace QpdfGui.Core.Tests;

public class PageRangeTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("1-5")]
    [InlineData("z")]
    [InlineData("z-1")]
    [InlineData("1-z")]
    [InlineData("1-z:even")]
    [InlineData("1-z:odd")]
    [InlineData("r1")]
    [InlineData("r1-r5")]
    [InlineData("1-5, 8, z-1")]
    [InlineData("1,2,3,4,5")]
    public void TryValidate_ValidRanges_ReturnsTrue(string range)
    {
        var isValid = PageRange.TryValidate(range, out var error);
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("1-")]
    [InlineData("-5")]
    [InlineData("1-z:invalid")]
    [InlineData("1-5, error")]
    public void TryValidate_InvalidRanges_ReturnsFalse(string range)
    {
        var isValid = PageRange.TryValidate(range, out var error);
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void Normalize_CleansWhitespaceAndFormats()
    {
        var result = PageRange.Normalize(" 1-5,   8,  z-1 ");
        Assert.Equal("1-5,8,z-1", result);
    }

    [Fact]
    public void Normalize_Empty_DefaultsToOneToZ()
    {
        var result = PageRange.Normalize("  ");
        Assert.Equal("1-z", result);
    }
}
