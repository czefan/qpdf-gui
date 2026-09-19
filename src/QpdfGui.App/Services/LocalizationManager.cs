using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Controls;

namespace QpdfGui.App.Services;

/// <summary>
/// 多语言本地化管理服务
/// 负责在运行时通过动态挂载/卸载 Avalonia ResourceDictionary 实现界面文本的即时无缝切换。
/// </summary>
public static class LocalizationManager
{
    private static ResourceDictionary? _currentStringDictionary;

    /// <summary>
    /// 当前生效的语言代码（如 "zh-CN" 或 "en-US"）
    /// </summary>
    public static string CurrentLanguage { get; private set; } = "zh-CN";

    /// <summary>
    /// 当界面语言发生改变时触发的事件
    /// </summary>
    public static event Action? LanguageChanged;

    /// <summary>
    /// 应用指定的语言代码，动态加载对应的 XAML 字符串资源字典并替换当前资源
    /// </summary>
    /// <param name="languageCode">语言代码，如 "zh-CN"、"en-US"</param>
    public static void ApplyLanguage(string languageCode)
    {
        CurrentLanguage = languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN";

        if (Application.Current == null) return;

        var assemblyName = typeof(LocalizationManager).Assembly.GetName().Name;
        var uri = new Uri($"avares://{assemblyName}/Resources/Strings.{CurrentLanguage}.axaml");
        var newDict = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);

        // 卸载先前载入的语言字典，避免内存泄漏与键冲突
        if (_currentStringDictionary != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_currentStringDictionary);
        }

        Application.Current.Resources.MergedDictionaries.Add(newDict);
        _currentStringDictionary = newDict;

        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// 从全局资源中获取指定键对应的本地化字符串
    /// </summary>
    /// <param name="key">资源键名，例如 "Status_Success"</param>
    /// <param name="fallback">若未找到对应键时返回的回退文本；若留空则直接返回键名本身</param>
    /// <returns>解析出的本地化文本</returns>
    public static string GetString(string key, string fallback = "")
    {
        if (Application.Current?.TryFindResource(key, out var res) == true && res is string str)
        {
            return str;
        }
        return fallback.Length > 0 ? fallback : key;
    }
}
