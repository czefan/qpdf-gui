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
    public string TitleResourceKey { get; }
    public Symbol Symbol { get; }
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

    public void RefreshTitle()
    {
        Title = LocalizationManager.GetString(TitleResourceKey);
    }
}

/// <summary>
/// 应用程序主窗口顶级 ViewModel
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

    public bool IsSettingsSelected => CurrentPage == SettingsVm;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];
    public NavigationItem SettingsItem { get; }

    public EngineStatusViewModel EngineStatus { get; }
    public MergeViewModel MergeVm { get; }
    public SplitViewModel SplitVm { get; }
    public EncryptViewModel EncryptVm { get; }
    public DecryptViewModel DecryptVm { get; }
    public RotateViewModel RotateVm { get; }
    public RepairViewModel RepairVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainWindowViewModel(
        EngineStatusViewModel engineStatus,
        MergeViewModel mergeVm,
        SplitViewModel splitVm,
        EncryptViewModel encryptVm,
        DecryptViewModel decryptVm,
        RotateViewModel rotateVm,
        RepairViewModel repairVm,
        SettingsViewModel settingsVm)
    {
        EngineStatus = engineStatus;
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
        else if (CurrentPage is ToolViewModel tool)
        {
            await tool.SetInputFile(pdfFiles[0]);
        }
    }
}
