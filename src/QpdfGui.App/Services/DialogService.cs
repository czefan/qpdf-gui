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

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow == null)
        {
            return false;
        }

        var tcs = new TaskCompletionSource<bool>();
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        var okButton = new Button
        {
            Content = "立即重启更新",
            Theme = dialog.FindResource("SolidButtonTheme") as Avalonia.Styling.ControlTheme,
            Classes = { "Primary" }
        };
        okButton.Click += (_, _) =>
        {
            tcs.TrySetResult(true);
            dialog.Close();
        };

        var cancelButton = new Button
        {
            Content = "稍后",
            Theme = dialog.FindResource("BorderlessButtonTheme") as Avalonia.Styling.ControlTheme,
            Margin = new Thickness(8, 0, 0, 0)
        };
        cancelButton.Click += (_, _) =>
        {
            tcs.TrySetResult(false);
            dialog.Close();
        };

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 13
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Children = { okButton, cancelButton }
                }
            }
        };

        dialog.Content = content;
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(desktop.MainWindow);
        return await tcs.Task;
    }
}

