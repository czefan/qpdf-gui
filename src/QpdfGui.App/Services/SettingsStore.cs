using System.Text.Json.Serialization;

namespace QpdfGui.App.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext
{
}

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
/// 采用“便携目录优先”策略：优先加载或保存在可执行文件同级目录；
/// 若当前目录无写权限，则自动平滑回退至系统的 AppData 目录。
/// 杜绝启动时试写探测文件。
/// </summary>
public class SettingsStore
{
    private string? _resolvedPath;

    /// <summary>
    /// 当设置发生变更并保存成功时触发
    /// </summary>
    public event Action<AppSettings>? Changed;

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
        var baseDir = AppContext.BaseDirectory;
        var portablePath = Path.Combine(baseDir, "settings.json");
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "qpdf-gui",
            "settings.json");

        if (File.Exists(portablePath))
        {
            _resolvedPath = portablePath;
        }
        else if (File.Exists(appDataPath))
        {
            _resolvedPath = appDataPath;
        }

        if (_resolvedPath != null && File.Exists(_resolvedPath))
        {
            try
            {
                var json = File.ReadAllText(_resolvedPath);
                Current = (AppSettings?)System.Text.Json.JsonSerializer.Deserialize(
                    json,
                    typeof(AppSettings),
                    SettingsJsonContext.Default) ?? new AppSettings();
                return;
            }
            catch
            {
                // 加载失败时使用默认设置
            }
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
            var targetPath = _resolvedPath;
            if (targetPath == null)
            {
                // 首次保存，优先尝试便携模式
                var baseDir = AppContext.BaseDirectory;
                var portablePath = Path.Combine(baseDir, "settings.json");
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(
                        Current,
                        typeof(AppSettings),
                        SettingsJsonContext.Default);
                    File.WriteAllText(portablePath, json);
                    _resolvedPath = portablePath;
                    Changed?.Invoke(Current);
                    return;
                }
                catch
                {
                    // 若无写入权限，回退到 AppData
                    targetPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "qpdf-gui",
                        "settings.json");
                    _resolvedPath = targetPath;
                }
            }

            var folder = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var serialized = System.Text.Json.JsonSerializer.Serialize(
                Current,
                typeof(AppSettings),
                SettingsJsonContext.Default);
            File.WriteAllText(targetPath, serialized);
            Changed?.Invoke(Current);
        }
        catch
        {
            // 忽略写入失败
        }
    }
}
