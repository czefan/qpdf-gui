using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using QpdfGui.App.Services;
using QpdfGui.App.ViewModels;
using QpdfGui.App.ViewModels.Tools;
using QpdfGui.App.Views;
using QpdfGui.Core.Services;

namespace QpdfGui.App;

/// <summary>
/// 应用程序入口与依赖注入容器管理类
/// 负责全局服务注册、首选项（主题、语言）初始应用、主窗口生命周期管理以及自动化测试截图支持
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 全局依赖注入服务提供者实例
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var settings = Services.GetRequiredService<SettingsStore>();
        LocalizationManager.ApplyLanguage(settings.Current.Language);
        SettingsViewModel.ApplyTheme(settings.Current.ThemeVariant);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            desktop.MainWindow = mainWindow;

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

    /// <summary>
    /// 注册核心业务服务与所有页面的 ViewModel 依赖项
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        services.AddSingleton<IQpdfService>(sp =>
        {
            var store = sp.GetRequiredService<SettingsStore>();
            return new QpdfService(customPathProvider: () => store.Current.CustomQpdfPath);
        });

        services.AddTransient<MergeViewModel>();
        services.AddTransient<SplitViewModel>();
        services.AddTransient<EncryptViewModel>();
        services.AddTransient<DecryptViewModel>();
        services.AddTransient<RotateViewModel>();
        services.AddTransient<RepairViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddSingleton<MainWindowViewModel>();
    }
}
