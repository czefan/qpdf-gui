using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Inspect;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// 所有 PDF 工具页面（单文件工具及合并工具）的统一通用抽象基类
/// 统一承载：
/// 1. 输入文件探查与密码交互（单文件通用）
/// 2. 输出目录/文件名/物理路径双向同步与浏览选择
/// 3. QPDF 任务执行生命周期（进度、取消、耗时、成功/失败/警告诊断）
/// 4. 基于 QpdfJob.ToCommandLine() 的等效命令自动生成与一键复制
/// 5. 跨平台结果文件与目录打开
/// </summary>
public abstract partial class ToolViewModel : ViewModelBase
{
    protected readonly QpdfRunner Runner;
    protected readonly PdfInspector Inspector;
    protected readonly IDialogService DialogService;
    protected readonly SettingsStore SettingsStore;

    #region 输入文件属性（单文件通用）

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName))]
    [NotifyPropertyChangedFor(nameof(HasInputFile))]
    private string? _inputPath;

    public string FileName => string.IsNullOrWhiteSpace(InputPath) ? string.Empty : Path.GetFileName(InputPath);

    public bool HasInputFile => !string.IsNullOrWhiteSpace(InputPath);

    [ObservableProperty]
    private string _fileSizeDisplay = string.Empty;

    [ObservableProperty]
    private int _pageCount;

    [ObservableProperty]
    private bool _isEncrypted;

    [ObservableProperty]
    private bool _requiresPassword;

    [ObservableProperty]
    private string? _inputPassword;

    #endregion

    #region 输出路径属性

    [ObservableProperty]
    private string? _outputDirectory;

    [ObservableProperty]
    private string? _outputFileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputPath))]
    private string? _outputPath;

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
            UpdateEquivalentCommand();
        }
        finally
        {
            _isSyncingPath = false;
        }
    }

    #endregion

    #region 执行状态与结果反馈

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExecute))]
    private bool _isBusy;

    [ObservableProperty]
    private int _progressPercentage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasSuccessResult;

    [ObservableProperty]
    private string? _lastOutputFilePath;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _equivalentCommand;

    [ObservableProperty]
    private bool _isCopied;

    public ObservableCollection<string> Warnings { get; } = [];

    [ObservableProperty]
    private bool _hasWarnings;

    protected CancellationTokenSource? ActiveCts;

    public virtual bool CanExecute => BuildJob() != null && !IsBusy;

    #endregion

    protected ToolViewModel(
        IDialogService dialogService,
        SettingsStore settingsStore,
        QpdfRunner? runner = null,
        PdfInspector? inspector = null)
    {
        DialogService = dialogService;
        SettingsStore = settingsStore;
        Runner = runner ?? new QpdfRunner();
        Inspector = inspector ?? new PdfInspector(settingsStore.Current.CustomQpdfPath);
    }

    #region 核心抽象：Job 构建与单一真源

    /// <summary>
    /// 依据当前界面的配置构造标准的 QpdfJob 对象；若当前参数不可执行则返回 null
    /// </summary>
    public abstract QpdfJob? BuildJob();

    /// <summary>
    /// 可选：针对多步骤切分等场景提供多 Job 迭代
    /// </summary>
    public virtual IEnumerable<QpdfJob>? BuildJobs() => null;

    /// <summary>
    /// 当不可执行时给出的阻止原因（用于界面按钮提示等）
    /// </summary>
    public virtual string? BlockingReason
    {
        get
        {
            if (HasInputFile && RequiresPassword && string.IsNullOrWhiteSpace(InputPassword))
            {
                return LocalizationManager.GetString("Input_PasswordPrompt");
            }
            if (!HasInputFile)
            {
                return LocalizationManager.GetString("Input_SelectFile");
            }
            return null;
        }
    }

    /// <summary>
    /// 依据当前 Job 派生等效命令行字符串
    /// </summary>
    public virtual void UpdateEquivalentCommand()
    {
        EquivalentCommand = BuildJob()?.ToCommandLine();
        OnPropertyChanged(nameof(CanExecute));
    }

    #endregion

    #region 单文件输入交互与探查

    [RelayCommand]
    public async Task SelectInput()
    {
        var path = await DialogService.OpenFileAsync(LocalizationManager.GetString("Input_SelectFile"));
        if (!string.IsNullOrWhiteSpace(path))
        {
            await SetInputFile(path);
        }
    }

    public async Task SetInputFile(string path)
    {
        InputPath = path;
        HasSuccessResult = false;
        ErrorMessage = null;
        StatusMessage = null;

        if (File.Exists(path))
        {
            var fi = new FileInfo(path);
            FileSizeDisplay = FormatFileSize(fi.Length);
        }

        await RefreshInspectionAsync();
        UpdateDefaultOutputPath();
        UpdateEquivalentCommand();
    }

    public async Task RefreshInspectionAsync()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || !File.Exists(InputPath)) return;

        var info = await Inspector.InspectAsync(InputPath, InputPassword);
        PageCount = info.PageCount;
        IsEncrypted = info.IsEncrypted;
        RequiresPassword = info.RequiresPassword;

        if (info.RequiresPassword && string.IsNullOrWhiteSpace(InputPassword))
        {
            StatusMessage = LocalizationManager.GetString("Input_PasswordPrompt");
        }
        UpdateEquivalentCommand();
    }

    [RelayCommand]
    public async Task SubmitPassword()
    {
        await RefreshInspectionAsync();
    }

    [RelayCommand]
    public void ClearFile()
    {
        InputPath = null;
        PageCount = 0;
        IsEncrypted = false;
        RequiresPassword = false;
        InputPassword = null;
        FileSizeDisplay = string.Empty;
        HasSuccessResult = false;
        ErrorMessage = null;
        StatusMessage = null;
        Warnings.Clear();
        HasWarnings = false;
        OutputPath = null;
        OutputDirectory = null;
        OutputFileName = null;
        UpdateEquivalentCommand();
    }

    protected virtual void UpdateDefaultOutputPath()
    {
        // 子类重写以更新默认输出文件名
    }

    #endregion

    #region 输出路径交互

    [RelayCommand]
    public async Task BrowseOutputFolder()
    {
        var dir = await DialogService.SelectFolderAsync(LocalizationManager.GetString("Output_BrowseFolder"));
        if (!string.IsNullOrWhiteSpace(dir))
        {
            OutputDirectory = dir;
        }
    }

    [RelayCommand]
    public async Task BrowseOutputFilePath()
    {
        var path = await DialogService.SaveFileAsync(
            LocalizationManager.GetString("Output_Label"),
            OutputFileName ?? "output.pdf");
        if (!string.IsNullOrWhiteSpace(path))
        {
            OutputPath = path;
        }
    }

    #endregion

    #region 命令行复制与结果打开

    [RelayCommand]
    public async Task CopyCommand()
    {
        if (string.IsNullOrWhiteSpace(EquivalentCommand)) return;

        await DialogService.SetClipboardTextAsync(EquivalentCommand);
        IsCopied = true;
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsCopied = false);
        });
    }

    [RelayCommand]
    public void OpenFile()
    {
        if (!string.IsNullOrWhiteSpace(LastOutputFilePath))
        {
            Shell.OpenFile(LastOutputFilePath);
        }
    }

    [RelayCommand]
    public void OpenFolder()
    {
        if (!string.IsNullOrWhiteSpace(LastOutputFilePath))
        {
            Shell.RevealInFolder(LastOutputFilePath);
        }
        else if (!string.IsNullOrWhiteSpace(OutputDirectory))
        {
            Shell.RevealInFolder(OutputDirectory);
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        ActiveCts?.Cancel();
    }

    #endregion

    #region 统一执行管线

    [RelayCommand]
    public virtual async Task ExecuteAsync()
    {
        if (IsBusy) return;

        var job = BuildJob();
        if (job == null) return;

        await RunPipelineAsync(async (progress, ct) =>
        {
            return await Runner.RunJobAsync(job, SettingsStore.Current.CustomQpdfPath, progress, ct);
        });
    }

    protected async Task<bool> RunPipelineAsync(Func<IProgress<int>, CancellationToken, Task<QpdfResult>> action)
    {
        IsBusy = true;
        HasSuccessResult = false;
        ErrorMessage = null;
        Warnings.Clear();
        HasWarnings = false;
        ProgressPercentage = 0;
        StatusMessage = LocalizationManager.GetString("Status_Processing");

        ActiveCts = new CancellationTokenSource();
        var progress = new Progress<int>(p => ProgressPercentage = p);

        try
        {
            var result = await action(progress, ActiveCts.Token);
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
                    var warningsTemplate = LocalizationManager.GetString("Status_WarningsCount");
                    var countStr = string.Format(warningsTemplate, result.Warnings.Count);
                    StatusMessage = $"{LocalizationManager.GetString("Status_Success")} ({countStr}, {result.Duration.TotalSeconds:F2}s)";
                }
                else
                {
                    StatusMessage = $"{LocalizationManager.GetString("Status_Success")} ({result.Duration.TotalSeconds:F2}s)";
                }
                return true;
            }
            else
            {
                ErrorMessage = result.ErrorText ?? result.StandardError;
                StatusMessage = LocalizationManager.GetString("Status_Failed");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = LocalizationManager.GetString("Btn_Cancel");
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = LocalizationManager.GetString("Status_Failed");
            return false;
        }
        finally
        {
            IsBusy = false;
            ActiveCts = null;
            UpdateEquivalentCommand();
        }
    }

    protected static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    #endregion
}
