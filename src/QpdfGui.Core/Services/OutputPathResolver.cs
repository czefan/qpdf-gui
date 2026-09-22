namespace QpdfGui.Core.Services;

/// <summary>
/// 输出路径解析与重名冲突处理
/// </summary>
public static class OutputPathResolver
{
    /// <summary>
    /// 生成不冲突的唯一目标文件路径
    /// </summary>
    /// <param name="targetDirectory">目标文件夹（若为 null 则使用源文件所在文件夹）</param>
    /// <param name="baseFileName">基础文件名（含或不含扩展名）</param>
    /// <param name="operationSuffix">操作后缀，如 "merged", "split", "decrypted", "encrypted", "rotated", "repaired"</param>
    public static string ResolveUniquePath(string? targetDirectory, string baseFileName, string operationSuffix)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
        var ext = Path.GetExtension(baseFileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".pdf";
        }

        var dir = !string.IsNullOrWhiteSpace(targetDirectory)
            ? targetDirectory
            : Path.GetDirectoryName(baseFileName) ?? Environment.CurrentDirectory;

        var candidateName = string.IsNullOrWhiteSpace(operationSuffix)
            ? $"{nameWithoutExt}{ext}"
            : $"{nameWithoutExt}_{operationSuffix}{ext}";

        var fullPath = Path.Combine(dir, candidateName);
        if (!File.Exists(fullPath))
        {
            return fullPath;
        }

        // 冲突递增序号
        var index = 1;
        while (true)
        {
            var indexedName = string.IsNullOrWhiteSpace(operationSuffix)
                ? $"{nameWithoutExt} ({index}){ext}"
                : $"{nameWithoutExt}_{operationSuffix} ({index}){ext}";

            fullPath = Path.Combine(dir, indexedName);
            if (!File.Exists(fullPath))
            {
                return fullPath;
            }
            index++;
        }
    }

    /// <summary>
    /// 生成拆分任务所需的带 %d 占位符的输出路径模板
    /// </summary>
    public static string ResolveSplitPattern(string? targetDirectory, string baseFileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
        var dir = !string.IsNullOrWhiteSpace(targetDirectory)
            ? targetDirectory
            : Path.GetDirectoryName(baseFileName) ?? Environment.CurrentDirectory;

        return Path.Combine(dir, $"{nameWithoutExt}_page_%d.pdf");
    }
}
