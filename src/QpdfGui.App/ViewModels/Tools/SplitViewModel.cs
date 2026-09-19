using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// 拆分模式枚举
/// </summary>
public enum SplitMode
{
    /// <summary>
    /// 按指定页码范围列表拆分为多个独立文件（如 1-2, 3-5, 6-10）
    /// </summary>
    SplitRanges,

    /// <summary>
    /// 按固定连续页数切分为多个文件（支持指定切分的作用范围）
    /// </summary>
    FixedPages
}

/// <summary>
/// PDF 拆分与多文件切分 ViewModel
/// 支持按范围列表切分为多个文件，或按固定页数连续分卷切分（可指定作用范围）
/// </summary>
public partial class SplitViewModel : SingleFileToolViewModel
{
    /// <summary>
    /// 当前选定的拆分模式
    /// </summary>
    [ObservableProperty]
    private SplitMode _mode = SplitMode.SplitRanges;

    /// <summary>
    /// 基础作用页码范围（默认为全本，如 1-72 或 1-z；对固定切分和范围切分均生效）
    /// </summary>
    [ObservableProperty]
    private string _baseRange = "1-z";

    /// <summary>
    /// 分卷拆分范围列表（如 "1-2, 3-5, 6-10"）
    /// </summary>
    [ObservableProperty]
    private string _customRanges = "1-z";

    /// <summary>
    /// 固定切分页数（每个拆分文件包含多少页）
    /// </summary>
    [ObservableProperty]
    private int _fixedPagesCount = 1;

    /// <summary>
    /// 实时切分预估说明
    /// </summary>
    [ObservableProperty]
    private string? _splitPreviewText;

    public SplitViewModel(
        IQpdfService qpdfService,
        IDialogService dialogService,
        SettingsStore settingsStore)
        : base(qpdfService, dialogService, settingsStore)
    {
    }

    /// <summary>
    /// 是否为“按范围分卷切分”模式
    /// </summary>
    public bool IsSplitRangesMode
    {
        get => Mode == SplitMode.SplitRanges;
        set
        {
            if (value && Mode != SplitMode.SplitRanges)
            {
                Mode = SplitMode.SplitRanges;
            }
        }
    }

    /// <summary>
    /// 是否为“按固定页数连续切分”模式
    /// </summary>
    public bool IsFixedPagesMode
    {
        get => Mode == SplitMode.FixedPages;
        set
        {
            if (value && Mode != SplitMode.FixedPages)
            {
                Mode = SplitMode.FixedPages;
            }
        }
    }

    partial void OnModeChanged(SplitMode value)
    {
        OnPropertyChanged(nameof(IsSplitRangesMode));
        OnPropertyChanged(nameof(IsFixedPagesMode));
        UpdateDefaultOutputPath();
        UpdateSplitPreview();
        UpdateEquivalentCommand();
    }

    partial void OnBaseRangeChanged(string value)
    {
        UpdateSplitPreview();
        UpdateEquivalentCommand();
    }

    partial void OnCustomRangesChanged(string value)
    {
        UpdateSplitPreview();
        UpdateEquivalentCommand();
    }

    partial void OnFixedPagesCountChanged(int value)
    {
        UpdateSplitPreview();
        UpdateEquivalentCommand();
    }

    /// <inheritdoc />
    protected override void UpdateDefaultOutputPath()
    {
        if (string.IsNullOrWhiteSpace(InputPath)) return;

        if (PageCount > 0)
        {
            BaseRange = $"1-{PageCount}";
            CustomRanges = $"1-{PageCount}";
        }
        else
        {
            BaseRange = "1-z";
            CustomRanges = "1-z";
        }
        UpdateSplitPreview();

        var targetDir = !string.IsNullOrWhiteSpace(SettingsStore.Current.DefaultOutputDirectory) &&
                        Directory.Exists(SettingsStore.Current.DefaultOutputDirectory)
            ? SettingsStore.Current.DefaultOutputDirectory
            : Path.GetDirectoryName(InputPath);

        OutputPath = OutputPathResolver.ResolveSplitPattern(targetDir, InputPath);
    }

