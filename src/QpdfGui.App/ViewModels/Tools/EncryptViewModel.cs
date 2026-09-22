using CommunityToolkit.Mvvm.ComponentModel;
using QpdfGui.App.Services;
using QpdfGui.Core.Inspect;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Process;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// PDF 权限与密码加密 ViewModel
/// </summary>
public partial class EncryptViewModel : ToolViewModel
{
    [ObservableProperty]
    private string? _userPassword;

    [ObservableProperty]
    private string? _ownerPassword;

    [ObservableProperty]
    private bool _allowPrinting = true;

    [ObservableProperty]
    private bool _allowModification = true;

    [ObservableProperty]
    private bool _allowExtraction = true;

    [ObservableProperty]
    private bool _allowAnnotations = true;

    partial void OnUserPasswordChanged(string? value) => UpdateEquivalentCommand();
    partial void OnOwnerPasswordChanged(string? value) => UpdateEquivalentCommand();
    partial void OnAllowPrintingChanged(bool value) => UpdateEquivalentCommand();
    partial void OnAllowModificationChanged(bool value) => UpdateEquivalentCommand();
    partial void OnAllowExtractionChanged(bool value) => UpdateEquivalentCommand();
    partial void OnAllowAnnotationsChanged(bool value) => UpdateEquivalentCommand();

    public EncryptViewModel(
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
        OutputPath = OutputPathResolver.ResolveUniquePath(dir, InputPath, "encrypted");
    }

    private EncryptOptions? BuildNormalizedOptions()
    {
        var raw = new EncryptOptions
        {
            UserPassword = UserPassword,
            OwnerPassword = OwnerPassword,
            Aes256 = new Encrypt256BitOptions
            {
                Print = AllowPrinting ? "full" : "none",
                Modify = AllowModification ? "all" : "none",
                Extract = AllowExtraction ? "y" : "n",
                Annotate = AllowAnnotations ? "y" : "n"
            }
        };
        return raw.Normalize();
    }

    public override QpdfJob? BuildJob()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath))
        {
            return null;
        }

        var options = BuildNormalizedOptions();
        if (options == null)
        {
            return null;
        }

        return new QpdfJob
        {
            InputFile = InputPath,
            OutputFile = OutputPath,
            Password = InputPassword,
            Encrypt = options
        };
    }
}
