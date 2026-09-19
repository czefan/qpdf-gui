using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using QpdfGui.Core.Jobs;

namespace QpdfGui.Core.Process;

/// <summary>
/// QPDF 进程调度器：负责 Job JSON 写入、CliWrap 调度、进度解析、错误收集与结果打包
/// </summary>
public partial class QpdfRunner
{
    [GeneratedRegex(@"write progress:\s*(\d+)%", RegexOptions.IgnoreCase)]
    private static partial Regex ProgressRegex();

    /// <summary>
    /// 运行指定的 QpdfJob
    /// </summary>
    public async Task<QpdfResult> RunJobAsync(
        QpdfJob job,
        string? customQpdfPath = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var qpdfExe = QpdfLocator.Locate(customQpdfPath)
            ?? throw new FileNotFoundException("未找到 qpdf 可执行文件，请在设置中配置或将其放置在 runtimes 目录下。");

        // 默认启用进度输出
        job.Progress ??= "";

        var jsonContent = job.ToJson();
        await using var tempJobFile = await TempJobFile.CreateAsync(jsonContent, cancellationToken);

        return await RunProcessAsync(
            qpdfExe,
            $"--job-json-file=\"{tempJobFile.FilePath}\"",
            job.OutputFile,
            progress,
            cancellationToken);
    }

    /// <summary>
    /// 运行通用 QPDF 命令行参数（用于元数据探测等轻量命令）
    /// </summary>
    public async Task<QpdfResult> RunArgsAsync(
        string arguments,
        string? customQpdfPath = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var qpdfExe = QpdfLocator.Locate(customQpdfPath)
            ?? throw new FileNotFoundException("未找到 qpdf 可执行文件。");

        return await RunProcessAsync(qpdfExe, arguments, null, progress, cancellationToken);
    }

    private static async Task<QpdfResult> RunProcessAsync(
        string qpdfExe,
        string arguments,
        string? outputFile,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        var warnings = new List<string>();
        var errors = new List<string>();

        var sw = Stopwatch.StartNew();

        var stdoutPipe = PipeTarget.ToDelegate(line =>
        {
            stdoutBuilder.AppendLine(line);
            var match = ProgressRegex().Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var percent))
            {
                progress?.Report(percent);
            }
        });

        var stderrPipe = PipeTarget.ToDelegate(line =>
        {
            stderrBuilder.AppendLine(line);
            var trimmed = line.Trim();
            if (trimmed.Contains("warning", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(trimmed);
            }
            else if (trimmed.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(trimmed);
            }
        });

        var cmd = Cli.Wrap(qpdfExe)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(stdoutPipe)
            .WithStandardErrorPipe(stderrPipe);

        var execResult = await cmd.ExecuteAsync(cancellationToken);
        sw.Stop();

        // 确保任务完成时报告 100% 进度
        if (execResult.ExitCode == 0 || execResult.ExitCode == 3)
        {
            progress?.Report(100);
        }

        return new QpdfResult
        {
            ExitCode = execResult.ExitCode,
            OutputFile = outputFile,
            Duration = sw.Elapsed,
            Warnings = warnings,
            Errors = errors,
            StandardOutput = stdoutBuilder.ToString(),
            StandardError = stderrBuilder.ToString()
        };
    }
}
