using Avalonia.Controls;
using Avalonia.Logging;
using QpdfGui.App.Services;
using QpdfGui.App.ViewModels;
using QpdfGui.App.ViewModels.Tools;
using QpdfGui.App.Views;
using QpdfGui.Core.Inspect;
using QpdfGui.Core.Process;
using Xunit;

namespace QpdfGui.Core.Tests;

public class HeadlessSmokeTests
{
    private class BindingErrorSink : ILogSink
    {
        public List<string> Errors { get; } = [];

        public bool IsEnabled(LogEventLevel level, string area)
        {
            return level >= LogEventLevel.Warning && area == LogArea.Binding;
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
            if (IsEnabled(level, area))
            {
                Errors.Add($"[{level}] {area}: {messageTemplate} (Source: {source})");
            }
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            if (IsEnabled(level, area))
            {
                var msg = propertyValues.Length > 0 ? string.Format(messageTemplate, propertyValues) : messageTemplate;
                Errors.Add($"[{level}] {area}: {msg} (Source: {source})");
            }
        }
    }

    [Fact]
    public void AllViews_LoadAndBind_WithoutBindingErrors()
    {
        using var session = Avalonia.Headless.HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        session.Dispatch(() =>
        {
            var sink = new BindingErrorSink();
            var originalSink = Logger.Sink;
            Logger.Sink = sink;

            try
            {
                var settings = new SettingsStore();
                var dialogService = new DialogService();
                var runner = new QpdfRunner();
                var inspector = new PdfInspector(settings.Current.CustomQpdfPath);
                var downloader = new QpdfDownloaderService();
                var updater = new UpdateService();

                var engineStatus = new EngineStatusViewModel(settings, dialogService, downloader);
                var mergeVm = new MergeViewModel(dialogService, settings, runner, inspector);
                var splitVm = new SplitViewModel(dialogService, settings, runner, inspector);
                var encryptVm = new EncryptViewModel(dialogService, settings, runner, inspector);
                var decryptVm = new DecryptViewModel(dialogService, settings, runner, inspector);
                var rotateVm = new RotateViewModel(dialogService, settings, runner, inspector);
                var repairVm = new RepairViewModel(dialogService, settings, runner, inspector);
                var settingsVm = new SettingsViewModel(settings, dialogService, engineStatus, updater, downloader);

                var mainVm = new MainWindowViewModel(
                    engineStatus, mergeVm, splitVm, encryptVm, decryptVm, rotateVm, repairVm, settingsVm);

                var window = new MainWindow
                {
                    DataContext = mainVm
                };
                window.Show();

                var viewsAndVms = new (UserControl View, object DataContext)[]
                {
                    (new EncryptView(), encryptVm),
                    (new DecryptView(), decryptVm),
                    (new RotateView(), rotateVm),
                    (new RepairView(), repairVm),
                    (new QpdfGui.App.Views.SplitView(), splitVm),
                    (new MergeView(), mergeVm),
                    (new SettingsView(), settingsVm)
                };

                foreach (var (view, vm) in viewsAndVms)
                {
                    view.DataContext = vm;
                    window.Content = view;
                    view.ApplyTemplate();
                    window.UpdateLayout();
                }

                Assert.Empty(sink.Errors);
            }
            finally
            {
                Logger.Sink = originalSink;
            }
        }, default);
    }
}
