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
/// 负责将 <see cref="AppSettings"/> 序列化并持久化存储至 AppData 目录的存储服务
/// </summary>
public class SettingsStore
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "qpdf-gui");

    private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

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
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
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
