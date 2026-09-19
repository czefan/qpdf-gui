using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QpdfGui.App.Services;
using QpdfGui.Core.Jobs;
using QpdfGui.Core.Services;

namespace QpdfGui.App.ViewModels.Tools;

/// <summary>
/// PDF 权限与密码加密 ViewModel
/// 采用高强度 AES-256 标准，支持配置用户打开密码、拥有者管理密码以及打印/编辑/内容复制/批注等精细权限
/// </summary>
public partial class EncryptViewModel : SingleFileToolViewModel
{
    /// <summary>
    /// 文档打开密码（用户密码）；若设置则打开该 PDF 时必须输入此密码
    /// </summary>
    [ObservableProperty]
    private string? _userPassword;

    /// <summary>
    /// 权限/拥有者管理密码；若设置则用于保护权限不被擅自更改
    /// </summary>
    [ObservableProperty]
    private string? _ownerPassword;

    /// <summary>
    /// 是否允许打印该文档
    /// </summary>
    [ObservableProperty]
    private bool _allowPrinting = true;

    /// <summary>
    /// 是否允许修改文档内容
    /// </summary>
    [ObservableProperty]
    private bool _allowModification = true;

    /// <summary>
    /// 是否允许复制文本与图像（提取内容）
    /// </summary>
    [ObservableProperty]
    private bool _allowExtraction = true;

    /// <summary>
    /// 是否允许添加批注与表单填写
    /// </summary>
    [ObservableProperty]
    private bool _allowAnnotations = true;

    public EncryptViewModel(
        IQpdfService qpdfService,
        IDialogService dialogService,
        SettingsStore settingsStore)
        : base(qpdfService, dialogService, settingsStore)
    {
    }

    /// <inheritdoc />
    protected override void UpdateDefaultOutputPath()
    {
        OutputPath = ResolveOutputPathWithSuffix("encrypted");
    }

    /// <inheritdoc />
    public override void UpdateEquivalentCommand()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            EquivalentCommand = null;
            return;
        }

        var uPwd = !string.IsNullOrWhiteSpace(UserPassword) ? UserPassword : "";
        var oPwd = !string.IsNullOrWhiteSpace(OwnerPassword) ? OwnerPassword : "owner";
        var print = AllowPrinting ? "full" : "none";
        var modify = AllowModification ? "all" : "none";
        var extract = AllowExtraction ? "y" : "n";
        var annotate = AllowAnnotations ? "y" : "n";

        var pwdArg = !string.IsNullOrWhiteSpace(InputPassword) ? $"--password=\"{InputPassword}\" " : "";
        EquivalentCommand = $"qpdf {pwdArg}\"{InputPath}\" --encrypt \"{uPwd}\" \"{oPwd}\" 256 --print={print} --modify={modify} --extract={extract} --annotate={annotate} -- \"{OutputPath}\"";
    }

    /// <inheritdoc />
    [RelayCommand]
    public override async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || string.IsNullOrWhiteSpace(OutputPath)) return;

        var options = new EncryptOptions
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

        await RunProcessTaskAsync((progress, ct) =>
            QpdfService.EncryptAsync(InputPath, OutputPath, options, InputPassword, progress, ct));
    }
}
