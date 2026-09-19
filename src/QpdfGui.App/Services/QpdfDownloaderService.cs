using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using QpdfGui.Core.Process;

namespace QpdfGui.App.Services;

public record GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }
}

public record QpdfReleaseDetails
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; init; } = [];
}

/// <summary>
/// QPDF 原生引擎一键在线下载与解压安装服务接口
/// </summary>
public interface IQpdfDownloaderService
{
    /// <summary>
    /// 从官方 GitHub Releases 下载并自动部署 QPDF 引擎至本地
    /// </summary>
    /// <param name="progress">进度回调：0.0 ~ 1.0 为下载百分比，以及阶段提示信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>成功部署后的 qpdf.exe 绝对路径</returns>
    Task<string> DownloadAndInstallAsync(IProgress<(double Percentage, string StatusMessage)>? progress = null, CancellationToken ct = default);
}

/// <summary>
/// QPDF 原生引擎下载与安装实现
/// </summary>
public class QpdfDownloaderService : IQpdfDownloaderService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    static QpdfDownloaderService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("QpdfGui-Desktop-App/1.0");
    }

    /// <inheritdoc />
    public async Task<string> DownloadAndInstallAsync(IProgress<(double Percentage, string StatusMessage)>? progress = null, CancellationToken ct = default)
    {
        progress?.Report((0, "正在连接 GitHub 获取最新 QPDF 版本信息..."));

        var release = await HttpClient.GetFromJsonAsync<QpdfReleaseDetails>(
            "https://api.github.com/repos/qpdf/qpdf/releases/latest", ct)
            ?? throw new InvalidOperationException("无法获取 QPDF 官方发布版本信息，请检查网络连接。");

        // 定位 Windows x64 二进制发布包（如 qpdf-12.4.1-msvc64.zip）
        var targetAsset = release.Assets.FirstOrDefault(a =>
            a.Name.Contains("msvc64", StringComparison.OrdinalIgnoreCase) &&
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? release.Assets.FirstOrDefault(a => a.Name.Contains("win64", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"未在 QPDF {release.TagName} 中找到适用于 Windows x64 的官方预编译包。");

        progress?.Report((0.05, $"开始下载 {targetAsset.Name} ({targetAsset.Size / (1024 * 1024.0):F1} MB)..."));

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"qpdf-{Guid.NewGuid():N}.zip");
        var tempExtractDir = Path.Combine(Path.GetTempPath(), $"qpdf-{Guid.NewGuid():N}");

        try
        {
            // 带进度的流式下载
            using (var response = await HttpClient.GetAsync(targetAsset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? targetAsset.Size;

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;

                    if (totalBytes > 0)
                    {
                        var pct = 0.05 + ((double)totalRead / totalBytes * 0.75); // 5% ~ 80% 用于下载
                        var mbRead = totalRead / (1024 * 1024.0);
                        var mbTotal = totalBytes / (1024 * 1024.0);
                        progress?.Report((pct, $"正在下载: {mbRead:F1}MB / {mbTotal:F1}MB ({pct:P0})"));
                    }
                }
            }

            // 解压与提纯
            progress?.Report((0.85, "下载完成，正在解压引擎组件..."));
            ZipFile.ExtractToDirectory(tempZipPath, tempExtractDir);

            // 寻找 bin 目录
            var binDir = Directory.GetDirectories(tempExtractDir, "bin", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new DirectoryNotFoundException("解压包中未找到 bin 目录。");

            // 决定目标存放位置：优先尝试当前应用目录下的 runtimes，若无写权限则回退到 LocalAppData
            var rid = QpdfLocator.GetCurrentRid();
            var targetDir = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native");

            bool canWriteToAppDir;
            try
            {
                Directory.CreateDirectory(targetDir);
                var testFile = Path.Combine(targetDir, ".write_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                canWriteToAppDir = true;
            }
            catch
            {
                canWriteToAppDir = false;
            }

            if (!canWriteToAppDir)
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                targetDir = Path.Combine(localAppData, "QpdfGui", "runtimes", rid, "native");
                Directory.CreateDirectory(targetDir);
            }

            progress?.Report((0.92, "正在部署可执行文件及运行库..."));

            // 仅提取 bin 目录下的所有 dll 和 exe
            foreach (var file in Directory.GetFiles(binDir))
            {
                var dest = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            var finalExePath = Path.Combine(targetDir, "qpdf.exe");
            if (!File.Exists(finalExePath))
            {
                throw new FileNotFoundException("部署失败，未找到 qpdf.exe。");
            }

            progress?.Report((1.0, "QPDF 核心引擎部署成功！"));
            return Path.GetFullPath(finalExePath);
        }
        finally
        {
            // 清理临时文件
            try
            {
                if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true);
            }
            catch
            {
                // 忽略临时文件清理异常
            }
        }
    }
}
