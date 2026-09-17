using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Controls.Default;

public sealed partial class PanelTitleUserControl : UserControl
{
    public static readonly DependencyProperty AdditionalContentProperty =
        DependencyProperty.Register(
            nameof(AdditionalContent),
            typeof(object),
            typeof(PanelTitleUserControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(PanelTitleUserControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CloseEnabledProperty =
        DependencyProperty.Register(
            nameof(CloseEnabled),
            typeof(bool),
            typeof(PanelTitleUserControl),
            new PropertyMetadata(true));

    public PanelTitleUserControl()
    {
        InitializeComponent();
    }

    public object AdditionalContent
    {
        get => GetValue(AdditionalContentProperty);
        set => SetValue(AdditionalContentProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool CloseEnabled
    {
        get => (bool)GetValue(CloseEnabledProperty);
        set => SetValue(CloseEnabledProperty, value);
    }

    public event RoutedEventHandler Click;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Click?.Invoke(this, e);
    }
}
