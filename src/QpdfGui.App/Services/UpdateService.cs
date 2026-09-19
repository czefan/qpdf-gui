using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace QpdfGui.App.Services;

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
    Task<(bool HasUpdate, string LatestVersion, string ReleaseUrl, string Message)> CheckAppUpdateAsync();

    /// <summary>
    /// 异步检查官方 QPDF 引擎最新发布版本
    /// </summary>
    Task<(bool HasUpdate, string LatestVersion, string ReleaseUrl, string Message)> CheckQpdfEngineUpdateAsync(string? currentQpdfVersion);

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
        Timeout = TimeSpan.FromSeconds(5)
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
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
        }
    }

    /// <inheritdoc />
    public async Task<(bool HasUpdate, string LatestVersion, string ReleaseUrl, string Message)> CheckAppUpdateAsync()
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

                if (Version.TryParse(latestTag, out var latestVer) && Version.TryParse(currentTag, out var curVer))
                {
                    if (latestVer > curVer)
                    {
                        return (true, release.TagName, release.HtmlUrl, $"发现新版本 {release.TagName}，点击前往下载");
                    }
                }

                return (false, release.TagName, release.HtmlUrl, "当前已是最新版本");
            }
        }
        catch
        {
            // 网络离线或 GitHub API 受限时做优雅兜底
        }

        return (false, CurrentAppVersion, $"{repoUrl}/releases", "已是最新版本或可通过发布页查看");
    }

    /// <inheritdoc />
    public async Task<(bool HasUpdate, string LatestVersion, string ReleaseUrl, string Message)> CheckQpdfEngineUpdateAsync(string? currentQpdfVersion)
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

                if (Version.TryParse(latestTag, out var latestVer) && Version.TryParse(currentClean, out var curVer))
                {
                    if (latestVer > curVer)
                    {
                        return (true, release.TagName, release.HtmlUrl, $"官方最新版本为 {release.TagName}，可前往升级");
                    }
                }

                return (false, release.TagName, release.HtmlUrl, $"官方最新版本 {release.TagName}（当前引擎就绪）");
            }
        }
        catch
        {
            // 离线兜底
        }

        return (false, currentQpdfVersion ?? "未知", qpdfReleasesUrl, "可前往 QPDF 官方发布页查看更新");
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
