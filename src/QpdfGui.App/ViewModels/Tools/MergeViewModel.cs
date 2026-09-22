using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// 合并文件列表项，封装待合并单文件的路径、页数、目标截取范围及打开密码
/// </summary>
public partial class MergeFileItem : ObservableObject
{
    /// <summary>
    /// PDF 文件物理绝对路径
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// 文件名（不含目录路径）
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// 1-based 文件在合并列表中的显示序号（如 1, 2, 3）
    /// </summary>
    [ObservableProperty]
    private int _index;

    /// <summary>
    /// 文档总页数
    /// </summary>
    [ObservableProperty]
    private int _pageCount;

    /// <summary>
    /// 截取提取的页码范围（默认为 "1-z" 全部页面）
    /// </summary>
    [ObservableProperty]
    private string _pageRange = "1-z";

    /// <summary>
    /// 打开该加密文档所必需的密码（可选）
    /// </summary>
    [ObservableProperty]
    private string? _password;

    /// <summary>
    /// 该文档是否已加密
    /// </summary>
    [ObservableProperty]
    private bool _isEncrypted;

    /// <summary>
    /// 该文档是否必须输入密码才能读取
    /// </summary>
    [ObservableProperty]
    private bool _requiresPassword;
}

/// <summary>
/// 多 PDF 文档合并 ViewModel
/// 支持拖放/添加多个文档、调整拼接顺序、为每个文件自定义抽取页码范围及设置独立解密密码
/// </summary>
public partial class MergeViewModel : ViewModelBase
{
    private readonly IQpdfService _qpdfService;
    private readonly IDialogService _dialogService;
    private readonly SettingsStore _settingsStore;

    /// <summary>
    /// 待合并的文件集合列表（UI 列表绑定源，支持动态添加/删除/调序）
    /// </summary>
    public ObservableCollection<MergeFileItem> Files { get; } = [];

    /// <summary>
    /// 合并后输出的目标保存目录
    /// </summary>
    [ObservableProperty]
    private string? _outputDirectory;

    /// <summary>
    /// 合并后输出的目标文件名
    /// </summary>
    [ObservableProperty]
    private string? _outputFileName;

