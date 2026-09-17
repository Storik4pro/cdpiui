using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Controls.Dialogs.ConfigShare;

public sealed partial class ConfigShareMessageContentDialog : ContentDialog
{
    public ConfigShareMessageContentDialog()
    {
        InitializeComponent();
    }

    public string Message
    {
        get => MessageTextBlock.Text;
        set => MessageTextBlock.Text = value;
    }
}
