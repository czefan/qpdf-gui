namespace QpdfGui.Core.Process;

/// <summary>
/// 表示 QPDF 命令或 Job 的执行结果
/// </summary>
public record QpdfResult
{
    public required int ExitCode { get; init; }
    public string? OutputFile { get; init; }
    public TimeSpan Duration { get; init; }
    public List<string> Warnings { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;

    /// <summary>
    /// 完全无警告、无错误地成功执行（ExitCode 0）
    /// </summary>
    public bool IsSuccess => ExitCode == 0;

    /// <summary>
    /// 执行完成但存在警告或修复性变动（ExitCode 3）
    /// </summary>
    public bool IsSuccessWithWarnings => ExitCode == 3;

    /// <summary>
    /// 无论是否有警告，核心任务均成功生成了目标文件
    /// </summary>
    public bool IsCompleted => ExitCode == 0 || ExitCode == 3;

    /// <summary>
    /// 是否发生错误
    /// </summary>
    public bool HasError => !IsCompleted;
}
