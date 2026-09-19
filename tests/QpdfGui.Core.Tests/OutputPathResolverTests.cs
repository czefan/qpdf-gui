using QpdfGui.Core.Services;
using Xunit;

namespace QpdfGui.Core.Tests;

public class OutputPathResolverTests
{
    [Fact]
    public void ResolveUniquePath_NoConflict_AppendsSuffix()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = OutputPathResolver.ResolveUniquePath(tempDir, "sample.pdf", "merged");
            var expected = Path.Combine(tempDir, "sample_merged.pdf");
            Assert.Equal(expected, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveUniquePath_WithConflict_IncrementsNumber()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var first = Path.Combine(tempDir, "sample_merged.pdf");
            File.WriteAllText(first, "dummy");

            var second = OutputPathResolver.ResolveUniquePath(tempDir, "sample.pdf", "merged");
            var expected = Path.Combine(tempDir, "sample_merged (1).pdf");
            Assert.Equal(expected, second);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveSplitPattern_ContainsPattern()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = OutputPathResolver.ResolveSplitPattern(tempDir, "document.pdf");
            Assert.Contains("%d", result);
            Assert.EndsWith("document_page_%d.pdf", result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
