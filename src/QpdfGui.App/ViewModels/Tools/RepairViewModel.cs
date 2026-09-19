using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// PDF 语法修复与 Web 线性化优化 ViewModel
/// 重新组织 PDF 交叉引用表与对象流，修复语法错误，并可启用线性化以实现网页“边下边看”快速视图
/// </summary>
public partial class RepairViewModel : SingleFileToolViewModel
{
    /// <summary>
    /// 是否在修复同时启用线性化（Linearize / Fast Web View）
    /// </summary>
    [ObservableProperty]
    private bool _linearize = true;

    public RepairViewModel(
        IQpdfService qpdfService,
        IDialogService dialogService,
        SettingsStore settingsStore)
        : base(qpdfService, dialogService, settingsStore)
    {
    }

    partial void OnLinearizeChanged(bool value) => UpdateEquivalentCommand();

    /// <inheritdoc />
    protected override void UpdateDefaultOutputPath()
    {
        OutputPath = ResolveOutputPathWithSuffix("repaired");
    }

    /// <inheritdoc />
    public override void UpdateEquivalentCommand()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            EquivalentCommand = null;
            return;
        }

        var linArg = Linearize ? " --linearize" : "";
        var pwdArg = !string.IsNullOrWhiteSpace(InputPassword) ? $"--password=\"{InputPassword}\" " : "";
        EquivalentCommand = $"qpdf {pwdArg}\"{InputPath}\" --object-streams=generate{linArg} \"{OutputPath}\"";
    }

    /// <inheritdoc />
    [RelayCommand]
    public override async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath)) return;

        await RunProcessTaskAsync((progress, ct) =>
            QpdfService.RepairAsync(InputPath, OutputPath, InputPassword, progress, ct));
    }
}
