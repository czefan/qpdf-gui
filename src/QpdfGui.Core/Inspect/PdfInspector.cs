using CliWrap;
using CliWrap.Buffered;
using QpdfGui.Core.Process;

namespace QpdfGui.Core.Inspect;

/// <summary>
/// PDF 文件轻量探查服务
/// </summary>
public class PdfInspector
{
    private readonly string? _customQpdfPath;

    public PdfInspector(string? customQpdfPath = null)
    {
        _customQpdfPath = customQpdfPath;
    }

    /// <summary>
    /// 探查 PDF 页数与加密信息
    /// </summary>
    public async Task<PdfInfo> InspectAsync(string filePath, string? password = null, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return new PdfInfo
            {
                FilePath = filePath,
                ErrorMessage = $"文件不存在：{filePath}"
            };
        }

        var qpdfExe = QpdfLocator.Locate(_customQpdfPath);
        if (qpdfExe == null)
        {
            return new PdfInfo
            {
                FilePath = filePath,
                ErrorMessage = "未检测到 qpdf 可执行文件。"
            };
        }

        try
        {
            // 1. 探测是否加密 (ExitCode 0 = 加密, 2 = 未加密)
            var encCheck = await Cli.Wrap(qpdfExe)
                .WithArguments(args => args.Add("--is-encrypted").Add(filePath))
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct);

            var isEncrypted = encCheck.ExitCode == 0;

            // 2. 探测是否需要密码才能打开 (ExitCode 0 = 需要密码, 2 = 不需要)
            var reqPassCheck = await Cli.Wrap(qpdfExe)
                .WithArguments(args => args.Add("--requires-password").Add(filePath))
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct);

            var requiresPassword = reqPassCheck.ExitCode == 0;

            // 3. 读取总页数
            var pageResult = await Cli.Wrap(qpdfExe)
                .WithArguments(args =>
                {
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        args.Add($"--password={password}");
                    }
                    args.Add("--show-npages").Add(filePath);
                })
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct);

            int pageCount = 0;
            string? errorMessage = null;

            if (pageResult.ExitCode == 0 && int.TryParse(pageResult.StandardOutput.Trim(), out var parsedCount))
            {
                pageCount = parsedCount;
            }
            else if (requiresPassword && string.IsNullOrWhiteSpace(password))
            {
                // 需要密码且尚未提供
                errorMessage = "该文件已设置打开密码，请输入密码后重试。";
            }
            else
            {
                errorMessage = string.IsNullOrWhiteSpace(pageResult.StandardError)
                    ? "无法读取 PDF 页面信息。"
                    : pageResult.StandardError.Trim();
            }

            // 4. 若加密，读取加密摘要详情
            string? encDetails = null;
            if (isEncrypted)
            {
                var encResult = await Cli.Wrap(qpdfExe)
                    .WithArguments(args =>
                    {
                        if (!string.IsNullOrWhiteSpace(password))
                        {
                            args.Add($"--password={password}");
                        }
                        args.Add("--show-encryption").Add(filePath);
                    })
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct);

                encDetails = encResult.StandardOutput.Trim();
            }

            return new PdfInfo
            {
                FilePath = filePath,
                PageCount = pageCount,
                IsEncrypted = isEncrypted,
                RequiresPassword = requiresPassword,
                EncryptionDetails = encDetails,
                ErrorMessage = errorMessage
            };
        }
        catch (Exception ex)
        {
            return new PdfInfo
            {
                FilePath = filePath,
                ErrorMessage = ex.Message
            };
        }
    }
}
