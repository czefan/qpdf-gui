using QpdfGui.App.Services;
using QpdfGui.Core.Inspect;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// PDF 文档解密与权限限制移除 ViewModel
/// </summary>
public partial class DecryptViewModel : ToolViewModel
{
    public DecryptViewModel(
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
        OutputPath = OutputPathResolver.ResolveUniquePath(dir, InputPath, "decrypted");
    }

    public override QpdfJob? BuildJob()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath))
        {
            return null;
        }

        return new QpdfJob
        {
            InputFile = InputPath,
            OutputFile = OutputPath,
            Password = InputPassword,
            Decrypt = ""
        };
    }
}
