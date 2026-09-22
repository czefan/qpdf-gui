using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Inspect;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// 合并文件列表项，封装待合并单文件的路径、页数、目标截取范围及打开密码
/// </summary>
public partial class MergeFileItem : ObservableObject
{
    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);

    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private int _pageCount;

    [ObservableProperty]
    private string _pageRange = "1-z";

    [ObservableProperty]
    private string? _password;

    [ObservableProperty]
    private bool _isEncrypted;

    [ObservableProperty]
    private bool _requiresPassword;
}

/// <summary>
/// 多 PDF 文档合并 ViewModel
/// 列表行为单一真源，支持高级表达式导入解析
/// </summary>
public partial class MergeViewModel : ToolViewModel
{
    public ObservableCollection<MergeFileItem> Files { get; } = [];

    [ObservableProperty]
    private string? _mergeRuleExpression;

    [ObservableProperty]
    private string? _mergeRuleError;

    [ObservableProperty]
    private bool _hasMergeRuleError;

    public override bool CanExecute => Files.Count > 0 && !string.IsNullOrWhiteSpace(OutputPath) && !IsBusy;

    public MergeViewModel(
        IDialogService dialogService,
        SettingsStore settingsStore,
        QpdfRunner? runner = null,
        PdfInspector? inspector = null)
        : base(dialogService, settingsStore, runner, inspector)
    {
    }

    private void UpdateIndices()
    {
        for (int i = 0; i < Files.Count; i++)
        {
            Files[i].Index = i + 1;
        }
    }

    [RelayCommand]
    public async Task AddFiles()
    {
        var paths = await DialogService.OpenFilesAsync(LocalizationManager.GetString("Input_SelectFile"));
        foreach (var path in paths)
        {
            await AddFileInternal(path);
        }
        UpdateIndices();
        UpdateEquivalentCommand();
    }

    public async Task AddFileInternal(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        var info = await Inspector.InspectAsync(path);
        var item = new MergeFileItem
        {
            FilePath = path,
            PageCount = info.PageCount,
            IsEncrypted = info.IsEncrypted,
            RequiresPassword = info.RequiresPassword
        };

        Files.Add(item);
        UpdateIndices();

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            var targetDir = !string.IsNullOrWhiteSpace(SettingsStore.Current.DefaultOutputDirectory) &&
                            Directory.Exists(SettingsStore.Current.DefaultOutputDirectory)
                ? SettingsStore.Current.DefaultOutputDirectory
                : Path.GetDirectoryName(path);

            OutputPath = OutputPathResolver.ResolveUniquePath(targetDir, path, "merged");
        }

        UpdateEquivalentCommand();
    }

    [RelayCommand]
    public void RemoveFile(MergeFileItem item)
    {
        Files.Remove(item);
        UpdateIndices();
        UpdateEquivalentCommand();
    }

    [RelayCommand]
    public void MoveUp(MergeFileItem item)
    {
        var idx = Files.IndexOf(item);
        if (idx > 0)
        {
            Files.Move(idx, idx - 1);
            UpdateIndices();
            UpdateEquivalentCommand();
        }
    }

    [RelayCommand]
    public void MoveDown(MergeFileItem item)
    {
        var idx = Files.IndexOf(item);
        if (idx >= 0 && idx < Files.Count - 1)
        {
            Files.Move(idx, idx + 1);
            UpdateIndices();
            UpdateEquivalentCommand();
        }
    }

    [RelayCommand]
    public void ClearFiles()
    {
        Files.Clear();
        OutputPath = null;
        OutputDirectory = null;
        OutputFileName = null;
        MergeRuleExpression = null;
        MergeRuleError = null;
        HasMergeRuleError = false;
        UpdateEquivalentCommand();
    }

    [RelayCommand]
    public void ImportExpression()
    {
        if (string.IsNullOrWhiteSpace(MergeRuleExpression) || Files.Count == 0) return;

        if (MergeRuleParser.TryParse(MergeRuleExpression, Files.Count, out var segments, out var error))
        {
            HasMergeRuleError = false;
            MergeRuleError = null;

            var existing = Files.ToList();
            Files.Clear();
            foreach (var seg in segments)
            {
                var src = existing.ElementAtOrDefault(seg.FileIndex - 1);
                if (src != null)
                {
                    Files.Add(new MergeFileItem
                    {
                        FilePath = src.FilePath,
                        PageCount = src.PageCount,
                        PageRange = seg.Range,
                        Password = src.Password,
                        IsEncrypted = src.IsEncrypted,
                        RequiresPassword = src.RequiresPassword
                    });
                }
            }
            UpdateIndices();
            UpdateEquivalentCommand();
        }
        else
        {
            HasMergeRuleError = true;
            MergeRuleError = error;
        }
    }

    public override QpdfJob? BuildJob()
    {
        if (Files.Count == 0 || string.IsNullOrWhiteSpace(OutputPath))
        {
            return null;
        }

        var pages = Files.Select(f => new PagesSpec
        {
            File = f.FilePath,
            Range = string.IsNullOrWhiteSpace(f.PageRange) ? "1-z" : PageRange.Normalize(f.PageRange),
            Password = f.Password
        }).ToList();

        return new QpdfJob
        {
            Empty = "",
            OutputFile = OutputPath,
            Pages = pages
        };
    }
}
