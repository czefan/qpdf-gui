using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using QpdfGui.App.Services;
using QpdfGui.App.ViewModels;
using QpdfGui.App.ViewModels.Tools;
using QpdfGui.App.Views;
using QpdfGui.Core.Inspect;
using QpdfGui.Core.Process;

namespace QpdfGui.App;

/// <summary>
/// 应用程序入口与对象生命周期管理类
/// 移除反射 DI 容器，采用直接单例对象构造，极简且提升启动速度与裁剪安全性
/// </summary>
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var settings = new SettingsStore();
        var dialogService = new DialogService();
        var runner = new QpdfRunner();
        var inspector = new PdfInspector(settings.Current.CustomQpdfPath);
        var downloaderService = new QpdfDownloaderService();
        var updateService = new UpdateService();

        var engineStatus = new EngineStatusViewModel(settings, dialogService, downloaderService);

        var mergeVm = new MergeViewModel(dialogService, settings, runner, inspector);
        var splitVm = new SplitViewModel(dialogService, settings, runner, inspector);
        var encryptVm = new EncryptViewModel(dialogService, settings, runner, inspector);
        var decryptVm = new DecryptViewModel(dialogService, settings, runner, inspector);
        var rotateVm = new RotateViewModel(dialogService, settings, runner, inspector);
        var repairVm = new RepairViewModel(dialogService, settings, runner, inspector);
        var settingsVm = new SettingsViewModel(settings, dialogService, engineStatus, updateService, downloaderService);

        var mainVm = new MainWindowViewModel(engineStatus, mergeVm, splitVm, encryptVm, decryptVm, rotateVm, repairVm, settingsVm);

        LocalizationManager.ApplyLanguage(settings.Current.Language);
        SettingsViewModel.ApplyTheme(settings.Current.ThemeVariant);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            desktop.MainWindow = mainWindow;

            // P7：延后至窗口 Opened 之后在低优先级队列异步探测 QPDF 引擎版本，不与首帧竞争
            mainWindow.Opened += (_, _) =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await engineStatus.RefreshAsync();
                }, DispatcherPriority.Background);
            };

            // 自动化自测截图与参数驱动支持
            var args = desktop.Args ?? [];
            var screenshotIdx = Array.IndexOf(args, "--screenshot");
            if (screenshotIdx >= 0 && screenshotIdx + 1 < args.Length)
            {
                var savePath = args[screenshotIdx + 1];
                var langIdx = Array.IndexOf(args, "--lang");
                if (langIdx >= 0 && langIdx + 1 < args.Length)
                {
                    LocalizationManager.ApplyLanguage(args[langIdx + 1]);
                }

                var themeIdx = Array.IndexOf(args, "--theme");
                if (themeIdx >= 0 && themeIdx + 1 < args.Length)
                {
                    var themeStr = args[themeIdx + 1];
                    if (themeStr.Equals("dark", StringComparison.OrdinalIgnoreCase))
                    {
                        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                    }
                    else if (themeStr.Equals("light", StringComparison.OrdinalIgnoreCase))
                    {
                        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                    }
                }

                if (args.Contains("--settings"))
                {
                    mainVm.SelectSettings();
                }
                else if (args.Contains("--split"))
                {
                    mainVm.SelectedNavigationItem = mainVm.NavigationItems.FirstOrDefault(i => i.ViewModel is SplitViewModel) ?? mainVm.NavigationItems[0];
                }
                else if (args.Contains("--encrypt"))
                {
                    mainVm.SelectedNavigationItem = mainVm.NavigationItems.FirstOrDefault(i => i.ViewModel is EncryptViewModel) ?? mainVm.NavigationItems[0];
                }
                else if (args.Contains("--decrypt"))
                {
                    mainVm.SelectedNavigationItem = mainVm.NavigationItems.FirstOrDefault(i => i.ViewModel is DecryptViewModel) ?? mainVm.NavigationItems[0];
                }
                else if (args.Contains("--rotate"))
                {
                    mainVm.SelectedNavigationItem = mainVm.NavigationItems.FirstOrDefault(i => i.ViewModel is RotateViewModel) ?? mainVm.NavigationItems[0];
                }
                else if (args.Contains("--repair"))
                {
                    mainVm.SelectedNavigationItem = mainVm.NavigationItems.FirstOrDefault(i => i.ViewModel is RepairViewModel) ?? mainVm.NavigationItems[0];
                }
                else if (args.Contains("--merge"))
                {
                    mainVm.SelectedNavigationItem = mainVm.NavigationItems.FirstOrDefault(i => i.ViewModel is MergeViewModel) ?? mainVm.NavigationItems[0];
                }

                mainWindow.Loaded += async (_, _) =>
                {
                    await Task.Delay(600);
                    try
                    {
                        mainWindow.Width = 1080;
                        mainWindow.Height = 720;
                        var width = 1080;
                        var height = 720;

                        mainWindow.Measure(new Size(width, height));
                        mainWindow.Arrange(new Rect(0, 0, width, height));

                        var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
                        rtb.Render(mainWindow);

                        var dir = Path.GetDirectoryName(savePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        rtb.Save(savePath, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Screenshot failed: {ex}");
                    }
                    finally
                    {
                        desktop.Shutdown();
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
