using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;
using QpdfGui.Core.Services;
using Xunit;

namespace QpdfGui.Core.Tests;

public class QpdfServiceIntegrationTests
{
    private readonly string _qpdfExe;
    private readonly QpdfService _service;

    public QpdfServiceIntegrationTests()
    {
        // 查找工程根目录下的 runtimes/win-x64/native/qpdf.exe
        var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        _qpdfExe = Path.Combine(rootDir, "runtimes", QpdfLocator.GetCurrentRid(), "native", OperatingSystem.IsWindows() ? "qpdf.exe" : "qpdf");
        _service = new QpdfService(new QpdfRunner(), () => _qpdfExe);
    }

    [Fact]
    public async Task QpdfLocator_CheckVersion_Succeeds()
    {
        if (!File.Exists(_qpdfExe))
        {
            // 环境未预装本地 qpdf 引擎时跳过集成验证
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
        if (!File.Exists(_qpdfExe))
        {
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. 查找 sample PDF
            var rootDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            var samplePdf = Path.Combine(rootDir, "scratch", "qpdf-12.4.1-msvc64", "share", "doc", "qpdf", "qpdf-manual.pdf");
            if (!File.Exists(samplePdf))
            {
                // 若未找到 manual 则跳过
                return;
            }

            // 2. 探查原文件
            var originalInfo = await _service.InspectAsync(samplePdf);
            Assert.True(originalInfo.IsValid);
            Assert.False(originalInfo.IsEncrypted);
            Assert.True(originalInfo.PageCount > 0);

            // 3. 提取前 2 页
            var extractedPdf = Path.Combine(tempDir, "extracted.pdf");
            var extractResult = await _service.ExtractAsync(samplePdf, extractedPdf, "1-2");
            Assert.True(extractResult.IsCompleted, extractResult.StandardError);

            var extractedInfo = await _service.InspectAsync(extractedPdf);
            Assert.Equal(2, extractedInfo.PageCount);

            // 4. 加密该文件
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

            // 探查加密文件（无密码应提示需要密码）
            var encInfoNoPwd = await _service.InspectAsync(encryptedPdf);
            Assert.True(encInfoNoPwd.IsEncrypted);
            Assert.True(encInfoNoPwd.RequiresPassword);

            // 带密码探查应返回正确页数
            var encInfoWithPwd = await _service.InspectAsync(encryptedPdf, "mypassword");
            Assert.Equal(2, encInfoWithPwd.PageCount);

            // 5. 解密文件
            var decryptedPdf = Path.Combine(tempDir, "decrypted.pdf");
            var decResult = await _service.DecryptAsync(encryptedPdf, decryptedPdf, "mypassword");
            Assert.True(decResult.IsCompleted, decResult.StandardError);

            var decInfo = await _service.InspectAsync(decryptedPdf);
            Assert.False(decInfo.IsEncrypted);
            Assert.Equal(2, decInfo.PageCount);

            // 6. 旋转页面
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
}
