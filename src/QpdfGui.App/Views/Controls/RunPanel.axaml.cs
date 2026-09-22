using Avalonia;
using Avalonia.Controls;

namespace QpdfGui.App.Views.Controls;

public partial class RunPanel : UserControl
{
    public static readonly StyledProperty<object?> ActionContentProperty =
        AvaloniaProperty.Register<RunPanel, object?>(nameof(ActionContent));

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public RunPanel()
    {
        InitializeComponent();
    }
}
