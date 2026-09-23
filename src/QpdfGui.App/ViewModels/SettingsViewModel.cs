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
    private readonly IQpdfDownloaderService _downloaderService;

    /// <summary>
    /// 当 QPDF 引擎状态（有效性、版本）发生刷新时的通知回调
    /// </summary>
    public Action? OnQpdfStatusChanged { get; set; }

    /// <summary>
    /// 当界面缩放率改变时的通知回调
    /// </summary>
    public Action<double>? OnUiScaleChanged { get; set; }

    /// <summary>
    /// 是否使用内置引擎模式（true 为内置引擎，只更新自己的；false 为指定路径，由外部独立管理）
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseCustomEngine))]
    [NotifyPropertyChangedFor(nameof(IsUsingCustomPath))]
    [NotifyPropertyChangedFor(nameof(EngineActionButtonText))]
    [NotifyPropertyChangedFor(nameof(CanExecuteEngineAction))]
    [NotifyPropertyChangedFor(nameof(EngineActionDescription))]
    private bool _useBuiltInEngine = true;

    /// <summary>
    /// 是否使用指定路径模式（与 UseBuiltInEngine 互斥）
    /// </summary>
    public bool UseCustomEngine
    {
        get => !UseBuiltInEngine;
        set => UseBuiltInEngine = !value;
    }

    partial void OnUseBuiltInEngineChanged(bool value)
    {
        if (value) CustomQpdfPath = null;
    }

    /// <summary>
    /// 用户配置的手动指定 QPDF 可执行文件路径
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUsingCustomPath))]
    [NotifyPropertyChangedFor(nameof(EngineActionButtonText))]
    [NotifyPropertyChangedFor(nameof(CanExecuteEngineAction))]
    [NotifyPropertyChangedFor(nameof(EngineActionDescription))]
    private string? _customQpdfPath;

    /// <summary>
    /// 是否正使用外部自定义 QPDF 路径（若为 true 则由外部独立管理，软件不自动下载覆盖）
    /// </summary>
    public bool IsUsingCustomPath => !string.IsNullOrWhiteSpace(CustomQpdfPath);

    partial void OnCustomQpdfPathChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && UseBuiltInEngine)
        {
            UseBuiltInEngine = false;
        }

        _settingsStore.Current.CustomQpdfPath = value;
        _settingsStore.Save();
        QpdfLocator.Reset();
        _ = RefreshQpdfStatusAsync();
    }

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
    [NotifyPropertyChangedFor(nameof(IsGeneralSection))]
    [NotifyPropertyChangedFor(nameof(IsAppearanceSection))]
    [NotifyPropertyChangedFor(nameof(IsEngineSection))]
    [NotifyPropertyChangedFor(nameof(IsAboutSection))]
    private SettingsSection _currentSection = SettingsSection.General;

    public bool IsGeneralSection => CurrentSection == SettingsSection.General;
    public bool IsAppearanceSection => CurrentSection == SettingsSection.Appearance;
    public bool IsEngineSection => CurrentSection == SettingsSection.Engine;
    public bool IsAboutSection => CurrentSection == SettingsSection.About;

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
    [NotifyPropertyChangedFor(nameof(AppActionButtonText))]
    [NotifyPropertyChangedFor(nameof(CanExecuteAppAction))]
    private bool _isCheckingAppUpdate;

    /// <summary>
    /// 软件自身更新检查反馈消息
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppStatusDescription))]
    private string? _appUpdateStatusMessage;

    /// <summary>
    /// 客户端状态描述文本：未检查时展示软件简述，检查后展示更新状态反馈
    /// </summary>
    public string AppStatusDescription =>
        !string.IsNullOrWhiteSpace(AppUpdateStatusMessage)
            ? AppUpdateStatusMessage
            : LocalizationManager.GetString("Settings_ClientAppDesc");

    /// <summary>
    /// 最新版本的发布链接
    /// </summary>
    [ObservableProperty]
    private string? _appReleaseUrl;

    /// <summary>
    /// 软件新版本下载直链
    /// </summary>
    [ObservableProperty]
    private string? _appDownloadUrl;

    /// <summary>
    /// 软件新版本压缩包名称
    /// </summary>
    [ObservableProperty]
    private string? _appDownloadFileName;

    /// <summary>
    /// 是否发现软件新版本
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppActionButtonText))]
    [NotifyPropertyChangedFor(nameof(CanExecuteAppAction))]
    private bool _hasAppUpdate;

    /// <summary>
    /// 是否正在下载软件新版
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppActionButtonText))]
    [NotifyPropertyChangedFor(nameof(CanExecuteAppAction))]
    private bool _isDownloadingApp;

    /// <summary>
    /// 软件下载进度（0.0 ~ 1.0）
    /// </summary>
    [ObservableProperty]
    private double _appDownloadProgress;

    /// <summary>
    /// 软件下载状态文本
    /// </summary>
    [ObservableProperty]
    private string? _appDownloadStatusText;

    /// <summary>
    /// 客户端更新操作按钮文案：未检查/最新为“检查更新”，查到新版本为“下载最新版”，下载中为“正在下载...”
    /// </summary>
    public string AppActionButtonText =>
        IsDownloadingApp ? LocalizationManager.GetString("Settings_Downloading") :
        IsCheckingAppUpdate ? LocalizationManager.GetString("Settings_CheckingUpdate") :
        HasAppUpdate ? LocalizationManager.GetString("Settings_DownloadLatest") :
        LocalizationManager.GetString("Settings_CheckUpdate");

    public bool CanExecuteAppAction => !IsCheckingAppUpdate && !IsDownloadingApp;

    /// <summary>
    /// 是否正在检查 QPDF 引擎更新
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EngineActionButtonText))]
    [NotifyPropertyChangedFor(nameof(CanExecuteEngineAction))]
    private bool _isCheckingQpdfUpdate;

    /// <summary>
    /// QPDF 引擎更新检查反馈消息
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EngineActionDescription))]
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
    [NotifyPropertyChangedFor(nameof(EngineActionButtonText))]
    [NotifyPropertyChangedFor(nameof(CanExecuteEngineAction))]
    [NotifyPropertyChangedFor(nameof(EngineActionDescription))]
    private bool _hasQpdfUpdate;

    /// <summary>
    /// 是否正在下载安装 QPDF 引擎
    /// </summary>
    [ObservableProperty]
    private bool _isDownloadingEngine;

    /// <summary>
    /// 引擎下载百分比（0.0 ~ 1.0）
    /// </summary>
    [ObservableProperty]
    private double _engineDownloadProgress;

    /// <summary>
    /// 引擎下载过程中的状态提示文本
    /// </summary>
    [ObservableProperty]
    private string? _engineDownloadStatusText;

    /// <summary>
    /// 引擎操作按钮文案：未安装为“一键下载”，已安装为“检查更新”，有新版为“下载最新版”
    /// </summary>
    public string EngineActionButtonText =>
        IsDownloadingEngine ? LocalizationManager.GetString("Settings_Downloading") :
        IsCheckingQpdfUpdate ? LocalizationManager.GetString("Settings_CheckingUpdate") :
        !IsQpdfValid ? LocalizationManager.GetString("Banner_DownloadAction") :
        HasQpdfUpdate ? LocalizationManager.GetString("Settings_DownloadLatest") :
        LocalizationManager.GetString("Settings_CheckUpdate");

    public bool CanExecuteEngineAction => !IsCheckingQpdfUpdate && !IsDownloadingEngine;

    /// <summary>
    /// 引擎当前状态与动作提示说明
    /// </summary>
    public string EngineActionDescription =>
        IsDownloadingEngine ? (EngineDownloadStatusText ?? LocalizationManager.GetString("Settings_Downloading")) :
        !IsQpdfValid ? LocalizationManager.GetString("Settings_EngineNotInstalled") :
        !string.IsNullOrWhiteSpace(QpdfUpdateStatusMessage) ? QpdfUpdateStatusMessage :
        LocalizationManager.GetString("Settings_EngineReadyDesc");

    public EngineStatusViewModel EngineStatus { get; }

    public SettingsViewModel(
        SettingsStore settingsStore,
        IDialogService dialogService,
        EngineStatusViewModel? engineStatus = null,
        IUpdateService? updateService = null,
        IQpdfDownloaderService? downloaderService = null)
    {
        _settingsStore = settingsStore;
        _dialogService = dialogService;
        _downloaderService = downloaderService ?? new QpdfDownloaderService();
        EngineStatus = engineStatus ?? new EngineStatusViewModel(_settingsStore, _dialogService, _downloaderService);
        _updateService = updateService ?? new UpdateService();

        _customQpdfPath = _settingsStore.Current.CustomQpdfPath;
        _useBuiltInEngine = string.IsNullOrWhiteSpace(_customQpdfPath);
        _defaultOutputDirectory = _settingsStore.Current.DefaultOutputDirectory;
        _useCustomOutputDir = !string.IsNullOrWhiteSpace(_defaultOutputDirectory);

        // 联动 EngineStatus
        EngineStatus.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EngineStatusViewModel.IsQpdfValid))
            {
                IsQpdfValid = EngineStatus.IsQpdfValid;
                OnPropertyChanged(nameof(EngineActionButtonText));
                OnPropertyChanged(nameof(EngineActionDescription));
                OnQpdfStatusChanged?.Invoke();
            }
            else if (e.PropertyName == nameof(EngineStatusViewModel.QpdfVersion))
            {
                QpdfVersion = EngineStatus.QpdfVersion;
                OnPropertyChanged(nameof(EngineActionDescription));
            }
            else if (e.PropertyName == nameof(EngineStatusViewModel.ResolvedQpdfPath))
            {
                ResolvedQpdfPath = EngineStatus.ResolvedQpdfPath;
            }
            else if (e.PropertyName == nameof(EngineStatusViewModel.IsDownloading))
            {
                IsDownloadingEngine = EngineStatus.IsDownloading;
                OnPropertyChanged(nameof(EngineActionButtonText));
                OnPropertyChanged(nameof(CanExecuteEngineAction));
                OnPropertyChanged(nameof(EngineActionDescription));
            }
            else if (e.PropertyName == nameof(EngineStatusViewModel.DownloadProgress))
            {
                EngineDownloadProgress = EngineStatus.DownloadProgress;
            }
            else if (e.PropertyName == nameof(EngineStatusViewModel.DownloadStatusText))
            {
                EngineDownloadStatusText = EngineStatus.DownloadStatusText;
                OnPropertyChanged(nameof(EngineActionDescription));
            }
        };

        IsQpdfValid = EngineStatus.IsQpdfValid;
        QpdfVersion = EngineStatus.QpdfVersion;
        ResolvedQpdfPath = EngineStatus.ResolvedQpdfPath;

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
            OnPropertyChanged(nameof(AppActionButtonText));
            OnPropertyChanged(nameof(AppStatusDescription));
            OnPropertyChanged(nameof(EngineActionButtonText));
            OnPropertyChanged(nameof(EngineActionDescription));
        };
        foreach (var theme in AvailableThemes)
        {
            theme.RefreshLabel();
        }
    }

    public async Task RefreshQpdfStatusAsync()
    {
        await EngineStatus.RefreshAsync();
    }

    /// <summary>
    /// 客户端更新单一流转主命令：若有新版本则下载安装，否则触发检查更新
    /// </summary>
    [RelayCommand]
    public async Task ExecuteAppActionAsync()
    {
        if (!CanExecuteAppAction) return;
        if (HasAppUpdate)
        {
            await DownloadAndApplyAppUpdateAsync();
        }
        else
        {
            await CheckAppUpdateAsync();
        }
    }

    /// <summary>
    /// 内置引擎单一流转主命令：若未安装或有新版本则下载安装，否则触发检查更新
    /// </summary>
    [RelayCommand]
    public async Task ExecuteEngineActionAsync()
    {
        if (!CanExecuteEngineAction) return;
        if (!IsQpdfValid || HasQpdfUpdate)
        {
            await DownloadAndInstallEngineAsync();
        }
        else
        {
            await CheckQpdfUpdateAsync();
        }
    }

    /// <summary>
    /// 一键在临时沙箱中静默下载、解压并自动重启应用更新，零残留
    /// </summary>
    [RelayCommand]
    public async Task DownloadAndApplyAppUpdateAsync()
    {
        if (IsDownloadingApp) return;

        if (string.IsNullOrWhiteSpace(AppDownloadUrl))
        {
            OpenAppReleasePage();
            return;
        }

        IsDownloadingApp = true;
        AppDownloadProgress = 0.0;
        AppDownloadStatusText = LocalizationManager.GetString("Settings_Downloading");
        string? extractedDir = null;

        try
        {
            var progress = new Progress<double>(p =>
            {
                AppDownloadProgress = p;
                AppDownloadStatusText = $"{LocalizationManager.GetString("Settings_Downloading")} {p:P0}";
            });

            extractedDir = await _updateService.DownloadAndExtractAppUpdateAsync(AppDownloadUrl, progress);
            AppDownloadStatusText = LocalizationManager.GetString("Update_ReadyTitle");

            var confirm = await _dialogService.ConfirmAsync(
                LocalizationManager.GetString("Update_ReadyTitle"),
                LocalizationManager.GetString("Update_ReadyMessage"));

            if (confirm)
            {
                _updateService.ApplyUpdateAndRestart(extractedDir);
            }
            else
            {
                _updateService.CleanupUpdateSandbox(extractedDir);
                AppDownloadStatusText = LocalizationManager.GetString("Update_Canceled");
            }
        }
        catch (Exception ex)
        {
            AppDownloadStatusText = $"{LocalizationManager.GetString("Update_Failed")}: {ex.Message}";
            if (extractedDir != null)
            {
                _updateService.CleanupUpdateSandbox(extractedDir);
            }
        }
        finally
        {
            IsDownloadingApp = false;
        }
    }

    /// <summary>
    /// 异步检查 QPDF GUI 自身更新
    /// </summary>
    [RelayCommand]
    public async Task CheckAppUpdateAsync()
    {
        if (IsCheckingAppUpdate) return;
        IsCheckingAppUpdate = true;
        AppUpdateStatusMessage = LocalizationManager.GetString("Settings_CheckingUpdate");
        try
        {
            var result = await _updateService.CheckAppUpdateAsync();
            HasAppUpdate = result.HasUpdate;
            AppReleaseUrl = result.ReleaseUrl;
            AppDownloadUrl = result.DownloadUrl;
            AppDownloadFileName = result.AssetName;
            AppUpdateStatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            AppUpdateStatusMessage = $"{LocalizationManager.GetString("Update_Failed")}: {ex.Message}";
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
        QpdfUpdateStatusMessage = LocalizationManager.GetString("Settings_CheckingUpdate");
        try
        {
            var result = await _updateService.CheckQpdfEngineUpdateAsync(QpdfVersion);
            HasQpdfUpdate = result.HasUpdate;
            QpdfReleaseUrl = result.ReleaseUrl;
            QpdfUpdateStatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            QpdfUpdateStatusMessage = $"{LocalizationManager.GetString("Update_Failed")}: {ex.Message}";
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
    /// 在默认浏览器中打开 MIT 开源许可协议页面
    /// </summary>
    [RelayCommand]
    public void OpenLicensePage()
    {
        _updateService.OpenBrowser("https://github.com/czefan/qpdf-gui/blob/main/LICENSE");
    }

    /// <summary>
    /// 弹出文件选择对话框供用户手动指定外部 qpdf.exe
    /// </summary>
    [RelayCommand]
    public async Task BrowseCustomQpdfPath()
    {
        var exe = await _dialogService.OpenFileAsync(LocalizationManager.GetString("Settings_CustomPath"), ["qpdf.exe", "qpdf", "*"]);
        if (!string.IsNullOrWhiteSpace(exe))
        {
            UseBuiltInEngine = false;
            CustomQpdfPath = exe;
        }
    }

    /// <summary>
    /// 清除用户自定义的 QPDF 路径，重置为自动寻找本地内置或系统 PATH
    /// </summary>
    [RelayCommand]
    public void ResetQpdfPath()
    {
        UseBuiltInEngine = true;
    }

    /// <summary>
    /// 在线下载并配置/升级内置 QPDF 核心引擎
    /// </summary>
    [RelayCommand]
    public async Task DownloadAndInstallEngineAsync()
    {
        await EngineStatus.DownloadEngine();
        HasQpdfUpdate = false;
        QpdfUpdateStatusMessage = null;
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
