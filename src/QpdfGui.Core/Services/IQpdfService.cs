using QpdfGui.Core.Inspect;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;

namespace QpdfGui.Core.Services;

/// <summary>
/// QPDF 高级业务服务契约
/// </summary>
public interface IQpdfService
{
    /// <summary>
    /// 探查 PDF 基础信息（页数、加密状态）
    /// </summary>
    Task<PdfInfo> InspectAsync(string filePath, string? password = null, CancellationToken ct = default);

    /// <summary>
    /// 多文件页面合并
    /// </summary>
    Task<QpdfResult> MergeAsync(
        IReadOnlyList<PagesSpec> pages,
        string outputPath,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// 按固定页数拆分为多个文件（可指定抽取的作用页码范围，outputPattern 需含 %d 或由 QPDF 自动补齐）
    /// </summary>
    Task<QpdfResult> SplitAsync(
        string inputPath,
        string outputPattern,
        int splitPages,
        string? pageRange = null,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// 按一组页码范围切分为多个独立文件（如 ["1-2", "3-5", "6-10"]）
    /// </summary>
    Task<QpdfResult> SplitByRangesAsync(
        string inputPath,
        IReadOnlyList<string> ranges,
        string outputDirectory,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// 按页码范围提取为单文件
    /// </summary>
    Task<QpdfResult> ExtractAsync(
        string inputPath,
        string outputPath,
        string pageRange,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// 256 位 AES 加密及权限控制
    /// </summary>
    Task<QpdfResult> EncryptAsync(
        string inputPath,
        string outputPath,
        EncryptOptions options,
        string? currentPassword = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// 解密 PDF（解除权限限制或移除打开密码）
    /// </summary>
    Task<QpdfResult> DecryptAsync(
        string inputPath,
        string outputPath,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// 旋转页面（如 "+90:1-z" 或 "-90:2-5"）
    /// </summary>
    Task<QpdfResult> RotateAsync(
        string inputPath,
        string outputPath,
        string rotationSpec,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// 修复损坏或异常的 PDF
    /// </summary>
    Task<QpdfResult> RepairAsync(
        string inputPath,
        string outputPath,
        string? password = null,
        IProgress<int>? progress = null,
        CancellationToken ct = default);
}
