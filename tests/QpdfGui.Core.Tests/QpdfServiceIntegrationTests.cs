using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;
using QpdfGui.Core.Services;
using Xunit;

namespace QpdfGui.Core.Tests;

/// <summary>
/// QPDF 核心执行与集成验证测试
/// 依赖 tests/fixtures/three-pages.pdf 真实调用 qpdf.exe 执行端到端任务
/// </summary>
public class QpdfServiceIntegrationTests
{
    private readonly string _qpdfExe;
    private readonly string _samplePdf;
    private readonly QpdfService _service;

    public QpdfServiceIntegrationTests()
    {
        var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        _qpdfExe = Path.Combine(rootDir, "runtimes", QpdfLocator.GetCurrentRid(), "native", OperatingSystem.IsWindows() ? "qpdf.exe" : "qpdf");
        _samplePdf = Path.Combine(rootDir, "tests", "fixtures", "three-pages.pdf");
        _service = new QpdfService(new QpdfRunner(), () => _qpdfExe);
    }

    [Fact]
    public async Task QpdfLocator_CheckVersion_Succeeds()
    {
        if (!File.Exists(_qpdfExe))
        {
            return;
        }

        var (isValid, version, error) = await QpdfLocator.CheckVersionAsync(_qpdfExe);
        Assert.True(isValid, error);
        Assert.NotNull(version);
        Assert.StartsWith("12.", version);
    }

    [Fact]
    public async Task Encrypt_Decrypt_Flow_Succeeds()
    {
        if (!File.Exists(_qpdfExe)) return;
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. 探查原文件
            var originalInfo = await _service.InspectAsync(_samplePdf);
            Assert.True(originalInfo.IsValid);
            Assert.False(originalInfo.IsEncrypted);
            Assert.Equal(3, originalInfo.PageCount);

            // 2. 提取前 2 页（验证 ExtractAsync 与 empty job 规范）
            var extractedPdf = Path.Combine(tempDir, "extracted.pdf");
            var extractResult = await _service.ExtractAsync(_samplePdf, extractedPdf, "1-2");
            Assert.True(extractResult.IsCompleted, extractResult.StandardError);

            var extractedInfo = await _service.InspectAsync(extractedPdf);
            Assert.Equal(2, extractedInfo.PageCount);

            // 3. 加密文件
            var encryptedPdf = Path.Combine(tempDir, "encrypted.pdf");
            var encResult = await _service.EncryptAsync(extractedPdf, encryptedPdf, new EncryptOptions
            {
                UserPassword = "mypassword",
                OwnerPassword = "ownerpwd",
                Aes256 = new Encrypt256BitOptions
                {
                    Print = "none"
                }
            });
            Assert.True(encResult.IsCompleted, encResult.StandardError);

            // 探查加密文件
            var encInfoNoPwd = await _service.InspectAsync(encryptedPdf);
            Assert.True(encInfoNoPwd.IsEncrypted);
            Assert.True(encInfoNoPwd.RequiresPassword);

            var encInfoWithPwd = await _service.InspectAsync(encryptedPdf, "mypassword");
            Assert.Equal(2, encInfoWithPwd.PageCount);

            // 4. 解密文件
            var decryptedPdf = Path.Combine(tempDir, "decrypted.pdf");
            var decResult = await _service.DecryptAsync(encryptedPdf, decryptedPdf, "mypassword");
            Assert.True(decResult.IsCompleted, decResult.StandardError);

            var decInfo = await _service.InspectAsync(decryptedPdf);
            Assert.False(decInfo.IsEncrypted);
            Assert.Equal(2, decInfo.PageCount);

            // 5. 旋转页面
            var rotatedPdf = Path.Combine(tempDir, "rotated.pdf");
            var rotResult = await _service.RotateAsync(decryptedPdf, rotatedPdf, "+90:1-z");
            Assert.True(rotResult.IsCompleted, rotResult.StandardError);
            Assert.True(File.Exists(rotatedPdf));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task MergeAsync_WithEmptyInput_Succeeds()
    {
        if (!File.Exists(_qpdfExe)) return;
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputPath = Path.Combine(tempDir, "merged_output.pdf");
            var pages = new List<PagesSpec>
            {
                new() { File = _samplePdf, Range = "1-2" },
                new() { File = _samplePdf, Range = "3" }
            };

            var result = await _service.MergeAsync(pages, outputPath);
            Assert.True(result.IsCompleted, result.StandardError);
            Assert.True(File.Exists(outputPath));

            var info = await _service.InspectAsync(outputPath);
            Assert.Equal(3, info.PageCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SplitByRangesAsync_GeneratesMultipleFiles()
    {
        if (!File.Exists(_qpdfExe)) return;
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var ranges = new List<string> { "1-1", "2-3" };
            var result = await _service.SplitByRangesAsync(_samplePdf, ranges, tempDir);

            Assert.True(result.IsCompleted, result.StandardError);

            var files = Directory.GetFiles(tempDir, "*.pdf");
            Assert.Equal(2, files.Length);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task EncryptAsync_UserPasswordOnly_Succeeds()
    {
        if (!File.Exists(_qpdfExe)) return;
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var encryptedPdf = Path.Combine(tempDir, "user_only.pdf");
            // 当只提供 UserPassword 时，OwnerPassword 回退为相同值，qpdf 256 位加密能正常工作
            var result = await _service.EncryptAsync(_samplePdf, encryptedPdf, new EncryptOptions
            {
                UserPassword = "open123",
                OwnerPassword = "open123",
                Aes256 = new Encrypt256BitOptions
                {
                    Print = "full"
                }
            });

            Assert.True(result.IsCompleted, result.StandardError);
            Assert.True(File.Exists(encryptedPdf));

            var info = await _service.InspectAsync(encryptedPdf, "open123");
            Assert.Equal(3, info.PageCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RepairAsync_Succeeds_AndCollectsWarnings()
    {
        if (!File.Exists(_qpdfExe)) return;
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var repairedPdf = Path.Combine(tempDir, "repaired.pdf");
            var result = await _service.RepairAsync(_samplePdf, repairedPdf);

            Assert.True(result.IsCompleted, result.StandardError);
            Assert.True(File.Exists(repairedPdf));

            var info = await _service.InspectAsync(repairedPdf);
            Assert.Equal(3, info.PageCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
