namespace QpdfGui.Core.Inspect;

/// <summary>
/// PDF 文件基础元数据
/// </summary>
public record PdfInfo
{
    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);
    public int PageCount { get; init; }
    public bool IsEncrypted { get; init; }
    public bool RequiresPassword { get; init; }
    public string? EncryptionDetails { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsValid => string.IsNullOrEmpty(ErrorMessage);
}
