using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace QpdfGui.App.Services;

/// <summary>
/// 基于 Avalonia 顶级窗口 TopLevel StorageProvider 和 Clipboard 实现的对话框与剪贴板服务
/// </summary>
public class DialogService : IDialogService
{
    /// <summary>
    /// 获取当前主窗口的 StorageProvider 实例
    /// </summary>
    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window != null)
            {
                return TopLevel.GetTopLevel(window)?.StorageProvider;
            }
        }
        return null;
    }

    public async Task<string?> OpenFileAsync(string title, string[]? extensions = null)
    {
        var sp = GetStorageProvider();
        if (sp == null) return null;

        var patterns = extensions != null && extensions.Length > 0
            ? extensions.Select(e => e.StartsWith("*.") ? e : $"*.{e.TrimStart('.')}").ToList()
            : ["*.pdf"];

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PDF 文档") { Patterns = patterns },
                new FilePickerFileType("所有文件") { Patterns = ["*.*"] }
            ]
        };

        var files = await sp.OpenFilePickerAsync(options);
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<IReadOnlyList<string>> OpenFilesAsync(string title, string[]? extensions = null)
    {
        var sp = GetStorageProvider();
        if (sp == null) return [];

        var patterns = extensions != null && extensions.Length > 0
            ? extensions.Select(e => e.StartsWith("*.") ? e : $"*.{e.TrimStart('.')}").ToList()
            : ["*.pdf"];

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("PDF 文档") { Patterns = patterns },
                new FilePickerFileType("所有文件") { Patterns = ["*.*"] }
            ]
        };

        var files = await sp.OpenFilePickerAsync(options);
        return files.Select(f => f.Path.LocalPath).ToList();
    }

    public async Task<string?> SaveFileAsync(string title, string suggestedFileName, string? defaultDirectory = null)
    {
        var sp = GetStorageProvider();
        if (sp == null) return null;

        IStorageFolder? suggestedFolder = null;
        if (!string.IsNullOrWhiteSpace(defaultDirectory) && Directory.Exists(defaultDirectory))
        {
            suggestedFolder = await sp.TryGetFolderFromPathAsync(defaultDirectory);
        }

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "pdf",
            SuggestedStartLocation = suggestedFolder,
            FileTypeChoices =
            [
                new FilePickerFileType("PDF 文档") { Patterns = ["*.pdf"] }
            ]
        };

        var file = await sp.SaveFilePickerAsync(options);
        return file?.Path.LocalPath;
    }

    public async Task<string?> SelectFolderAsync(string title, string? defaultDirectory = null)
    {
        var sp = GetStorageProvider();
        if (sp == null) return null;

        IStorageFolder? suggestedFolder = null;
        if (!string.IsNullOrWhiteSpace(defaultDirectory) && Directory.Exists(defaultDirectory))
        {
            suggestedFolder = await sp.TryGetFolderFromPathAsync(defaultDirectory);
        }

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = suggestedFolder
        };

        var folders = await sp.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.Clipboard != null)
            {
                var dt = new Avalonia.Input.DataTransfer();
                dt.Add(Avalonia.Input.DataTransferItem.CreateText(text));
                await topLevel.Clipboard.SetDataAsync(dt);
            }
        }
    }
}
