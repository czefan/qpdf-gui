using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Inspect;
using QpdfGui.Core.Process;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// 单文件 PDF 处理工具（拆分、加密、解密、旋转、修复）的通用抽象基类
/// 统一封装了文件输入探查、密码校验、输出路径推导、任务异步执行生命周期（进度/取消/耗时）以及结果后置操作
/// </summary>
public abstract partial class SingleFileToolViewModel : ViewModelBase
{
    /// <summary>
    /// QPDF 核心执行与探查服务
    /// </summary>
    protected readonly IQpdfService QpdfService;

    /// <summary>
    /// 文件对话框与剪贴板服务
    /// </summary>
    protected readonly IDialogService DialogService;

    /// <summary>
    /// 全局应用配置存储
    /// </summary>
    protected readonly SettingsStore SettingsStore;

    /// <summary>
    /// 输入的 PDF 文件绝对路径
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName))]
    [NotifyPropertyChangedFor(nameof(HasInputFile))]
    private string? _inputPath;

    /// <summary>
    /// 输入文件的文件名（不含路径）
    /// </summary>
    public string FileName => string.IsNullOrWhiteSpace(InputPath) ? string.Empty : Path.GetFileName(InputPath);

    /// <summary>
    /// 是否已选定输入文件
    /// </summary>
    public bool HasInputFile => !string.IsNullOrWhiteSpace(InputPath);

    /// <summary>
    /// 文件大小的可读性格式化显示（如 "2.4 MB"）
    /// </summary>
    [ObservableProperty]
    private string _fileSizeDisplay = string.Empty;

    /// <summary>
    /// 文档的总页数
    /// </summary>
    [ObservableProperty]
    private int _pageCount;

    /// <summary>
    /// 该文档是否启用了 PDF 加密（可能为只读或权限限制）
    /// </summary>
    [ObservableProperty]
    private bool _isEncrypted;

    /// <summary>
    /// 该文档是否必须提供打开密码才能读取内容
    /// </summary>
    [ObservableProperty]
    private bool _requiresPassword;

    /// <summary>
    /// 用户当前填写的打开密码
    /// </summary>
    [ObservableProperty]
    private string? _inputPassword;

    /// <summary>
    /// 处理结果的目标保存目录
    /// </summary>
    [ObservableProperty]
    private string? _outputDirectory;

    /// <summary>
    /// 处理结果的目标输出文件名（或前缀）
    /// </summary>
    [ObservableProperty]
    private string? _outputFileName;

    /// <summary>
    /// 处理结果的目标完整输出路径
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputPath))]
    private string? _outputPath;

    /// <summary>
    /// 是否已指定输出路径
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
    /// 当前是否有后台任务正在执行
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// 任务执行进度百分比（0 - 100）
    /// </summary>
    [ObservableProperty]
    private int _progressPercentage;

    /// <summary>
    /// 状态栏/提示文本（如正在处理、成功、失败或需要密码）
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// 任务是否成功完成（用于在界面展示后置操作条）
    /// </summary>
    [ObservableProperty]
    private bool _hasSuccessResult;

    /// <summary>
    /// 最终成功生成的输出文件物理路径
    /// </summary>
    [ObservableProperty]
    private string? _lastOutputFilePath;

    /// <summary>
    /// 详细错误消息（如 QPDF 标准错误输出或异常消息）
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// 当前配置对应的 QPDF 纯命令行等效文本，方便开发者与高级用户直接复制使用
    /// </summary>
    [ObservableProperty]
    private string? _equivalentCommand;

    /// <summary>
    /// 执行过程中 QPDF 产生的警告或修复提示项
    /// </summary>
    public System.Collections.ObjectModel.ObservableCollection<string> Warnings { get; } = [];

    /// <summary>
    /// 是否存在警告/修复记录
    /// </summary>
    [ObservableProperty]
    private bool _hasWarnings;

    /// <summary>
    /// 当前活动执行任务的取消令牌源
    /// </summary>
    protected CancellationTokenSource? ActiveCts;

    protected SingleFileToolViewModel(
        IQpdfService qpdfService,
        IDialogService dialogService,
        SettingsStore settingsStore)
    {
        QpdfService = qpdfService;
        DialogService = dialogService;
        SettingsStore = settingsStore;
    }

    /// <summary>
    /// 弹出文件选择对话框供用户选取输入 PDF
    /// </summary>
    [RelayCommand]
    public async Task SelectInput()
    {
        var path = await DialogService.OpenFileAsync(LocalizationManager.GetString("Input_SelectFile"));
        if (!string.IsNullOrWhiteSpace(path))
        {
            await SetInputFile(path);
        }
    }

    /// <summary>
    /// 设置并加载输入 PDF 文件，重置前次状态并触发文档信息探查与默认输出推导
    /// </summary>
    /// <param name="path">输入的 PDF 物理文件绝对路径</param>
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

    /// <summary>
    /// 异步调用底层 QPDF 探查服务，读取页数、加密状态以及是否需要打开密码
    /// </summary>
    public async Task RefreshInspectionAsync()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || !File.Exists(InputPath)) return;

        var info = await QpdfService.InspectAsync(InputPath, InputPassword);
        PageCount = info.PageCount;
        IsEncrypted = info.IsEncrypted;
        RequiresPassword = info.RequiresPassword;

        if (info.RequiresPassword && string.IsNullOrWhiteSpace(InputPassword))
        {
            StatusMessage = LocalizationManager.GetString("Input_PasswordPrompt");
        }
    }

    /// <summary>
    /// 提交输入的打开密码并重新校验文档访问权限
    /// </summary>
    [RelayCommand]
    public async Task SubmitPassword()
    {
        await RefreshInspectionAsync();
        UpdateEquivalentCommand();
    }

    /// <summary>
    /// 清除当前已加载的文件及相关结果状态
    /// </summary>
    [RelayCommand]
    public void ClearFile()
    {
        InputPath = null;
        PageCount = 0;
        IsEncrypted = false;
        RequiresPassword = false;
        InputPassword = null;
        OutputDirectory = null;
        OutputFileName = null;
        OutputPath = null;
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
            : (!string.IsNullOrWhiteSpace(InputPath) && File.Exists(InputPath) ? Path.GetDirectoryName(InputPath) : null);

        var selected = await DialogService.SelectFolderAsync(
            LocalizationManager.GetString("Output_DirectoryLabel"),
            currentDir);

        if (!string.IsNullOrWhiteSpace(selected))
        {
            OutputDirectory = selected;
        }
    }

    /// <summary>
    /// 弹出保存文件对话框，允许用户自由指定输出文件的存储路径
    /// </summary>
    [RelayCommand]
    public async Task BrowseOutput()
    {
        if (string.IsNullOrWhiteSpace(InputPath)) return;

        var defaultName = !string.IsNullOrWhiteSpace(OutputPath)
            ? Path.GetFileName(OutputPath)
            : Path.GetFileNameWithoutExtension(InputPath) + "_out.pdf";

        var dir = !string.IsNullOrWhiteSpace(OutputPath) ? Path.GetDirectoryName(OutputPath) : null;
        var selected = await DialogService.SaveFileAsync(LocalizationManager.GetString("Output_Label"), defaultName, dir);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            OutputPath = selected;
            UpdateEquivalentCommand();
        }
    }

    /// <summary>
    /// 取消当前正在执行的异步后台任务
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        ActiveCts?.Cancel();
    }

    /// <summary>
    /// 调用系统默认关联的阅读器打开已生成的输出 PDF 文件
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
    /// 在 Windows 资源管理器中打开输出文件所在文件夹，并自动高亮选中该文件
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
    /// 将当前任务等效的 QPDF 纯命令行指令复制到系统剪贴板
    /// </summary>
    [RelayCommand]
    public async Task CopyCommand()
    {
        if (string.IsNullOrWhiteSpace(EquivalentCommand)) return;

        await DialogService.SetClipboardTextAsync(EquivalentCommand);
        StatusMessage = LocalizationManager.GetString("Btn_CopyCommand") + " OK";
    }

    /// <summary>
    /// 由具体子类实现：基于当前输入文件与设置规则，自动推导默认输出路径
    /// </summary>
    protected abstract void UpdateDefaultOutputPath();

    /// <summary>
    /// 由具体子类实现：基于当前配置参数，实时生成等效的 QPDF CLI 命令行字符串
    /// </summary>
    public abstract void UpdateEquivalentCommand();

    /// <summary>
    /// 由具体子类实现：触发该工具核心任务的异步执行入口
    /// </summary>
    public abstract Task ExecuteAsync();

    /// <summary>
    /// 辅助方法：结合全局首选项中的默认输出目录，并在目标位置计算包含指定后缀且不重名的安全输出路径
    /// </summary>
    /// <param name="suffix">文件名附加后缀（如 "_split"、"_encrypted"）</param>
    /// <returns>计算出的唯一目标文件绝对路径</returns>
    protected string ResolveOutputPathWithSuffix(string suffix)
    {
        if (string.IsNullOrWhiteSpace(InputPath)) return string.Empty;

        var targetDir = !string.IsNullOrWhiteSpace(SettingsStore.Current.DefaultOutputDirectory) &&
                        Directory.Exists(SettingsStore.Current.DefaultOutputDirectory)
            ? SettingsStore.Current.DefaultOutputDirectory
            : Path.GetDirectoryName(InputPath);

        return OutputPathResolver.ResolveUniquePath(targetDir, InputPath, suffix);
    }

    /// <summary>
    /// 统一的任务执行管道：管理防重复并发触发、进度回报、取消令牌、执行计时与统一的异常与错误状态捕获
    /// </summary>
    /// <param name="action">接受进度报告与取消令牌并返回 QpdfResult 的异步执行委托</param>
    /// <returns>任务是否圆满成功完成</returns>
    protected async Task<bool> RunProcessTaskAsync(Func<IProgress<int>, CancellationToken, Task<QpdfResult>> action)
    {
        if (IsBusy) return false;

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
                    StatusMessage = $"{LocalizationManager.GetString("Status_Success")} ({result.Warnings.Count} 条提示/警告, {result.Duration.TotalSeconds:F2}s)";
                }
                else
                {
                    StatusMessage = $"{LocalizationManager.GetString("Status_Success")} ({result.Duration.TotalSeconds:F2}s)";
                }
                return true;
            }
            else
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.StandardError)
                    ? LocalizationManager.GetString("Status_Failed")
                    : result.StandardError;
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
        }
    }

    /// <summary>
    /// 将字节长度格式化为人类友好的可读字符串（B / KB / MB）
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
