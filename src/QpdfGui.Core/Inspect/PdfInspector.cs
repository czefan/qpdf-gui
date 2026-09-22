using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using QpdfGui.Core.Process;

namespace QpdfGui.Core.Inspect;

/// <summary>
/// PDF 文件轻量探查服务（双进程探测加密与页数）
/// </summary>
public class PdfInspector
{
    private readonly string? _customQpdfPath;

    public PdfInspector(string? customQpdfPath = null)
    {
        _customQpdfPath = customQpdfPath;
    }

    /// <summary>
    /// 探查 PDF 页数与加密信息（最多 2 次进程执行）
    /// </summary>
    public async Task<PdfInfo> InspectAsync(string filePath, string? password = null, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return new PdfInfo
            {
                FilePath = filePath,
                ErrorMessage = $"File not found: {filePath}"
            };
        }

        var qpdfExe = QpdfLocator.Locate(_customQpdfPath);
        if (qpdfExe == null)
        {
            return new PdfInfo
            {
                FilePath = filePath,
                ErrorMessage = "QPDF executable not found."
            };
        }

        try
        {
            // 进程 1：一次性获取加密结构状态
            var encArgs = new List<string>();
            if (!string.IsNullOrEmpty(password))
            {
                encArgs.Add($"--password={password}");
            }
            encArgs.Add("--json");
            encArgs.Add("--json-key=encrypt");
            encArgs.Add(filePath);

            var encExec = await Cli.Wrap(qpdfExe)
                .WithArguments(encArgs)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(ct);

            bool isEncrypted = false;
            bool requiresPassword = false;
            string? encDetails = null;

            if (encExec.ExitCode == 2 && encExec.StandardError.Contains("invalid password", StringComparison.OrdinalIgnoreCase))
            {
                isEncrypted = true;
                requiresPassword = true;
            }
            else if (encExec.ExitCode == 0)
            {
                try
                {
                    using var doc = JsonDocument.Parse(encExec.StandardOutput);
                    if (doc.RootElement.TryGetProperty("encrypt", out var encProp))
                    {
                        if (encProp.TryGetProperty("encrypted", out var encryptedElem))
                        {
                            isEncrypted = encryptedElem.GetBoolean();
                        }

                        if (isEncrypted)
                        {
                            bool userMatched = false;
                            if (encProp.TryGetProperty("userpasswordmatched", out var userMatchedElem))
                            {
                                userMatched = userMatchedElem.GetBoolean();
                            }

                            requiresPassword = !userMatched;

                            if (encProp.TryGetProperty("parameters", out var paramsElem))
                            {
                                var bits = paramsElem.TryGetProperty("bits", out var b) ? b.GetInt32() : 0;
                                var method = paramsElem.TryGetProperty("filemethod", out var m) ? m.GetString() : null;
                                encDetails = bits > 0 ? $"{method ?? "AES"} {bits}-bit" : method;
                            }
                        }
                    }
                }
                catch
                {
                    // 容错处理
                }
            }
            else
            {
                var err = encExec.StandardError.Trim();
                return new PdfInfo
                {
                    FilePath = filePath,
                    ErrorMessage = string.IsNullOrEmpty(err) ? "Failed to inspect PDF." : err
                };
            }

            // 进程 2：读取总页数（仅在无需密码或已正确解锁时调用）
            int pageCount = 0;
            string? errorMessage = null;

            if (requiresPassword && string.IsNullOrEmpty(password))
            {
                errorMessage = "Password required.";
            }
            else
            {
                var pageArgs = new List<string>();
                if (!string.IsNullOrEmpty(password))
                {
                    pageArgs.Add($"--password={password}");
                }
                pageArgs.Add("--show-npages");
                pageArgs.Add(filePath);

                var pageExec = await Cli.Wrap(qpdfExe)
                    .WithArguments(pageArgs)
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct);

                if (pageExec.ExitCode == 0 && int.TryParse(pageExec.StandardOutput.Trim(), out var parsedCount))
                {
                    pageCount = parsedCount;
                }
                else
                {
                    errorMessage = string.IsNullOrWhiteSpace(pageExec.StandardError)
                        ? "Failed to get page count."
                        : pageExec.StandardError.Trim();
                }
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
