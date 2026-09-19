using System.Text;

namespace QpdfGui.Core.Process;

/// <summary>
/// 作用域临时 Job JSON 文件，支持自动清理
/// </summary>
public sealed class TempJobFile : IDisposable, IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private bool _disposed;

    public string FilePath { get; }

    private TempJobFile(string filePath)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// 异步创建并写入无 BOM UTF-8 的临时 Job 文件
    /// </summary>
    public static async Task<TempJobFile> CreateAsync(string jsonContent, CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"qpdf-job-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempPath, jsonContent, Utf8NoBom, cancellationToken);
        return new TempJobFile(tempPath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch
        {
            // 忽略临时文件删除异常
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
