using QpdfGui.App.Services;
using Xunit;

namespace QpdfGui.Core.Tests;

public class UpdateServiceTests
{
    [Fact]
    public void CurrentAppVersion_ReturnsValidSemanticVersion()
    {
        var service = new UpdateService();
        var version = service.CurrentAppVersion;

        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.StartsWith("v", version);
    }

    [Fact]
    public void CleanupUpdateSandbox_WithNullOrInvalidPath_DoesNotThrow()
    {
        var service = new UpdateService();

        // 验证空路径与非法路径安全容错
        service.CleanupUpdateSandbox(null);
        service.CleanupUpdateSandbox(string.Empty);
        service.CleanupUpdateSandbox("C:\\non_existent_sandbox_path_12345");
    }

    [Fact]
    public void CleanupUpdateSandbox_WithRealDirectory_DeletesParentSandbox()
    {
        var service = new UpdateService();
        var tempSandbox = Path.Combine(Path.GetTempPath(), $"QpdfGui_Test_Sandbox_{Guid.NewGuid():N}");
        var extractedDir = Path.Combine(tempSandbox, "extracted");
        Directory.CreateDirectory(extractedDir);

        Assert.True(Directory.Exists(tempSandbox));

        service.CleanupUpdateSandbox(extractedDir);

        Assert.False(Directory.Exists(tempSandbox));
    }
}
