using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace QpdfGui.App.Services;

/// <summary>
/// GitHub Releases 资源文件数据模型
/// </summary>
public record GitHubAssetInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }
}

/// <summary>
/// GitHub Releases 响应数据模型
/// </summary>
public record GitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("assets")]
    public List<GitHubAssetInfo>? Assets { get; init; }
}

/// <summary>
/// 客户端更新检查结果模型
/// </summary>
public record AppUpdateCheckResult
{
    public bool HasUpdate { get; init; }
    public string LatestVersion { get; init; } = string.Empty;
    public string ReleaseUrl { get; init; } = string.Empty;
    public string? DownloadUrl { get; init; }
    public string? AssetName { get; init; }
    public long AssetSize { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// QPDF 引擎更新检查结果模型
/// </summary>
public record EngineUpdateCheckResult
{
    public bool HasUpdate { get; init; }
    public string LatestVersion { get; init; } = string.Empty;
    public string ReleaseUrl { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 聚合更新检查结果模型
/// </summary>
public record UnifiedUpdateCheckResult
{
    public required AppUpdateCheckResult App { get; init; }
    public required EngineUpdateCheckResult Engine { get; init; }
}

/// <summary>
/// 软件及 QPDF 引擎版本检测与在线更新服务接口
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// 获取当前 QPDF GUI 应用编译版本号
    /// </summary>
    string CurrentAppVersion { get; }

    /// <summary>
    /// 异步检查 QPDF GUI 自身更新
    /// </summary>
    Task<AppUpdateCheckResult> CheckAppUpdateAsync();

    /// <summary>
    /// 异步检查官方 QPDF 引擎最新发布版本
    /// </summary>
    Task<EngineUpdateCheckResult> CheckQpdfEngineUpdateAsync(string? currentQpdfVersion);

    /// <summary>
    /// 并发检查客户端与引擎的全部更新
    /// </summary>
    Task<UnifiedUpdateCheckResult> CheckAllUpdatesAsync(string? currentQpdfVersion);

    /// <summary>
    /// 在临时安全沙箱中静默下载并解压客户端新版，绝不在用户个人目录产生任何残留
    /// </summary>
    Task<string> DownloadAndExtractAppUpdateAsync(string downloadUrl, IProgress<double> progress, CancellationToken ct = default);

    /// <summary>
    /// 启动无残留置换脚本并重启应用程序
    /// </summary>
    void ApplyUpdateAndRestart(string extractedDir);

    /// <summary>
    /// 清理临时下载解压沙箱目录
    /// </summary>
    void CleanupUpdateSandbox(string? extractedDir);

    /// <summary>
    /// 在默认系统浏览器中打开指定链接
    /// </summary>
    void OpenBrowser(string url);
}

/// <summary>
/// 轻量级在线更新与版本检测实现
/// </summary>
public class UpdateService : IUpdateService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    static UpdateService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("QpdfGui-Desktop-App/1.0");
    }

    /// <inheritdoc />
    public string CurrentAppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v0.2.0";
        }
    }

    /// <inheritdoc />
    public async Task<AppUpdateCheckResult> CheckAppUpdateAsync()
    {
        const string repoUrl = "https://github.com/czefan/qpdf-gui";
        try
        {
            var release = await HttpClient.GetFromJsonAsync<GitHubReleaseInfo>(
                "https://api.github.com/repos/czefan/qpdf-gui/releases/latest");

            if (release != null && !string.IsNullOrWhiteSpace(release.TagName))
            {
                var latestTag = release.TagName.TrimStart('v', 'V');
                var currentTag = CurrentAppVersion.TrimStart('v', 'V');

                // 匹配最适合的 win-x64 免装版 asset
                var matchedAsset = release.Assets?.FirstOrDefault(a => a.Name.Contains("win-x64-self-contained.zip", StringComparison.OrdinalIgnoreCase))
                                   ?? release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                var downloadUrl = matchedAsset?.BrowserDownloadUrl;
                var assetName = matchedAsset?.Name;
                var assetSize = matchedAsset?.Size ?? 0L;

                if (Version.TryParse(latestTag, out var latestVer) && Version.TryParse(currentTag, out var curVer))
                {
                    if (latestVer > curVer)
                    {
                        return new AppUpdateCheckResult
                        {
                            HasUpdate = true,
                            LatestVersion = release.TagName,
                            ReleaseUrl = release.HtmlUrl,
                            DownloadUrl = downloadUrl,
                            AssetName = assetName,
                            AssetSize = assetSize,
                            Message = $"发现新版本 {release.TagName}"
                        };
                    }
                }

                return new AppUpdateCheckResult
                {
                    HasUpdate = false,
                    LatestVersion = release.TagName,
                    ReleaseUrl = release.HtmlUrl,
                    DownloadUrl = downloadUrl,
                    AssetName = assetName,
                    AssetSize = assetSize,
                    Message = "当前已是最新版本"
                };
            }
        }
        catch
        {
            // 网络离线或 GitHub API 受限时做优雅兜底
        }

        return new AppUpdateCheckResult
        {
            HasUpdate = false,
            LatestVersion = CurrentAppVersion,
            ReleaseUrl = $"{repoUrl}/releases",
            Message = "已是最新版本或可通过发布页查看"
        };
    }

    /// <inheritdoc />
    public async Task<EngineUpdateCheckResult> CheckQpdfEngineUpdateAsync(string? currentQpdfVersion)
    {
        const string qpdfReleasesUrl = "https://github.com/qpdf/qpdf/releases";
        try
        {
            var release = await HttpClient.GetFromJsonAsync<GitHubReleaseInfo>(
                "https://api.github.com/repos/qpdf/qpdf/releases/latest");

            if (release != null && !string.IsNullOrWhiteSpace(release.TagName))
            {
                var latestTag = release.TagName.TrimStart('v', 'V');
                var currentClean = (currentQpdfVersion ?? string.Empty).Trim().TrimStart('v', 'V');

                if (string.IsNullOrWhiteSpace(currentClean))
                {
                    return new EngineUpdateCheckResult
                    {
                        HasUpdate = true,
                        LatestVersion = release.TagName,
                        ReleaseUrl = release.HtmlUrl,
                        Message = $"官方最新版本为 {release.TagName}（未检测到本地引擎）"
                    };
                }

                if (Version.TryParse(latestTag, out var latestVer) && Version.TryParse(currentClean, out var curVer))
                {
                    if (latestVer > curVer)
                    {
                        return new EngineUpdateCheckResult
                        {
                            HasUpdate = true,
                            LatestVersion = release.TagName,
                            ReleaseUrl = release.HtmlUrl,
                            Message = $"官方最新版本为 {release.TagName}，可升级"
                        };
                    }
                }

                return new EngineUpdateCheckResult
                {
                    HasUpdate = false,
                    LatestVersion = release.TagName,
                    ReleaseUrl = release.HtmlUrl,
                    Message = $"当前已是官方最新版本 ({release.TagName})"
                };
            }
        }
        catch
        {
            // 离线兜底
        }

        return new EngineUpdateCheckResult
        {
            HasUpdate = false,
            LatestVersion = currentQpdfVersion ?? "未知",
            ReleaseUrl = qpdfReleasesUrl,
            Message = "可前往 QPDF 官方发布页查看更新"
        };
    }

    /// <inheritdoc />
    public async Task<UnifiedUpdateCheckResult> CheckAllUpdatesAsync(string? currentQpdfVersion)
    {
        var appTask = CheckAppUpdateAsync();
        var engineTask = CheckQpdfEngineUpdateAsync(currentQpdfVersion);
        await Task.WhenAll(appTask, engineTask);

        return new UnifiedUpdateCheckResult
        {
            App = await appTask,
            Engine = await engineTask
        };
    }

    /// <inheritdoc />
    public async Task<string> DownloadAndExtractAppUpdateAsync(string downloadUrl, IProgress<double> progress, CancellationToken ct = default)
    {
        var tempSandbox = Path.Combine(Path.GetTempPath(), $"QpdfGui_Update_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempSandbox);
        var zipPath = Path.Combine(tempSandbox, "update.zip");
        var extractPath = Path.Combine(tempSandbox, "extracted");

        try
        {
            using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;
                    if (totalBytes > 0)
                    {
                        progress.Report(Math.Clamp((double)totalRead / totalBytes, 0.0, 1.0));
                    }
                }
            }

            // 解压到临时 extracted 目录
            ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            // 解压完毕立刻删除下载的压缩包，减少空间占用
            try { File.Delete(zipPath); } catch { }

            return extractPath;
        }
        catch
        {
            // 失败时自清理沙箱
            CleanupUpdateSandbox(extractPath);
            throw;
        }
    }

    /// <inheritdoc />
    public void ApplyUpdateAndRestart(string extractedDir)
    {
        var currentExe = Environment.ProcessPath;
        var targetDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
        {
            currentExe = Path.Combine(targetDir, "QpdfGui.exe");
        }

        var sandboxDir = Directory.GetParent(extractedDir)?.FullName ?? extractedDir;
        var pid = Environment.ProcessId;

        // Windows 平台标准无残留静默置换脚本：
        // 1. 等待主进程完全退出；
        // 2. 将临时目录文件覆盖到程序根目录；
        // 3. 彻底删除临时沙箱目录；
        // 4. 启动新版主程序。
        var script = $"""
            Wait-Process -Id {pid} -Timeout 15 -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500
            Copy-Item -Path '{extractedDir}\*' -Destination '{targetDir}' -Recurse -Force
            Remove-Item -Path '{sandboxDir}' -Recurse -Force -ErrorAction SilentlyContinue
            Start-Process -FilePath '{currentExe}'
        """;

        var encodedCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -WindowStyle Hidden -EncodedCommand {encodedCommand}",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(psi);
        Environment.Exit(0);
    }

    /// <inheritdoc />
    public void CleanupUpdateSandbox(string? extractedDir)
    {
        if (string.IsNullOrWhiteSpace(extractedDir)) return;

        try
        {
            var sandboxDir = Directory.GetParent(extractedDir)?.FullName ?? extractedDir;
            if (Directory.Exists(sandboxDir))
            {
                Directory.Delete(sandboxDir, true);
            }
        }
        catch
        {
            // 忽略清理临时文件的非关键异常
        }
    }

    /// <inheritdoc />
    public void OpenBrowser(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // 忽略非标准环境启动失败
        }
    }
}
