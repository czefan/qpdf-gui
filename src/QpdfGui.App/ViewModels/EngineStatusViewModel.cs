using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Process;

namespace QpdfGui.App.ViewModels;

/// <summary>
/// 负责 QPDF 引擎检测状态、下载进度及顶部引导横幅的核心 ViewModel（横幅与设置页共用）
/// </summary>
public partial class EngineStatusViewModel : ViewModelBase
{
    private readonly SettingsStore _settingsStore;
    private readonly IDialogService _dialogService;
    private readonly IQpdfDownloaderService _downloaderService;

    /// <summary>
    /// QPDF 引擎是否已就绪且可用
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEngineBanner))]
    private bool _isQpdfValid;

    /// <summary>
    /// 当前检测到的 QPDF 引擎版本号字符串
    /// </summary>
    [ObservableProperty]
    private string? _qpdfVersion;

    /// <summary>
    /// 定位到的 QPDF 引擎物理绝对路径
    /// </summary>
    [ObservableProperty]
    private string? _resolvedQpdfPath;

    /// <summary>
    /// 引擎探测错误说明
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 用户是否在本次会话中手动关闭了提示横幅
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEngineBanner))]
    private bool _isBannerDismissed;

    /// <summary>
    /// 是否正在下载或安装 QPDF 引擎
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEngineBanner))]
    private bool _isDownloading;

    /// <summary>
    /// 下载进度百分比（0.0 ~ 1.0）
    /// </summary>
    [ObservableProperty]
    private double _downloadProgress;

    /// <summary>
    /// 下载状态提示文本
    /// </summary>
    [ObservableProperty]
    private string? _downloadStatusText;

    /// <summary>
    /// 是否展示顶部引导横幅（引擎缺失且未关闭，或处于下载中）
    /// </summary>
    public bool ShowEngineBanner => (!IsQpdfValid && !IsBannerDismissed) || IsDownloading;

    public EngineStatusViewModel(
        SettingsStore settingsStore,
        IDialogService dialogService,
        IQpdfDownloaderService downloaderService)
    {
        _settingsStore = settingsStore;
        _dialogService = dialogService;
        _downloaderService = downloaderService;
    }

    /// <summary>
    /// 刷新引擎探测状态
    /// </summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var info = await QpdfLocator.ProbeAsync(_settingsStore.Current.CustomQpdfPath, ct);
        IsQpdfValid = info.IsValid;
        QpdfVersion = info.Version;
        ResolvedQpdfPath = info.ExePath;
        ErrorMessage = info.ErrorMessage;
    }

    /// <summary>
    /// 手动关闭引导横幅
    /// </summary>
    [RelayCommand]
    public void DismissBanner()
    {
        IsBannerDismissed = true;
    }

    /// <summary>
    /// 浏览选取自定义 QPDF 执行程序路径
    /// </summary>
    [RelayCommand]
    public async Task BrowseEnginePath()
    {
        var selected = await _dialogService.OpenFileAsync(LocalizationManager.GetString("Settings_CustomPath"));
        if (!string.IsNullOrEmpty(selected))
        {
            _settingsStore.Current.CustomQpdfPath = selected;
            _settingsStore.Save();
            QpdfLocator.Reset();
            await RefreshAsync();
        }
    }

    /// <summary>
    /// 一键在线下载并安装 QPDF 核心引擎
    /// </summary>
    [RelayCommand]
    public async Task DownloadEngine()
    {
        if (IsDownloading) return;

        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatusText = LocalizationManager.GetString("Status_Processing");

        try
        {
            var progress = new Progress<(double Percentage, string StatusMessage)>(report =>
            {
                DownloadProgress = report.Percentage;
                DownloadStatusText = report.StatusMessage;
            });

            var exePath = await _downloaderService.DownloadAndInstallAsync(progress);
            if (!string.IsNullOrEmpty(exePath))
            {
                _settingsStore.Current.CustomQpdfPath = null;
                _settingsStore.Save();
                QpdfLocator.Reset();
                await RefreshAsync();
                DownloadStatusText = LocalizationManager.GetString("Status_Success");
            }
            else
            {
                DownloadStatusText = LocalizationManager.GetString("Status_Failed");
            }
        }
        catch (Exception ex)
        {
            DownloadStatusText = ex.Message;
        }
        finally
        {
            await Task.Delay(1500);
            IsDownloading = false;
        }
    }
}
