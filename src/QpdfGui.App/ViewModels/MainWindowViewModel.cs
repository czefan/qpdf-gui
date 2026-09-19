using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using QpdfGui.App.Services;
using QpdfGui.App.ViewModels.Tools;

namespace QpdfGui.App.ViewModels;

/// <summary>
/// 侧边栏导航条目项，封装图标、本地化资源键以及对应的页面 ViewModel
/// </summary>
public partial class NavigationItem : ObservableObject
{
    /// <summary>
    /// 本地化文本的资源键名（如 "Nav_Merge"）
    /// </summary>
    public string TitleResourceKey { get; }

    /// <summary>
    /// Fluent 图标标识
    /// </summary>
    public Symbol Symbol { get; }

    /// <summary>
    /// 导航项关联的目标功能页面 ViewModel
    /// </summary>
    public ViewModelBase ViewModel { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    public NavigationItem(string titleResourceKey, Symbol symbol, ViewModelBase viewModel)
    {
        TitleResourceKey = titleResourceKey;
        Symbol = symbol;
        ViewModel = viewModel;
        RefreshTitle();
    }

    /// <summary>
    /// 根据当前语言环境刷新导航项显示的标题
    /// </summary>
    public void RefreshTitle()
    {
        Title = LocalizationManager.GetString(TitleResourceKey);
    }
}

/// <summary>
/// 应用程序主窗口顶级 ViewModel
/// 负责导航项路由、当前页面展示、UI 缩放率同步以及全局文件拖拽分发
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private double _uiScale = 1.0;

    /// <summary>
    /// 当前是否正处于设置页面
    /// </summary>
    public bool IsSettingsSelected => CurrentPage == SettingsVm;

    /// <summary>
    /// 侧边栏主导航功能列表
    /// </summary>
    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    /// <summary>
    /// 侧边栏底部的设置导航项
    /// </summary>
    public NavigationItem SettingsItem { get; }

    public MergeViewModel MergeVm { get; }
    public SplitViewModel SplitVm { get; }
    public EncryptViewModel EncryptVm { get; }
    public DecryptViewModel DecryptVm { get; }
    public RotateViewModel RotateVm { get; }
    public RepairViewModel RepairVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainWindowViewModel(
        MergeViewModel mergeVm,
        SplitViewModel splitVm,
        EncryptViewModel encryptVm,
        DecryptViewModel decryptVm,
        RotateViewModel rotateVm,
        RepairViewModel repairVm,
        SettingsViewModel settingsVm)
    {
        MergeVm = mergeVm;
        SplitVm = splitVm;
        EncryptVm = encryptVm;
        DecryptVm = decryptVm;
        RotateVm = rotateVm;
        RepairVm = repairVm;
        SettingsVm = settingsVm;

        NavigationItems.Add(new NavigationItem("Nav_Merge", Symbol.DocumentMultiple, MergeVm));
        NavigationItems.Add(new NavigationItem("Nav_Split", Symbol.ArrowSplit, SplitVm));
        NavigationItems.Add(new NavigationItem("Nav_Encrypt", Symbol.LockClosed, EncryptVm));
        NavigationItems.Add(new NavigationItem("Nav_Decrypt", Symbol.LockOpen, DecryptVm));
        NavigationItems.Add(new NavigationItem("Nav_Rotate", Symbol.ArrowRotateClockwise, RotateVm));
        NavigationItems.Add(new NavigationItem("Nav_Repair", Symbol.Wrench, RepairVm));

        SettingsItem = new NavigationItem("Nav_Settings", Symbol.Settings, SettingsVm);

        // 语言变更时联动更新导航标题
        LocalizationManager.LanguageChanged += () =>
        {
            foreach (var item in NavigationItems)
            {
                item.RefreshTitle();
            }
            SettingsItem.RefreshTitle();
        };

        _uiScale = SettingsVm.SelectedScale?.Scale ?? 1.0;
        SettingsVm.OnUiScaleChanged = s => UiScale = s;

        _selectedNavigationItem = NavigationItems[0];
        _currentPage = _selectedNavigationItem.ViewModel;
    }

    /// <summary>
    /// 切换导航至设置页面
    /// </summary>
    [RelayCommand]
    public void SelectSettings()
    {
        SelectedNavigationItem = null;
        CurrentPage = SettingsItem.ViewModel;
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value != null)
        {
            CurrentPage = value.ViewModel;
        }
    }

    /// <summary>
    /// 处理主窗口全局接收到的拖拽外部文件，按当前激活的页面智能分发
    /// </summary>
    /// <param name="filePaths">外部拖入的文件绝对路径列表</param>
    public async Task HandleDroppedFilesAsync(IReadOnlyList<string> filePaths)
    {
        var pdfFiles = filePaths.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (pdfFiles.Count == 0) return;

        if (CurrentPage is MergeViewModel merge)
        {
            foreach (var f in pdfFiles)
            {
                await merge.AddFileInternal(f);
            }
        }
        else if (CurrentPage is SingleFileToolViewModel singleTool)
        {
            await singleTool.SetInputFile(pdfFiles[0]);
        }
    }
}
