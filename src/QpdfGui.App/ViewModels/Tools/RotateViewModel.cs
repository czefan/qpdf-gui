using CommunityToolkit.Mvvm.ComponentModel;
using QpdfGui.App.Services;
using QpdfGui.Core.Inspect;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// PDF 页面旋转 ViewModel
/// </summary>
public partial class RotateViewModel : ToolViewModel
{
    [ObservableProperty]
    private string _rotationAngle = "+90";

    [ObservableProperty]
    private string _pageRange = "1-z";

    partial void OnRotationAngleChanged(string value) => UpdateEquivalentCommand();
    partial void OnPageRangeChanged(string value) => UpdateEquivalentCommand();

    public RotateViewModel(
        IDialogService dialogService,
        SettingsStore settingsStore,
        QpdfRunner? runner = null,
        PdfInspector? inspector = null)
        : base(dialogService, settingsStore, runner, inspector)
    {
    }

    protected override void UpdateDefaultOutputPath()
    {
        if (string.IsNullOrWhiteSpace(InputPath)) return;
        var dir = SettingsStore.Current.DefaultOutputDirectory;
        OutputPath = OutputPathResolver.ResolveUniquePath(dir, InputPath, "rotated");
    }

    public override QpdfJob? BuildJob()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath))
        {
            return null;
        }

        var range = string.IsNullOrWhiteSpace(PageRange) ? "1-z" : PageRange;
        var spec = $"{RotationAngle}:{range}";

        return new QpdfJob
        {
            InputFile = InputPath,
            OutputFile = OutputPath,
            Password = InputPassword,
            Rotate = [spec]
        };
    }
}
