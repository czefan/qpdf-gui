using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Process;

namespace QpdfGui.App.ViewModels;

/// <summary>
/// 主题选项实体，支持跟随当前语言动态本地化
/// </summary>
public partial class ThemeOption : ObservableObject
{
    private readonly string _defaultLabel;
    public string ResourceKey { get; }
    public string Value { get; }

    [ObservableProperty]
    private string _label = string.Empty;

    public ThemeOption(string resourceKey, string value, string defaultLabel)
    {
        _defaultLabel = defaultLabel;
        ResourceKey = resourceKey;
        Value = value;
        _label = LocalizationManager.GetString(resourceKey, defaultLabel);
    }

    public void RefreshLabel()
    {
        Label = LocalizationManager.GetString(ResourceKey, _defaultLabel);
    }
}

/// <summary>
/// 界面语言选项实体
/// </summary>
/// <param name="DisplayName">界面显示名称（如 "简体中文", "English"）</param>
/// <param name="Code">区域语言标识符（"zh-CN", "en-US"）</param>
public record LanguageOption(string DisplayName, string Code);

/// <summary>
/// UI 缩放比例选项实体
/// </summary>
/// <param name="Label">显示文本（如 "100%", "140%"）</param>
/// <param name="Scale">缩放系数（如 1.0, 1.25）</param>
public record UiScaleOption(string Label, double Scale);

/// <summary>
/// 设置二级分类
/// </summary>
public enum SettingsSection
{
    /// <summary>
    /// 常规应用设置与输出保存策略
    /// </summary>
    General,

    /// <summary>
    /// 外观界面、语言与缩放
    /// </summary>
    Appearance,

    /// <summary>
    /// QPDF 引擎状态与版本更新
    /// </summary>
    Engine,

    /// <summary>
    /// 软件版本、检查更新与关于
    /// </summary>
    About
}

