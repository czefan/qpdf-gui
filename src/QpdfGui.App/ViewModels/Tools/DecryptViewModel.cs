using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// PDF 文档解密与权限限制移除 ViewModel
/// 支持解除密码保护以及打印/编辑/复制等权限限制，输出不受限制的普通 PDF
/// </summary>
public partial class DecryptViewModel : SingleFileToolViewModel
{
    public DecryptViewModel(
        IQpdfService qpdfService,
        IDialogService dialogService,
        SettingsStore settingsStore)
        : base(qpdfService, dialogService, settingsStore)
    {
    }

    /// <inheritdoc />
    protected override void UpdateDefaultOutputPath()
    {
        OutputPath = ResolveOutputPathWithSuffix("decrypted");
    }

    /// <inheritdoc />
    public override void UpdateEquivalentCommand()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            EquivalentCommand = null;
            return;
        }

        var pwdArg = !string.IsNullOrWhiteSpace(InputPassword) ? $"--password=\"{InputPassword}\" " : "";
        EquivalentCommand = $"qpdf {pwdArg}\"{InputPath}\" --decrypt \"{OutputPath}\"";
    }

    /// <inheritdoc />
    [RelayCommand]
    public override async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath)) return;

        await RunProcessTaskAsync((progress, ct) =>
            QpdfService.DecryptAsync(InputPath, OutputPath, InputPassword, progress, ct));
    }
}
