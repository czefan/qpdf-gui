using QpdfGui.Core.Jobs;
using Xunit;

namespace QpdfGui.Core.Tests;

public class EncryptOptionsTests
{
    [Fact]
    public void Normalize_OnlyUserPassword_FallsBackOwnerToUser()
    {
        var options = new EncryptOptions
        {
            UserPassword = "mypassword",
            OwnerPassword = null
        };

        var normalized = options.Normalize();
        Assert.NotNull(normalized);
        Assert.Equal("mypassword", normalized.UserPassword);
        Assert.Equal("mypassword", normalized.OwnerPassword);
    }

    [Fact]
    public void Normalize_BothPasswordsSet_PreservesBoth()
    {
        var options = new EncryptOptions
        {
            UserPassword = "user123",
            OwnerPassword = "owner456"
        };

        var normalized = options.Normalize();
        Assert.NotNull(normalized);
        Assert.Equal("user123", normalized.UserPassword);
        Assert.Equal("owner456", normalized.OwnerPassword);
    }

    [Fact]
    public void Normalize_OnlyOwnerPassword_UserBecomesEmpty()
    {
        var options = new EncryptOptions
        {
            UserPassword = null,
            OwnerPassword = "owner456"
        };

        var normalized = options.Normalize();
        Assert.NotNull(normalized);
        Assert.Equal(string.Empty, normalized.UserPassword);
        Assert.Equal("owner456", normalized.OwnerPassword);
    }

    [Fact]
    public void Normalize_BothEmptyOrNull_ReturnsNull()
    {
        var options1 = new EncryptOptions { UserPassword = null, OwnerPassword = null };
        var options2 = new EncryptOptions { UserPassword = "", OwnerPassword = "  " };

        Assert.Null(options1.Normalize());
        Assert.Null(options2.Normalize());
    }
}