    /// <summary>
    /// 计算并更新切分预览说明文本
    /// </summary>
    private void UpdateSplitPreview()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || PageCount <= 0)
        {
            SplitPreviewText = null;
            return;
        }

        if (Mode == SplitMode.SplitRanges)
        {
            var parts = ParseRangeList(CustomRanges);
            if (parts.Count > 1)
            {
                SplitPreviewText = $"💡 将按指定范围切分为 {parts.Count} 个独立文档（如 _{parts[0]}.pdf, _{parts[1]}.pdf ...）";
            }
            else if (parts.Count == 1)
            {
                SplitPreviewText = $"💡 将提取第 {parts[0]} 页为独立文档";
            }
            else
            {
                SplitPreviewText = null;
            }
        }
        else
        {
            var pagesPerFile = Math.Max(1, FixedPagesCount);
            var estimatedCount = (int)Math.Ceiling((double)PageCount / pagesPerFile);
            var rangeHint = !string.IsNullOrWhiteSpace(BaseRange) && BaseRange != "1-z" && BaseRange != $"1-{PageCount}"
                ? $"在范围 [{BaseRange}] 内，"
                : "";
            SplitPreviewText = $"💡 {rangeHint}每 {pagesPerFile} 页为一个文档，预计生成约 {estimatedCount} 个文件（如 _01-05.pdf ...）";
        }
    }

    /// <summary>
    /// 解析逗号/分号分隔的页码范围列表
    /// </summary>
    private static List<string> ParseRangeList(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return [];
        return expression.Split([',', '，', ';', '；', ' '], StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => !string.IsNullOrEmpty(s))
                         .ToList();
    }

    /// <inheritdoc />
    public override void UpdateEquivalentCommand()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            EquivalentCommand = null;
            return;
        }

        var pwdArg = !string.IsNullOrWhiteSpace(InputPassword) ? $"--password=\"{InputPassword}\" " : "";

        if (Mode == SplitMode.SplitRanges)
        {
            var ranges = ParseRangeList(CustomRanges);
            if (ranges.Count <= 1)
            {
                var r = ranges.Count == 1 ? ranges[0] : "1-z";
                EquivalentCommand = $"qpdf --empty {pwdArg}--pages \"{InputPath}\" {r} -- \"{OutputPath}\"";
            }
            else
            {
                var dir = Path.GetDirectoryName(OutputPath) ?? ".";
                var baseName = Path.GetFileNameWithoutExtension(InputPath);
                var example = $"qpdf --empty {pwdArg}--pages \"{InputPath}\" {ranges[0]} -- \"{Path.Combine(dir, $"{baseName}_{ranges[0]}.pdf")}\" ... (共 {ranges.Count} 次)";
                EquivalentCommand = example;
            }
        }
        else
        {
            var hasSubRange = !string.IsNullOrWhiteSpace(BaseRange) &&
                              BaseRange != "1-z" &&
                              BaseRange != $"1-{PageCount}";

            EquivalentCommand = hasSubRange
                ? $"qpdf --empty {pwdArg}--pages \"{InputPath}\" {BaseRange} -- --split-pages={FixedPagesCount} \"{OutputPath}\""
                : $"qpdf \"{InputPath}\" {pwdArg}--split-pages={FixedPagesCount} \"{OutputPath}\"";
        }
    }

    /// <inheritdoc />
    [RelayCommand]
    public override async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath)) return;

        if (Mode == SplitMode.SplitRanges)
        {
            var ranges = ParseRangeList(CustomRanges);
            if (ranges.Count == 0)
            {
                ranges = ["1-z"];
            }

            var targetDir = Path.GetDirectoryName(OutputPath) ?? Path.GetDirectoryName(InputPath) ?? ".";
            await RunProcessTaskAsync((progress, ct) =>
                QpdfService.SplitByRangesAsync(InputPath, ranges, targetDir, InputPassword, progress, ct));
        }
        else
        {
            var hasSubRange = !string.IsNullOrWhiteSpace(BaseRange) &&
                              BaseRange != "1-z" &&
                              BaseRange != $"1-{PageCount}";

            await RunProcessTaskAsync((progress, ct) =>
                QpdfService.SplitAsync(InputPath, OutputPath, FixedPagesCount, hasSubRange ? BaseRange : null, InputPassword, progress, ct));
        }
    }
}

