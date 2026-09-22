using System.Diagnostics;

namespace QpdfGui.App.Services;

/// <summary>
/// 系统原生 Shell 操作封装（打开文件、在文件夹中定位、打开超链接）
/// </summary>
public static class Shell
{
    /// <summary>
    /// 使用系统默认关联程序打开指定文件
    /// </summary>
    public static void OpenFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", filePath);
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", filePath);
            }
        }
        catch { /* 忽略打开失败 */ }
    }

    /// <summary>
    /// 打开文件管理器并高亮选中该文件（或打开所在目录）
    /// </summary>
    public static void RevealInFolder(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (Directory.Exists(filePath))
                {
                    Process.Start("explorer.exe", $"\"{filePath}\"");
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", $"-R \"{filePath}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                var dir = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Process.Start("xdg-open", dir);
                }
            }
        }
        catch { /* 忽略异常 */ }
    }

    /// <summary>
    /// 在默认浏览器中打开指定超链接
    /// </summary>
    public static void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { /* 忽略异常 */ }
    }
}