/// <summary>
/// 应用程序设置与首选项 ViewModel
/// 管理底层 QPDF 运行引擎检测与自定义路径、默认保存目录规则、语言和外观主题及界面缩放
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsStore _settingsStore;
    private readonly IDialogService _dialogService;
    private readonly IUpdateService _updateService;

    /// <summary>
    /// 当 QPDF 引擎状态（有效性、版本）发生刷新时的通知回调
    /// </summary>
    public Action? OnQpdfStatusChanged { get; set; }

    /// <summary>
    /// 当界面缩放率改变时的通知回调
    /// </summary>
    public Action<double>? OnUiScaleChanged { get; set; }

    /// <summary>
    /// 用户配置的手动指定 QPDF 可执行文件路径
    /// </summary>
    [ObservableProperty]
    private string? _customQpdfPath;

    /// <summary>
    /// 系统探查定位到的最终 QPDF 可执行文件真实物理路径
    /// </summary>
    [ObservableProperty]
    private string? _resolvedQpdfPath;

    /// <summary>
    /// 检测到的 QPDF 引擎版本号字符串（如 "v12.4.1"）
    /// </summary>
    [ObservableProperty]
    private string? _qpdfVersion;

    /// <summary>
    /// QPDF 引擎是否就绪且版本可用
    /// </summary>
    [ObservableProperty]
    private bool _isQpdfValid;

    /// <summary>
    /// 用户设置的全局默认输出目录；若为空表示保存至输入源文件同级目录
    /// </summary>
    [ObservableProperty]
    private string? _defaultOutputDirectory;

    /// <summary>
    /// 是否启用了自定义输出目录单选模式
    /// </summary>
    [ObservableProperty]
    private bool _useCustomOutputDir;

    /// <summary>
    /// 当前选中的界面语言
    /// </summary>
    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    /// <summary>
    /// 当前选中的色彩主题变体
    /// </summary>
    [ObservableProperty]
    private ThemeOption _selectedTheme;

    /// <summary>
    /// 当前选中的界面缩放率
    /// </summary>
    [ObservableProperty]
    private UiScaleOption _selectedScale;

    /// <summary>
    /// 可供选择的语言列表
    /// </summary>
    public LanguageOption[] AvailableLanguages { get; } =
    [
        new("简体中文", "zh-CN"),
        new("English", "en-US")
    ];

    /// <summary>
    /// 可供选择的色彩主题列表
    /// </summary>
    public ThemeOption[] AvailableThemes { get; } =
    [
        new("Settings_ThemeDefault", "Default", "跟随系统"),
        new("Settings_ThemeLight", "Light", "浅色模式"),
        new("Settings_ThemeDark", "Dark", "深色模式")
    ];

    /// <summary>
    /// 可供选择的界面缩放档位列表（75% ~ 175%）
    /// </summary>
    public UiScaleOption[] AvailableScales { get; } =
    [
        new("75%", 0.75),
        new("90%", 0.9),
        new("100%", 1.0),
        new("110%", 1.1),
        new("125%", 1.25),
        new("140%", 1.4),
        new("150%", 1.5),
        new("175%", 1.75)
    ];

    /// <summary>
    /// 当前选中的设置二级分类
    /// </summary>
    [ObservableProperty]
    private SettingsSection _currentSection = SettingsSection.General;

    public bool IsGeneralSection => CurrentSection == SettingsSection.General;
    public bool IsAppearanceSection => CurrentSection == SettingsSection.Appearance;
    public bool IsEngineSection => CurrentSection == SettingsSection.Engine;
    public bool IsAboutSection => CurrentSection == SettingsSection.About;

    partial void OnCurrentSectionChanged(SettingsSection value)
    {
        OnPropertyChanged(nameof(IsGeneralSection));
        OnPropertyChanged(nameof(IsAppearanceSection));
        OnPropertyChanged(nameof(IsEngineSection));
        OnPropertyChanged(nameof(IsAboutSection));
    }

    /// <summary>
    /// 切换二级设置分类
    /// </summary>
    [RelayCommand]
    public void SelectSection(string sectionName)
    {
        if (Enum.TryParse<SettingsSection>(sectionName, true, out var sec))
        {
            CurrentSection = sec;
        }
    }

    /// <summary>
    /// 应用程序当前编译版本号
    /// </summary>
    public string AppVersion => _updateService.CurrentAppVersion;

    /// <summary>
    /// 是否正在检查软件自身更新
    /// </summary>
    [ObservableProperty]
    private bool _isCheckingAppUpdate;

    /// <summary>
    /// 软件自身更新检查反馈消息
    /// </summary>
    [ObservableProperty]
    private string? _appUpdateStatusMessage;

    /// <summary>
    /// 最新版本的发布链接
    /// </summary>
    [ObservableProperty]
    private string? _appReleaseUrl;

    /// <summary>
    /// 是否发现软件新版本
    /// </summary>
    [ObservableProperty]
    private bool _hasAppUpdate;

    /// <summary>
    /// 是否正在检查 QPDF 引擎更新
    /// </summary>
    [ObservableProperty]
    private bool _isCheckingQpdfUpdate;

    /// <summary>
    /// QPDF 引擎更新检查反馈消息
    /// </summary>
    [ObservableProperty]
    private string? _qpdfUpdateStatusMessage;

    /// <summary>
    /// QPDF 官方发布链接
    /// </summary>
    [ObservableProperty]
    private string? _qpdfReleaseUrl;

    /// <summary>
    /// 是否发现 QPDF 引擎新版本
    /// </summary>
    [ObservableProperty]
    private bool _hasQpdfUpdate;

    public SettingsViewModel(
        SettingsStore settingsStore,
        IDialogService dialogService,
        IUpdateService? updateService = null)
    {
        _settingsStore = settingsStore;
        _dialogService = dialogService;
        _updateService = updateService ?? new UpdateService();

        _customQpdfPath = _settingsStore.Current.CustomQpdfPath;
        _defaultOutputDirectory = _settingsStore.Current.DefaultOutputDirectory;
        _useCustomOutputDir = !string.IsNullOrWhiteSpace(_defaultOutputDirectory);

        var currentThemeVal = _settingsStore.Current.ThemeVariant;
        _selectedTheme = AvailableThemes.FirstOrDefault(t => t.Value.Equals(currentThemeVal, StringComparison.OrdinalIgnoreCase)) ?? AvailableThemes[0];

        var currentLangVal = _settingsStore.Current.Language;
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code.Equals(currentLangVal, StringComparison.OrdinalIgnoreCase)) ?? AvailableLanguages[0];

        var currentScaleVal = _settingsStore.Current.UiScale <= 0 ? 1.0 : _settingsStore.Current.UiScale;
        _selectedScale = AvailableScales.FirstOrDefault(s => Math.Abs(s.Scale - currentScaleVal) < 0.01) ?? AvailableScales[0];

        // 语言变更时联动刷新主题选项名称（避免中英文混合）
        LocalizationManager.LanguageChanged += () =>
        {
            foreach (var theme in AvailableThemes)
            {
                theme.RefreshLabel();
            }
        };
        foreach (var theme in AvailableThemes)
        {
            theme.RefreshLabel();
        }

        _ = RefreshQpdfStatusAsync();
    }

    /// <summary>
    /// 异步检查 QPDF GUI 自身更新
    /// </summary>
    [RelayCommand]
    public async Task CheckAppUpdateAsync()
    {
        if (IsCheckingAppUpdate) return;
        IsCheckingAppUpdate = true;
        AppUpdateStatusMessage = "正在检查最新版本...";
        try
        {
            var (hasUpdate, _, url, msg) = await _updateService.CheckAppUpdateAsync();
            HasAppUpdate = hasUpdate;
            AppReleaseUrl = url;
            AppUpdateStatusMessage = msg;
        }
        catch (Exception ex)
        {
            AppUpdateStatusMessage = $"检查失败: {ex.Message}";
        }
        finally
        {
            IsCheckingAppUpdate = false;
        }
    }

    /// <summary>
    /// 异步检查 QPDF 官方引擎最新版本
    /// </summary>
    [RelayCommand]
    public async Task CheckQpdfUpdateAsync()
    {
        if (IsCheckingQpdfUpdate) return;
        IsCheckingQpdfUpdate = true;
        QpdfUpdateStatusMessage = "正在检查 QPDF 官方发布...";
        try
        {
            var (hasUpdate, _, url, msg) = await _updateService.CheckQpdfEngineUpdateAsync(QpdfVersion);
            HasQpdfUpdate = hasUpdate;
            QpdfReleaseUrl = url;
            QpdfUpdateStatusMessage = msg;
        }
        catch (Exception ex)
        {
            QpdfUpdateStatusMessage = $"检查失败: {ex.Message}";
        }
        finally
        {
            IsCheckingQpdfUpdate = false;
        }
    }

    /// <summary>
    /// 在默认浏览器中打开软件 Releases 页面
    /// </summary>
    [RelayCommand]
    public void OpenAppReleasePage()
    {
        _updateService.OpenBrowser(AppReleaseUrl ?? "https://github.com/czefan/qpdf-gui/releases");
    }

    /// <summary>
    /// 在默认浏览器中打开 QPDF 官方发布页面
    /// </summary>
    [RelayCommand]
    public void OpenQpdfReleasePage()
    {
        _updateService.OpenBrowser(QpdfReleaseUrl ?? "https://github.com/qpdf/qpdf/releases");
    }

    /// <summary>
    /// 在默认浏览器中打开项目开源主页
    /// </summary>
    [RelayCommand]
    public void OpenProjectHomePage()
    {
        _updateService.OpenBrowser("https://github.com/czefan/qpdf-gui");
    }

    /// <summary>
    /// 异步刷新 QPDF 引擎的探测路径与版本可用性
    /// </summary>
    public async Task RefreshQpdfStatusAsync()
    {
        var resolved = QpdfLocator.Locate(CustomQpdfPath);
        ResolvedQpdfPath = resolved;

        if (!string.IsNullOrWhiteSpace(resolved))
        {
            var (valid, ver, _) = await QpdfLocator.CheckVersionAsync(resolved);
            IsQpdfValid = valid;
            QpdfVersion = valid ? $"v{ver}" : "无效版本";
        }
        else
        {
            IsQpdfValid = false;
            QpdfVersion = "未检测到";
        }

        OnQpdfStatusChanged?.Invoke();
    }

    /// <summary>
    /// 弹出文件选择对话框供用户手动指定外部 qpdf.exe
    /// </summary>
    [RelayCommand]
    public async Task BrowseCustomQpdfPath()
    {
        var exe = await _dialogService.OpenFileAsync("选择 QPDF 可执行文件", ["qpdf.exe", "qpdf", "*"]);
        if (!string.IsNullOrWhiteSpace(exe))
        {
            CustomQpdfPath = exe;
            _settingsStore.Current.CustomQpdfPath = exe;
            _settingsStore.Save();
            await RefreshQpdfStatusAsync();
        }
    }

    /// <summary>
    /// 清除用户自定义的 QPDF 路径，重置为自动寻找本地内置或系统 PATH
    /// </summary>
    [RelayCommand]
    public async Task ResetQpdfPath()
    {
        CustomQpdfPath = null;
        _settingsStore.Current.CustomQpdfPath = null;
        _settingsStore.Save();
        await RefreshQpdfStatusAsync();
    }

    /// <summary>
    /// 浏览并指定保存文件的默认输出目录
    /// </summary>
    [RelayCommand]
    public async Task BrowseOutputDirectory()
    {
        var folder = await _dialogService.SelectFolderAsync("选择默认输出文件夹", DefaultOutputDirectory);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            DefaultOutputDirectory = folder;
            UseCustomOutputDir = true;
            _settingsStore.Current.DefaultOutputDirectory = folder;
            _settingsStore.Save();
        }
    }

    /// <summary>
    /// 恢复默认保存策略：保存在源文件同级目录
    /// </summary>
    [RelayCommand]
    public void SetSaveAlongsideSource()
    {
        UseCustomOutputDir = false;
        DefaultOutputDirectory = null;
        _settingsStore.Current.DefaultOutputDirectory = null;
        _settingsStore.Save();
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value.Code != _settingsStore.Current.Language)
        {
            _settingsStore.Current.Language = value.Code;
            _settingsStore.Save();
            LocalizationManager.ApplyLanguage(value.Code);
        }
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        if (!value.Value.Equals(_settingsStore.Current.ThemeVariant, StringComparison.OrdinalIgnoreCase))
        {
            _settingsStore.Current.ThemeVariant = value.Value;
            _settingsStore.Save();
            ApplyTheme(value.Value);
        }
    }

    partial void OnSelectedScaleChanged(UiScaleOption value)
    {
        if (Math.Abs(value.Scale - _settingsStore.Current.UiScale) > 0.01)
        {
            _settingsStore.Current.UiScale = value.Scale;
            _settingsStore.Save();
            OnUiScaleChanged?.Invoke(value.Scale);
        }
    }

    /// <summary>
    /// 静态辅助方法：将主题字符串应用到 Avalonia 应用程序实例
    /// </summary>
    /// <param name="themeVariant">"Light"、"Dark" 或 "Default"</param>
    public static void ApplyTheme(string themeVariant)
    {
        if (Application.Current == null) return;

        Application.Current.RequestedThemeVariant = themeVariant.ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
