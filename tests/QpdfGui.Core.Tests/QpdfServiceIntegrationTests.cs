using QpdfGui.Core.Inspect;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;
using Xunit;

namespace QpdfGui.Core.Tests;

/// <summary>
/// QPDF 核心执行与集成验证测试
/// 依赖 tests/fixtures/three-pages.pdf 真实调用 qpdf.exe 与 QpdfRunner / PdfInspector
/// </summary>
public class QpdfServiceIntegrationTests
{
    private readonly string _qpdfExe;
    private readonly string _samplePdf;
    private readonly QpdfRunner _runner = new();
    private readonly PdfInspector _inspector;

    public QpdfServiceIntegrationTests()
    {
        var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        _qpdfExe = Path.Combine(rootDir, "runtimes", QpdfLocator.GetCurrentRid(), "native", OperatingSystem.IsWindows() ? "qpdf.exe" : "qpdf");
        _samplePdf = Path.Combine(rootDir, "tests", "fixtures", "three-pages.pdf");
        _inspector = new PdfInspector(_qpdfExe);
    }

    [SkippableFact]
    public async Task QpdfLocator_CheckVersion_Succeeds()
    {
        Skip.If(!File.Exists(_qpdfExe), "QPDF 引擎不存在，跳过集成测试");

        var (isValid, version, error) = await QpdfLocator.CheckVersionAsync(_qpdfExe);
        Assert.True(isValid, error);
        Assert.NotNull(version);
        Assert.StartsWith("12.", version);
    }

