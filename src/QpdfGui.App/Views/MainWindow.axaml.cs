using Avalonia.Controls;
using Avalonia.Input;
using QpdfGui.App.ViewModels;

namespace QpdfGui.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var files = e.DataTransfer.TryGetFiles();
        if (files != null && files.Length > 0)
        {
            var paths = files.Select(f => f.Path.LocalPath).Where(File.Exists).ToList();
            if (paths.Count > 0)
            {
                await vm.HandleDroppedFilesAsync(paths);
                e.Handled = true;
            }
        }
    }
}