    /// <summary>
    /// 合并后输出的目标 PDF 路径
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputPath))]
    private string? _outputPath;

    /// <summary>
    /// 是否已具备输出路径
    /// </summary>
    public bool HasOutputPath => !string.IsNullOrWhiteSpace(OutputPath);

    private bool _isSyncingPath;

    partial void OnOutputDirectoryChanged(string? value) => SyncOutputPathFromParts();
    partial void OnOutputFileNameChanged(string? value) => SyncOutputPathFromParts();

    private void SyncOutputPathFromParts()
    {
        if (_isSyncingPath) return;
        _isSyncingPath = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(OutputDirectory) && !string.IsNullOrWhiteSpace(OutputFileName))
            {
                OutputPath = Path.Combine(OutputDirectory, OutputFileName);
            }
            else if (!string.IsNullOrWhiteSpace(OutputFileName))
            {
                OutputPath = OutputFileName;
            }
            else
            {
                OutputPath = OutputDirectory;
            }
            UpdateEquivalentCommand();
        }
        finally
        {
            _isSyncingPath = false;
        }
    }

    partial void OnOutputPathChanged(string? value)
    {
        if (_isSyncingPath) return;
        _isSyncingPath = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                OutputDirectory = Path.GetDirectoryName(value);
                OutputFileName = Path.GetFileName(value);
            }
            else
            {
                OutputDirectory = null;
                OutputFileName = null;
            }
        }
        finally
        {
            _isSyncingPath = false;
        }
    }

    /// <summary>
    /// 是否正在执行合并任务
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// 任务执行进度百分比（0 - 100）
    /// </summary>
    [ObservableProperty]
    private int _progressPercentage;

    /// <summary>
    /// 状态提示消息
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// 是否成功完成任务
    /// </summary>
    [ObservableProperty]
    private bool _hasSuccessResult;

    /// <summary>
    /// 最终生成的输出文件路径
    /// </summary>
    [ObservableProperty]
    private string? _lastOutputFilePath;

    /// <summary>
    /// 错误详细信息
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 高级合并规则表达式（如 "1.1-1.11, 2.1, 1.8-1.55"）
    /// </summary>
    [ObservableProperty]
    private string? _mergeRuleExpression;

    /// <summary>
    /// 执行过程中 QPDF 产生的警告或提示项
    /// </summary>
    public ObservableCollection<string> Warnings { get; } = [];

    /// <summary>
    /// 是否存在警告
    /// </summary>
    [ObservableProperty]
    private bool _hasWarnings;

    /// <summary>
    /// 合并规则解析成功后的友好预览提示
    /// </summary>
    [ObservableProperty]
    private string? _mergeRulePreview;

    /// <summary>
    /// 合并规则语法解析失败时的错误提示
    /// </summary>
    [ObservableProperty]
    private string? _mergeRuleError;

    /// <summary>
    /// 是否存在规则语法错误
    /// </summary>
    [ObservableProperty]
    private bool _hasMergeRuleError;

    /// <summary>
    /// 等效的 QPDF 纯命令行脚本
    /// </summary>
    [ObservableProperty]
    private string? _equivalentCommand;

    private CancellationTokenSource? _activeCts;

    public MergeViewModel(
        IQpdfService qpdfService,
        IDialogService dialogService,
        SettingsStore settingsStore)
    {
        _qpdfService = qpdfService;
        _dialogService = dialogService;
        _settingsStore = settingsStore;
    }

    partial void OnMergeRuleExpressionChanged(string? value)
    {
        ValidateAndUpdateMergeRule();
    }

    /// <summary>
    /// 校验并刷新合并规则解析状态
    /// </summary>
    public void ValidateAndUpdateMergeRule()
    {
        if (string.IsNullOrWhiteSpace(MergeRuleExpression))
        {
            MergeRulePreview = null;
            MergeRuleError = null;
            HasMergeRuleError = false;
            UpdateEquivalentCommand();
            return;
        }

        if (MergeRuleParser.TryParse(MergeRuleExpression, Files.Count, out var segments, out var error))
        {
            HasMergeRuleError = false;
            MergeRuleError = null;
            MergeRulePreview = "合并流程预览: " + MergeRuleParser.GeneratePreview(segments);
        }
        else
        {
            HasMergeRuleError = true;
            MergeRuleError = error;
            MergeRulePreview = null;
        }

        UpdateEquivalentCommand();
    }

    /// <summary>
    /// 刷新所有项的 1-based 序号
    /// </summary>
    private void UpdateIndices()
    {
        for (int i = 0; i < Files.Count; i++)
        {
            Files[i].Index = i + 1;
        }
    }

    /// <summary>
    /// 清除自定义合并规则，恢复默认顺序合并
    /// </summary>
    [RelayCommand]
    public void ClearRule()
    {
        MergeRuleExpression = null;
    }

    /// <summary>
    /// 弹出文件选择对话框多选添加 PDF 文件
    /// </summary>
    [RelayCommand]
    public async Task AddFiles()
    {
        var paths = await _dialogService.OpenFilesAsync(LocalizationManager.GetString("Input_SelectFile"));
        foreach (var path in paths)
        {
            await AddFileInternal(path);
        }
        UpdateIndices();
        ValidateAndUpdateMergeRule();
    }

    /// <summary>
    /// 内部方法：向合并列表中添加单个 PDF 文件，完成探查并智能设置初始输出路径
    /// </summary>
    public async Task AddFileInternal(string path)
    {
        if (Files.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase))) return;

        var info = await _qpdfService.InspectAsync(path);
        var item = new MergeFileItem
        {
            FilePath = path,
            PageCount = info.PageCount,
            IsEncrypted = info.IsEncrypted,
            RequiresPassword = info.RequiresPassword
        };

        Files.Add(item);
        UpdateIndices();

        // 若当前未设定输出路径，则基于首个文件的目录与名称生成默认输出文件名
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            var targetDir = !string.IsNullOrWhiteSpace(_settingsStore.Current.DefaultOutputDirectory) &&
                            Directory.Exists(_settingsStore.Current.DefaultOutputDirectory)
                ? _settingsStore.Current.DefaultOutputDirectory
                : Path.GetDirectoryName(path);

            OutputPath = OutputPathResolver.ResolveUniquePath(targetDir, path, "merged");
        }

        ValidateAndUpdateMergeRule();
    }

    /// <summary>
    /// 从合并列表中移除指定文件项
    /// </summary>
    [RelayCommand]
    public void RemoveFile(MergeFileItem item)
    {
        Files.Remove(item);
        UpdateIndices();
        ValidateAndUpdateMergeRule();
    }

    /// <summary>
    /// 将选中文件在列表中的合并次序上移一位
    /// </summary>
    [RelayCommand]
    public void MoveUp(MergeFileItem item)
    {
        var idx = Files.IndexOf(item);
        if (idx > 0)
        {
            Files.Move(idx, idx - 1);
            UpdateIndices();
            ValidateAndUpdateMergeRule();
        }
    }

    /// <summary>
    /// 将选中文件在列表中的合并次序下移一位
    /// </summary>
    [RelayCommand]
    public void MoveDown(MergeFileItem item)
    {
        var idx = Files.IndexOf(item);
        if (idx >= 0 && idx < Files.Count - 1)
        {
            Files.Move(idx, idx + 1);
            UpdateIndices();
            ValidateAndUpdateMergeRule();
        }
    }

    /// <summary>
    /// 清空所有已添加的文件列表及输出状态
    /// </summary>
    [RelayCommand]
    public void ClearFiles()
    {
        Files.Clear();
        OutputDirectory = null;
        OutputFileName = null;
        OutputPath = null;
        MergeRuleExpression = null;
        MergeRulePreview = null;
        MergeRuleError = null;
        HasMergeRuleError = false;
        HasSuccessResult = false;
        ErrorMessage = null;
        StatusMessage = null;
        Warnings.Clear();
        HasWarnings = false;
        EquivalentCommand = null;
    }

    /// <summary>
    /// 弹出选择文件夹对话框，允许用户自由指定输出目录
    /// </summary>
    [RelayCommand]
    public async Task BrowseDirectory()
    {
        var currentDir = !string.IsNullOrWhiteSpace(OutputDirectory) && Directory.Exists(OutputDirectory)
            ? OutputDirectory
            : (Files.Count > 0 && File.Exists(Files[0].FilePath) ? Path.GetDirectoryName(Files[0].FilePath) : null);

        var selected = await _dialogService.SelectFolderAsync(
            LocalizationManager.GetString("Output_DirectoryLabel"),
            currentDir);

        if (!string.IsNullOrWhiteSpace(selected))
        {
            OutputDirectory = selected;
        }
    }

    /// <summary>
    /// 弹出保存对话框自定义合并文件的输出路径
    /// </summary>
    [RelayCommand]
    public async Task BrowseOutput()
    {
        var defaultName = Files.Count > 0 ? Path.GetFileName(OutputPath) ?? "merged.pdf" : "merged.pdf";
        var dir = !string.IsNullOrWhiteSpace(OutputPath) ? Path.GetDirectoryName(OutputPath) : null;
        var selected = await _dialogService.SaveFileAsync(LocalizationManager.GetString("Output_Label"), defaultName, dir);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            OutputPath = selected;
            UpdateEquivalentCommand();
        }
    }

    /// <summary>
    /// 根据当前所有输入文件或自定义规则拼装等效 CLI 命令行
    /// </summary>
    public void UpdateEquivalentCommand()
    {
        if (Files.Count == 0 || string.IsNullOrWhiteSpace(OutputPath))
        {
            EquivalentCommand = null;
            return;
        }

        // 若配置了有效的自定义合并规则，优先按规则拼接
        if (!string.IsNullOrWhiteSpace(MergeRuleExpression) &&
            MergeRuleParser.TryParse(MergeRuleExpression, Files.Count, out var segments, out _))
        {
            var ruleParts = string.Join(" ", segments.Select(s =>
            {
                var f = Files[s.FileIndex - 1];
                var pwd = !string.IsNullOrWhiteSpace(f.Password) ? $"--password=\"{f.Password}\" " : "";
                return $"{pwd}\"{f.FilePath}\" {s.Range}";
            }));

            EquivalentCommand = $"qpdf --empty --pages {ruleParts} -- \"{OutputPath}\"";
            return;
        }

        var pagesParts = string.Join(" ", Files.Select(f =>
        {
            var r = string.IsNullOrWhiteSpace(f.PageRange) ? "1-z" : f.PageRange;
            var pwd = !string.IsNullOrWhiteSpace(f.Password) ? $"--password=\"{f.Password}\" " : "";
            return $"{pwd}\"{f.FilePath}\" {r}";
        }));

        EquivalentCommand = $"qpdf --empty --pages {pagesParts} -- \"{OutputPath}\"";
    }

    /// <summary>
    /// 取消当前正在执行的合并任务
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        _activeCts?.Cancel();
    }

    /// <summary>
    /// 打开已合并生成的 PDF 文件
    /// </summary>
    [RelayCommand]
    public void OpenFile()
    {
        if (!string.IsNullOrWhiteSpace(LastOutputFilePath) && File.Exists(LastOutputFilePath))
        {
            Process.Start(new ProcessStartInfo(LastOutputFilePath) { UseShellExecute = true });
        }
    }

    /// <summary>
    /// 打开合并文件所在目录并在资源管理器中高亮选中
    /// </summary>
    [RelayCommand]
    public void OpenFolder()
    {
        var target = LastOutputFilePath;
        if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{target}\"") { UseShellExecute = true });
        }
        else if (!string.IsNullOrWhiteSpace(target))
        {
            var dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
            }
        }
    }

    /// <summary>
    /// 复制等效的合并命令行至系统剪贴板
    /// </summary>
    [RelayCommand]
    public async Task CopyCommand()
    {
        if (string.IsNullOrWhiteSpace(EquivalentCommand)) return;

        await _dialogService.SetClipboardTextAsync(EquivalentCommand);
        StatusMessage = LocalizationManager.GetString("Btn_CopyCommand") + " OK";
    }

    /// <summary>
    /// 异步执行 PDF 合并流程
    /// </summary>
    [RelayCommand]
    public async Task ExecuteAsync()
    {
        if (Files.Count == 0 || string.IsNullOrWhiteSpace(OutputPath) || IsBusy) return;

        IsBusy = true;
        HasSuccessResult = false;
        ErrorMessage = null;
        Warnings.Clear();
        HasWarnings = false;
        ProgressPercentage = 0;
        StatusMessage = LocalizationManager.GetString("Status_Processing");

        _activeCts = new CancellationTokenSource();
        var progress = new Progress<int>(p => ProgressPercentage = p);

        try
        {
            List<PagesSpec> pagesSpecs;

            // 优先检查用户是否指定了高级合并规则
            if (!string.IsNullOrWhiteSpace(MergeRuleExpression))
            {
                if (!MergeRuleParser.TryParse(MergeRuleExpression, Files.Count, out var segments, out var ruleErr))
                {
                    ErrorMessage = ruleErr;
                    StatusMessage = LocalizationManager.GetString("Status_Failed");
                    IsBusy = false;
                    return;
                }

                pagesSpecs = segments.Select(s =>
                {
                    var fileItem = Files[s.FileIndex - 1];
                    return new PagesSpec
                    {
                        File = fileItem.FilePath,
                        Range = s.Range,
                        Password = fileItem.Password
                    };
                }).ToList();
            }
            else
            {
                pagesSpecs = Files.Select(f => new PagesSpec
                {
                    File = f.FilePath,
                    Range = string.IsNullOrWhiteSpace(f.PageRange) ? "1-z" : PageRange.Normalize(f.PageRange),
                    Password = f.Password
                }).ToList();
            }

            var result = await _qpdfService.MergeAsync(pagesSpecs, OutputPath, progress, _activeCts.Token);

            if (result.IsCompleted)
            {
                ProgressPercentage = 100;
                HasSuccessResult = true;
                LastOutputFilePath = result.OutputFile ?? OutputPath;
                if (result.Warnings.Count > 0)
                {
                    foreach (var w in result.Warnings)
                    {
                        Warnings.Add(w);
                    }
                    HasWarnings = true;
                    StatusMessage = $"{LocalizationManager.GetString("Status_Success")} ({result.Warnings.Count} 条提示/警告, {result.Duration.TotalSeconds:F2}s)";
                }
                else
                {
                    StatusMessage = $"{LocalizationManager.GetString("Status_Success")} ({result.Duration.TotalSeconds:F2}s)";
                }
            }
            else
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.StandardError)
                    ? LocalizationManager.GetString("Status_Failed")
                    : result.StandardError;
                StatusMessage = LocalizationManager.GetString("Status_Failed");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = LocalizationManager.GetString("Btn_Cancel");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = LocalizationManager.GetString("Status_Failed");
        }
        finally
        {
            IsBusy = false;
            _activeCts = null;
        }
    }
}
