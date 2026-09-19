using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// PDF 页面旋转 ViewModel
/// 支持顺时针/逆时针 90° 或 180° 旋转，并可指定作用的页码范围
/// </summary>
public partial class RotateViewModel : SingleFileToolViewModel
{
    /// <summary>
    /// 旋转角度修饰符（"+90", "-90", "+180"）
    /// </summary>
    [ObservableProperty]
    private string _rotationAngle = "+90";

    /// <summary>
    /// 生效的页码范围（默认为全部页面 "1-z"）
    /// </summary>
    [ObservableProperty]
    private string _pageRange = "1-z";

    public RotateViewModel(
        IQpdfService qpdfService,
        IDialogService dialogService,
        SettingsStore settingsStore)
        : base(qpdfService, dialogService, settingsStore)
    {
    }

    partial void OnRotationAngleChanged(string value) => UpdateEquivalentCommand();
    partial void OnPageRangeChanged(string value) => UpdateEquivalentCommand();

    /// <inheritdoc />
    protected override void UpdateDefaultOutputPath()
    {
        OutputPath = ResolveOutputPathWithSuffix("rotated");
    }

    /// <inheritdoc />
    public override void UpdateEquivalentCommand()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            EquivalentCommand = null;
            return;
        }

        var range = string.IsNullOrWhiteSpace(PageRange) ? "1-z" : PageRange;
        var pwdArg = !string.IsNullOrWhiteSpace(InputPassword) ? $"--password=\"{InputPassword}\" " : "";
        EquivalentCommand = $"qpdf {pwdArg}\"{InputPath}\" --rotate={RotationAngle}:{range} \"{OutputPath}\"";
    }

    /// <inheritdoc />
    [RelayCommand]
    public override async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath)) return;

        var range = string.IsNullOrWhiteSpace(PageRange) ? "1-z" : PageRange;
        var spec = $"{RotationAngle}:{range}";

        await RunProcessTaskAsync((progress, ct) =>
            QpdfService.RotateAsync(InputPath, OutputPath, spec, InputPassword, progress, ct));
    }
}
