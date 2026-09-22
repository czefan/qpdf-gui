using Avalonia;
using Avalonia.Controls;

namespace QpdfGui.App.Views.Controls;

public partial class OutputCard : UserControl
{
    public static readonly StyledProperty<string> FileNameLabelProperty =
        AvaloniaProperty.Register<OutputCard, string>(nameof(FileNameLabel), "Output_FileNameLabel");

    public static readonly StyledProperty<string> FileNamePlaceholderProperty =
        AvaloniaProperty.Register<OutputCard, string>(nameof(FileNamePlaceholder), string.Empty);

    public string FileNameLabel
    {
        get => GetValue(FileNameLabelProperty);
        set => SetValue(FileNameLabelProperty, value);
    }

    public string FileNamePlaceholder
    {
        get => GetValue(FileNamePlaceholderProperty);
        set => SetValue(FileNamePlaceholderProperty, value);
    }

    public OutputCard()
    {
        InitializeComponent();
    }
}
