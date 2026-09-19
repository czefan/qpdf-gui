using QpdfGui.Core.Inspect;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;

namespace QpdfGui.Core.Services;

/// <summary>
/// QPDF 核心业务服务实现
/// </summary>
public class QpdfService : IQpdfService
{
    private readonly QpdfRunner _runner;
    private readonly Func<string?>? _customPathProvider;

    public QpdfService(QpdfRunner? runner = null, Func<string?>? customPathProvider = null)
    {
        _runner = runner ?? new QpdfRunner();
        _customPathProvider = customPathProvider;
    }

    private string? CustomQpdfPath => _customPathProvider?.Invoke();

    public Task<PdfInfo> InspectAsync(string filePath, string? password = null, CancellationToken ct = default)
    {
        var inspector = new PdfInspector(CustomQpdfPath);
        return inspector.InspectAsync(filePath, password, ct);
    }

    public Task<QpdfResult> MergeAsync(
        IReadOnlyList<PagesSpec> pages,
        string outputPath,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var job = new QpdfJob
        {
            InputFile = "--empty",
            OutputFile = outputPath,
            Pages = pages.ToList()
        };

        return _runner.RunJobAsync(job, CustomQpdfPath, progress, ct);
    }

    public Task<QpdfResult> SplitAsync(
        string inputPath,
        string outputPattern,
        int splitPages,
        string? pageRange = null,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var hasSubRange = !string.IsNullOrWhiteSpace(pageRange) &&
                          !pageRange.Trim().Equals("1-z", StringComparison.OrdinalIgnoreCase);

        var job = new QpdfJob
        {
            InputFile = hasSubRange ? "--empty" : inputPath,
            OutputFile = outputPattern,
            SplitPages = splitPages.ToString(),
            Password = hasSubRange ? null : password,
            Pages = hasSubRange
                ? [new PagesSpec { File = inputPath, Range = pageRange!.Trim(), Password = password }]
                : null
        };

        return _runner.RunJobAsync(job, CustomQpdfPath, progress, ct);
    }

    public async Task<QpdfResult> SplitByRangesAsync(
        string inputPath,
        IReadOnlyList<string> ranges,
        string outputDirectory,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        if (ranges.Count == 0)
        {
            return new QpdfResult
            {
                ExitCode = 0,
                StandardOutput = "没有需要拆分的页码范围"
            };
        }

        Directory.CreateDirectory(outputDirectory);
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < ranges.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var range = ranges[i];
            var safeRangeName = range.Replace(":", "_").Replace("/", "_");
            var outPath = Path.Combine(outputDirectory, $"{baseName}_{safeRangeName}.pdf");

            var result = await ExtractAsync(inputPath, outPath, range, password, null, ct);
            if (!result.IsCompleted)
            {
                return result;
            }

            int percent = (int)((i + 1.0) / ranges.Count * 100);
            progress?.Report(percent);
        }

        sw.Stop();
        return new QpdfResult
        {
            ExitCode = 0,
            Duration = sw.Elapsed,
            OutputFile = outputDirectory,
            StandardOutput = $"成功按指定范围切分为 {ranges.Count} 个文件"
        };
    }

    public Task<QpdfResult> ExtractAsync(
        string inputPath,
        string outputPath,
        string pageRange,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var job = new QpdfJob
        {
            InputFile = "--empty",
            OutputFile = outputPath,
            Pages =
            [
                new PagesSpec
                {
                    File = inputPath,
                    Range = PageRange.Normalize(pageRange),
                    Password = password
                }
            ]
        };

        return _runner.RunJobAsync(job, CustomQpdfPath, progress, ct);
    }

    public Task<QpdfResult> EncryptAsync(
        string inputPath,
        string outputPath,
        EncryptOptions options,
        string? currentPassword = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var job = new QpdfJob
        {
            InputFile = inputPath,
            OutputFile = outputPath,
            Password = currentPassword,
            Encrypt = options
        };

        return _runner.RunJobAsync(job, CustomQpdfPath, progress, ct);
    }

    public Task<QpdfResult> DecryptAsync(
        string inputPath,
        string outputPath,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var job = new QpdfJob
        {
            InputFile = inputPath,
            OutputFile = outputPath,
            Password = password,
            Decrypt = ""
        };

        return _runner.RunJobAsync(job, CustomQpdfPath, progress, ct);
    }

    public Task<QpdfResult> RotateAsync(
        string inputPath,
        string outputPath,
        string rotationSpec,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var job = new QpdfJob
        {
            InputFile = inputPath,
            OutputFile = outputPath,
            Password = password,
            Rotate = [rotationSpec]
        };

        return _runner.RunJobAsync(job, CustomQpdfPath, progress, ct);
    }

    public Task<QpdfResult> RepairAsync(
        string inputPath,
        string outputPath,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var job = new QpdfJob
        {
            InputFile = inputPath,
            OutputFile = outputPath,
            Password = password,
            ObjectStreams = "generate"
        };

        return _runner.RunJobAsync(job, CustomQpdfPath, progress, ct);
    }
}
