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
            ?? throw new FileNotFoundException("QPDF executable not found. Please configure path in settings or download it.");

        // 确保目标输出目录存在
        if (!string.IsNullOrEmpty(job.OutputFile))
        {
            var dir = Path.GetDirectoryName(job.OutputFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

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
            ?? throw new FileNotFoundException("QPDF executable not found.");

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
            var warning = ExtractWarning(line);
            if (!string.IsNullOrWhiteSpace(warning))
            {
                warnings.Add(warning);
            }
            else
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("qpdf: ERROR:", StringComparison.OrdinalIgnoreCase) ||
                    (trimmed.Contains("error:", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("qpdf: operation", StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(trimmed);
                }
            }
        });

        var cmd = Cli.Wrap(qpdfExe)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(stdoutPipe)
            .WithStandardErrorPipe(stderrPipe);

        CommandResult execResult;
        try
        {
            execResult = await cmd.ExecuteAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 取消任务时，清理可能残留的半成品输出文件
            if (!string.IsNullOrEmpty(outputFile) && File.Exists(outputFile))
            {
                try { File.Delete(outputFile); } catch { /* 忽略清理异常 */ }
            }
            throw;
        }

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

    /// <summary>
    /// 从标准错误行中提取净化后的警告消息；若属于总结行或非警告则返回 null
    /// </summary>
    public static string? ExtractWarning(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;

        // 过滤总结行
        if (trimmed.StartsWith("qpdf: operation succeeded with warnings", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("qpdf: operation succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (trimmed.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
        {
            var content = trimmed["WARNING:".Length..].Trim();
            return StripPathPrefix(content);
        }

        if (trimmed.StartsWith("qpdf: WARNING:", StringComparison.OrdinalIgnoreCase))
        {
            var content = trimmed["qpdf: WARNING:".Length..].Trim();
            return StripPathPrefix(content);
        }

        return null;
    }

    private static string StripPathPrefix(string message)
    {
        int idx = message.IndexOf(": ", StringComparison.Ordinal);
        if (idx > 0)
        {
            var prefix = message[..idx];
            if (prefix.Contains(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var parenIdx = prefix.IndexOf('(');
                if (parenIdx >= 0 && parenIdx < idx)
                {
                    return $"{prefix[parenIdx..].Trim()} {message[(idx + 2)..].Trim()}";
                }
                return message[(idx + 2)..].Trim();
            }
        }
        return message;
    }
}
