using System.Text.Json;

namespace QpdfGui.App.Services;

/// <summary>
/// 应用程序全局用户偏好配置实体
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 用户自定义指定的 QPDF 可执行文件路径；若为空则自动通过环境变量 PATH 或本地 runtimes 目录探查
    /// </summary>
    public string? CustomQpdfPath { get; set; }

    /// <summary>
    /// 自定义默认输出目录；若为空则默认保存至源 PDF 文件同级目录
    /// </summary>
    public string? DefaultOutputDirectory { get; set; }

    /// <summary>
    /// 界面色彩主题变体："Default"（跟随系统）、"Light"（浅色模式）、"Dark"（深色模式）
    /// </summary>
    public string ThemeVariant { get; set; } = "Default";

    /// <summary>
    /// 界面语言代码："zh-CN"（简体中文）、"en-US"（English）
    /// </summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>
    /// 界面缩放倍率（1.0 = 100%, 1.25 = 125%, 1.5 = 150% 等）
    /// </summary>
    public double UiScale { get; set; } = 1.0;
}

/// <summary>
/// 负责将 <see cref="AppSettings"/> 序列化并持久化存储的服务。
/// 采用“便携目录优先”策略：优先将 settings.json 存放在可执行文件同级目录；
/// 若当前目录只读（如位于受限系统目录），则自动平滑回退至系统的 AppData 目录。
/// </summary>
public class SettingsStore
{
    private static readonly string SettingsFilePath = ResolveSettingsFilePath();

    private static string ResolveSettingsFilePath()
    {
        // 1. 优先尝试便携模式：应用程序所在根目录
        var baseDir = AppContext.BaseDirectory;
        var portablePath = Path.Combine(baseDir, "settings.json");

        // 如果便携配置已经存在，或者当前目录具有写权限，直接使用便携路径
        if (File.Exists(portablePath))
        {
            return portablePath;
        }

        try
        {
            var testFile = Path.Combine(baseDir, ".write_test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return portablePath;
        }
        catch
        {
            // 当前目录没有写权限，回退到系统 AppData 目录
        }

        // 2. 回退模式：%APPDATA%/qpdf-gui/settings.json
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "qpdf-gui");

        return Path.Combine(appDataFolder, "settings.json");
    }

    /// <summary>
    /// 当前生效的应用程序配置实例
    /// </summary>
    public AppSettings Current { get; private set; } = new();

    public SettingsStore()
    {
        Load();
    }

    /// <summary>
    /// 从磁盘 JSON 文件反序列化加载配置；若文件损坏或不存在则回退至默认配置
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                return;
            }
        }
        catch
        {
            // 加载失败时使用默认设置
        }

        Current = new AppSettings();
    }

    /// <summary>
    /// 将当前配置以格式化 JSON 持久化写入磁盘
    /// </summary>
    public void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // 忽略写入失败
        }
    }
}
