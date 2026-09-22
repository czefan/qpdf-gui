using CommunityToolkit.Mvvm.ComponentModel;
using QpdfGui.App.Services;
using QpdfGui.Core.Inspect;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

public enum SplitMode
{
    FixedPages,
    SplitRanges,
    ExtractSingle
}

/// <summary>
/// PDF 拆分与多文件切分 ViewModel
/// 采用三选一清晰模式：每 N 页 / 按多个范围 / 提取单个范围
/// </summary>
public partial class SplitViewModel : ToolViewModel
{
    [ObservableProperty]
    private SplitMode _mode = SplitMode.FixedPages;

    [ObservableProperty]
    private int _fixedPagesCount = 1;

    [ObservableProperty]
    private string _fixedScopeRange = "1-z";

    [ObservableProperty]
    private string _customRanges = "1-2, 3-5";

    [ObservableProperty]
    private string _singleExtractRange = "1";

    public bool IsFixedPagesMode => Mode == SplitMode.FixedPages;
    public bool IsSplitRangesMode => Mode == SplitMode.SplitRanges;
    public bool IsExtractSingleMode => Mode == SplitMode.ExtractSingle;

    partial void OnModeChanged(SplitMode value)
    {
        OnPropertyChanged(nameof(IsFixedPagesMode));
        OnPropertyChanged(nameof(IsSplitRangesMode));
        OnPropertyChanged(nameof(IsExtractSingleMode));
        UpdateDefaultOutputPath();
        UpdateEquivalentCommand();
    }

    partial void OnFixedPagesCountChanged(int value) => UpdateEquivalentCommand();
    partial void OnFixedScopeRangeChanged(string value) => UpdateEquivalentCommand();
    partial void OnCustomRangesChanged(string value) => UpdateEquivalentCommand();
    partial void OnSingleExtractRangeChanged(string value) => UpdateEquivalentCommand();

    public SplitViewModel(
        IDialogService dialogService,
        SettingsStore settingsStore,
        QpdfRunner? runner = null,
        PdfInspector? inspector = null)
        : base(dialogService, settingsStore, runner, inspector)
    {
    }

    protected override void UpdateDefaultOutputPath()
    {
        if (string.IsNullOrWhiteSpace(InputPath)) return;
        var dir = SettingsStore.Current.DefaultOutputDirectory;
        var baseName = Path.GetFileNameWithoutExtension(InputPath);

        switch (Mode)
        {
            case SplitMode.FixedPages:
                OutputFileName = $"{baseName}_page_%d.pdf";
                OutputDirectory = !string.IsNullOrWhiteSpace(dir) ? dir : Path.GetDirectoryName(InputPath);
                break;
            case SplitMode.SplitRanges:
                OutputFileName = $"{baseName}_split";
                OutputDirectory = !string.IsNullOrWhiteSpace(dir) ? dir : Path.GetDirectoryName(InputPath);
                break;
            case SplitMode.ExtractSingle:
                OutputPath = OutputPathResolver.ResolveUniquePath(dir, InputPath, "extracted");
                break;
        }
    }

    public override QpdfJob? BuildJob()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath))
        {
            return null;
        }

        switch (Mode)
        {
            case SplitMode.FixedPages:
                var hasScope = !string.IsNullOrWhiteSpace(FixedScopeRange) &&
                               !FixedScopeRange.Trim().Equals("1-z", StringComparison.OrdinalIgnoreCase);
                return new QpdfJob
                {
                    InputFile = hasScope ? null : InputPath,
                    Empty = hasScope ? "" : null,
                    OutputFile = OutputPath,
                    Password = hasScope ? null : InputPassword,
                    SplitPages = Math.Max(1, FixedPagesCount).ToString(),
                    Pages = hasScope
                        ? [new PagesSpec { File = InputPath, Range = FixedScopeRange.Trim(), Password = InputPassword }]
                        : null
                };

            case SplitMode.ExtractSingle:
                var singleRange = string.IsNullOrWhiteSpace(SingleExtractRange) ? "1" : SingleExtractRange.Trim();
                return new QpdfJob
                {
                    Empty = "",
                    OutputFile = OutputPath,
                    Pages =
                    [
                        new PagesSpec
                        {
                            File = InputPath,
                            Range = PageRange.Normalize(singleRange),
                            Password = InputPassword
                        }
                    ]
                };

            case SplitMode.SplitRanges:
                var ranges = ParseRanges(CustomRanges);
                if (ranges.Count == 0) return null;
                // 为展示等效命令提供首个 Job 示例
                var firstOut = Path.Combine(OutputDirectory ?? "", $"{Path.GetFileNameWithoutExtension(InputPath)}_{ranges[0].Replace(":", "_")}.pdf");
                return new QpdfJob
                {
                    Empty = "",
                    OutputFile = firstOut,
                    Pages =
                    [
                        new PagesSpec
                        {
                            File = InputPath,
                            Range = PageRange.Normalize(ranges[0]),
                            Password = InputPassword
                        }
                    ]
                };

            default:
                return null;
        }
    }

    public override async Task ExecuteAsync()
    {
        if (IsBusy) return;

        if (Mode != SplitMode.SplitRanges)
        {
            await base.ExecuteAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(InputPath)) return;
        var ranges = ParseRanges(CustomRanges);
        if (ranges.Count == 0) return;

        var targetDir = !string.IsNullOrWhiteSpace(OutputDirectory)
            ? OutputDirectory
            : Path.GetDirectoryName(InputPath) ?? Environment.CurrentDirectory;

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var baseName = Path.GetFileNameWithoutExtension(InputPath);

        await RunPipelineAsync(async (progress, ct) =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var allWarnings = new List<string>();

            for (int i = 0; i < ranges.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var r = ranges[i];
                var safeR = r.Replace(":", "_").Replace("/", "_");
                var outPath = Path.Combine(targetDir, $"{baseName}_{safeR}.pdf");

                var job = new QpdfJob
                {
                    Empty = "",
                    OutputFile = outPath,
                    Pages =
                    [
                        new PagesSpec
                        {
                            File = InputPath,
                            Range = PageRange.Normalize(r),
                            Password = InputPassword
                        }
                    ]
                };

                var res = await Runner.RunJobAsync(job, SettingsStore.Current.CustomQpdfPath, null, ct);
                if (!res.IsCompleted)
                {
                    return res;
                }
                allWarnings.AddRange(res.Warnings);

                int percent = (int)((i + 1.0) / ranges.Count * 100);
                progress.Report(percent);
            }

            sw.Stop();
            return new QpdfResult
            {
                ExitCode = 0,
                Duration = sw.Elapsed,
                OutputFile = targetDir,
                Warnings = allWarnings,
                StandardOutput = $"Split into {ranges.Count} files successfully."
            };
        });
    }

    private static List<string> ParseRanges(string? expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return [];
        return expr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
