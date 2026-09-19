using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;

namespace QpdfGui.Core.Process;

/// <summary>
/// QPDF 可执行文件三级查找器
/// </summary>
public static partial class QpdfLocator
{
    [GeneratedRegex(@"qpdf version (\d+\.\d+(\.\d+)?)")]
    private static partial Regex VersionRegex();

    /// <summary>
    /// 获取当前系统的 RID（如 win-x64, linux-x64, osx-arm64）
    /// </summary>
    public static string GetCurrentRid()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64"
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"win-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return $"linux-{arch}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"osx-{arch}";

        return $"win-{arch}";
    }

    /// <summary>
    /// 根据三级优先级查找 QPDF 可执行路径
    /// 1. runtimes/{rid}/native/qpdf[.exe]
    /// 2. 用户自定义路径 customPath
    /// 3. 系统环境变量 PATH
    /// </summary>
    public static string? Locate(string? customPath = null)
    {
        var exeName = OperatingSystem.IsWindows() ? "qpdf.exe" : "qpdf";

        // 1. 自定义优先（若用户显式指定）
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return Path.GetFullPath(customPath);
        }

        // 2. 本地 runtimes 目录
        var baseDir = AppContext.BaseDirectory;
        var rid = GetCurrentRid();
        var localNativePath = Path.Combine(baseDir, "runtimes", rid, "native", exeName);
        if (File.Exists(localNativePath))
        {
            return Path.GetFullPath(localNativePath);
        }

        // 3. 用户 LocalAppData 独立安装目录
        var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppDataDir))
        {
            var appDataNativePath = Path.Combine(localAppDataDir, "QpdfGui", "runtimes", rid, "native", exeName);
            if (File.Exists(appDataNativePath))
            {
                return Path.GetFullPath(appDataNativePath);
            }
        }

        // 备选查找上一层或当前目录
        var sameDirPath = Path.Combine(baseDir, exeName);
        if (File.Exists(sameDirPath))
        {
            return Path.GetFullPath(sameDirPath);
        }

        // 3. 查找环境变量 PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in paths)
            {
                var candidate = Path.Combine(p.Trim(), exeName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 探活并获取 QPDF 版本信息
    /// </summary>
    public static async Task<(bool IsValid, string? Version, string? ErrorMessage)> CheckVersionAsync(string qpdfPath, CancellationToken ct = default)
    {
        if (!File.Exists(qpdfPath))
        {
            return (false, null, $"文件不存在：{qpdfPath}");
        }

        try
        {
            var result = await Cli.Wrap(qpdfPath)
                .WithArguments("--version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct);

            if (result.ExitCode != 0)
            {
                return (false, null, $"执行失败 (ExitCode {result.ExitCode}): {result.StandardError}");
            }

            var match = VersionRegex().Match(result.StandardOutput);
            if (match.Success)
            {
                return (true, match.Groups[1].Value, null);
            }

            return (true, result.StandardOutput.Trim(), null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
