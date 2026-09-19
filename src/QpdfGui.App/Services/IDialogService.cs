namespace QpdfGui.App.Services;

/// <summary>
/// 文件对话框与系统剪贴板交互服务接口
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 打开单文件选择对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="extensions">支持的扩展名列表（如 ["*.pdf"] 或 ["pdf"]）</param>
    /// <returns>选中的文件本地绝对路径；若用户取消则返回 null</returns>
    Task<string?> OpenFileAsync(string title, string[]? extensions = null);

    /// <summary>
    /// 打开多文件选择对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="extensions">支持的扩展名列表</param>
    /// <returns>选中的文件绝对路径列表</returns>
    Task<IReadOnlyList<string>> OpenFilesAsync(string title, string[]? extensions = null);

    /// <summary>
    /// 打开文件保存路径选择对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="suggestedFileName">默认建议的文件名</param>
    /// <param name="defaultDirectory">初始打开的目录路径</param>
    /// <returns>用户选定的保存绝对路径；若取消则返回 null</returns>
    Task<string?> SaveFileAsync(string title, string suggestedFileName, string? defaultDirectory = null);

    /// <summary>
    /// 打开文件夹选择对话框
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="defaultDirectory">初始打开的目录路径</param>
    /// <returns>选中的文件夹绝对路径；若取消则返回 null</returns>
    Task<string?> SelectFolderAsync(string title, string? defaultDirectory = null);

    /// <summary>
    /// 将指定纯文本写入系统剪贴板（兼容 Avalonia 12 的 DataTransfer API）
    /// </summary>
    /// <param name="text">要复制的文本内容</param>
    Task SetClipboardTextAsync(string text);
}