    [SkippableFact]
    public async Task Encrypt_Decrypt_Flow_Succeeds()
    {
        Skip.If(!File.Exists(_qpdfExe), "QPDF 引擎不存在，跳过集成测试");
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. 探查原文件
            var originalInfo = await _inspector.InspectAsync(_samplePdf);
            Assert.True(originalInfo.IsValid);
            Assert.False(originalInfo.IsEncrypted);
            Assert.Equal(3, originalInfo.PageCount);

            // 2. 提取前 2 页
            var extractedPdf = Path.Combine(tempDir, "extracted.pdf");
            var extractJob = new QpdfJob
            {
                Empty = "",
                OutputFile = extractedPdf,
                Pages = [new PagesSpec { File = _samplePdf, Range = "1-2" }]
            };
            var extractResult = await _runner.RunJobAsync(extractJob, _qpdfExe);
            Assert.True(extractResult.IsCompleted, extractResult.StandardError);

            var extractedInfo = await _inspector.InspectAsync(extractedPdf);
            Assert.Equal(2, extractedInfo.PageCount);

            // 3. 加密文件
            var encryptedPdf = Path.Combine(tempDir, "encrypted.pdf");
            var encJob = new QpdfJob
            {
                InputFile = extractedPdf,
                OutputFile = encryptedPdf,
                Encrypt = new EncryptOptions
                {
                    UserPassword = "mypassword",
                    OwnerPassword = "ownerpwd",
                    Aes256 = new Encrypt256BitOptions
                    {
                        Print = "none"
                    }
                }
            };
            var encResult = await _runner.RunJobAsync(encJob, _qpdfExe);
            Assert.True(encResult.IsCompleted, encResult.StandardError);

            // 探查加密文件
            var encInfoNoPwd = await _inspector.InspectAsync(encryptedPdf);
            Assert.True(encInfoNoPwd.IsEncrypted);
            Assert.True(encInfoNoPwd.RequiresPassword);

            var encInfoWithPwd = await _inspector.InspectAsync(encryptedPdf, "mypassword");
            Assert.Equal(2, encInfoWithPwd.PageCount);

            // 4. 解密文件
            var decryptedPdf = Path.Combine(tempDir, "decrypted.pdf");
            var decJob = new QpdfJob
            {
                InputFile = encryptedPdf,
                OutputFile = decryptedPdf,
                Password = "mypassword",
                Decrypt = ""
            };
            var decResult = await _runner.RunJobAsync(decJob, _qpdfExe);
            Assert.True(decResult.IsCompleted, decResult.StandardError);

            var decInfo = await _inspector.InspectAsync(decryptedPdf);
            Assert.False(decInfo.IsEncrypted);
            Assert.Equal(2, decInfo.PageCount);

            // 5. 旋转页面
            var rotatedPdf = Path.Combine(tempDir, "rotated.pdf");
            var rotJob = new QpdfJob
            {
                InputFile = decryptedPdf,
                OutputFile = rotatedPdf,
                Rotate = ["+90:1-z"]
            };
            var rotResult = await _runner.RunJobAsync(rotJob, _qpdfExe);
            Assert.True(rotResult.IsCompleted, rotResult.StandardError);
            Assert.True(File.Exists(rotatedPdf));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [SkippableFact]
    public async Task MergeAsync_WithEmptyInput_Succeeds()
    {
        Skip.If(!File.Exists(_qpdfExe), "QPDF 引擎不存在，跳过集成测试");
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var outputPath = Path.Combine(tempDir, "merged_output.pdf");
            var job = new QpdfJob
            {
                Empty = "",
                OutputFile = outputPath,
                Pages =
                [
                    new PagesSpec { File = _samplePdf, Range = "1-2" },
                    new PagesSpec { File = _samplePdf, Range = "3" }
                ]
            };

            var result = await _runner.RunJobAsync(job, _qpdfExe);
            Assert.True(result.IsCompleted, result.StandardError);
            Assert.True(File.Exists(outputPath));

            var info = await _inspector.InspectAsync(outputPath);
            Assert.Equal(3, info.PageCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [SkippableFact]
    public async Task SplitByRangesAsync_GeneratesMultipleFiles()
    {
        Skip.If(!File.Exists(_qpdfExe), "QPDF 引擎不存在，跳过集成测试");
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var ranges = new List<string> { "1-1", "2-3" };
            foreach (var r in ranges)
            {
                var outPath = Path.Combine(tempDir, $"split_{r}.pdf");
                var job = new QpdfJob
                {
                    Empty = "",
                    OutputFile = outPath,
                    Pages = [new PagesSpec { File = _samplePdf, Range = r }]
                };
                var res = await _runner.RunJobAsync(job, _qpdfExe);
                Assert.True(res.IsCompleted, res.StandardError);
            }

            var files = Directory.GetFiles(tempDir, "*.pdf");
            Assert.Equal(2, files.Length);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [SkippableFact]
    public async Task EncryptAsync_UserPasswordOnly_Succeeds()
    {
        Skip.If(!File.Exists(_qpdfExe), "QPDF 引擎不存在，跳过集成测试");
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var encryptedPdf = Path.Combine(tempDir, "user_only.pdf");
            var options = new EncryptOptions
            {
                UserPassword = "open123",
                OwnerPassword = null,
                Aes256 = new Encrypt256BitOptions
                {
                    Print = "full"
                }
            }.Normalize();
            Assert.NotNull(options);

            var job = new QpdfJob
            {
                InputFile = _samplePdf,
                OutputFile = encryptedPdf,
                Encrypt = options
            };

            var result = await _runner.RunJobAsync(job, _qpdfExe);

            Assert.True(result.IsCompleted, result.StandardError);
            Assert.True(File.Exists(encryptedPdf));

            var info = await _inspector.InspectAsync(encryptedPdf, "open123");
            Assert.Equal(3, info.PageCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [SkippableFact]
    public async Task RepairAsync_Succeeds_AndCollectsWarnings()
    {
        Skip.If(!File.Exists(_qpdfExe), "QPDF 引擎不存在，跳过集成测试");
        Assert.True(File.Exists(_samplePdf), $"测试固件未找到: {_samplePdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var repairedPdf = Path.Combine(tempDir, "repaired.pdf");
            var job = new QpdfJob
            {
                InputFile = _samplePdf,
                OutputFile = repairedPdf,
                ObjectStreams = "generate"
            };

            var result = await _runner.RunJobAsync(job, _qpdfExe);

            Assert.True(result.IsCompleted, result.StandardError);
            Assert.True(File.Exists(repairedPdf));

            var info = await _inspector.InspectAsync(repairedPdf);
            Assert.Equal(3, info.PageCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [SkippableFact]
    public async Task RepairAsync_DamagedPdf_CollectsRealWarnings()
    {
        Skip.If(!File.Exists(_qpdfExe), "QPDF 引擎不存在，跳过集成测试");
        var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var damagedPdf = Path.Combine(rootDir, "tests", "fixtures", "damaged.pdf");
        Assert.True(File.Exists(damagedPdf), $"损坏测试固件未找到: {damagedPdf}");

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var repairedPdf = Path.Combine(tempDir, "damaged_repaired.pdf");
            var job = new QpdfJob
            {
                InputFile = damagedPdf,
                OutputFile = repairedPdf,
                ObjectStreams = "generate"
            };

            var result = await _runner.RunJobAsync(job, _qpdfExe);

            // 损坏文件修复应当成功（ExitCode 3/0），并抓取到真实 warnings
            Assert.True(result.IsCompleted, result.StandardError);
            Assert.True(File.Exists(repairedPdf));
            Assert.NotEmpty(result.Warnings);
            // 确保 operation succeeded with warnings 总结行已被过滤
            Assert.DoesNotContain(result.Warnings, w => w.Contains("operation succeeded with warnings"));

            var info = await _inspector.InspectAsync(repairedPdf);
            Assert.Equal(3, info.PageCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [SkippableFact]
    public async Task Inspect_EncryptedFixtures_IdentifiesPasswordAndEncryption()
    {
        Skip.If(!File.Exists(_qpdfExe), "QPDF 引擎不存在，跳过集成测试");
        var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var userEncryptedPdf = Path.Combine(rootDir, "tests", "fixtures", "encrypted-user.pdf");
        var ownerOnlyPdf = Path.Combine(rootDir, "tests", "fixtures", "encrypted-owner-only.pdf");
        Assert.True(File.Exists(userEncryptedPdf), $"固件未找到: {userEncryptedPdf}");
        Assert.True(File.Exists(ownerOnlyPdf), $"固件未找到: {ownerOnlyPdf}");

        // 1. user encrypted 需要密码
        var userInfoNoPwd = await _inspector.InspectAsync(userEncryptedPdf);
        Assert.True(userInfoNoPwd.IsEncrypted);
        Assert.True(userInfoNoPwd.RequiresPassword);

        var userInfoWithPwd = await _inspector.InspectAsync(userEncryptedPdf, "user123");
        Assert.Equal(3, userInfoWithPwd.PageCount);

        // 2. owner-only 加密无需用户密码即可打开探查
        var ownerInfo = await _inspector.InspectAsync(ownerOnlyPdf);
        Assert.True(ownerInfo.IsEncrypted);
        Assert.False(ownerInfo.RequiresPassword);
        Assert.Equal(3, ownerInfo.PageCount);
    }
}
