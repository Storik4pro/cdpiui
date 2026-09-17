using CDPIUI.Commands;
using CDPIUI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Controls.Dialogs.Store;

public sealed partial class DownloadErrorActionsContentDialog : ContentDialog
{
    public DownloadErrorActionsContentDialog() => InitializeComponent();

    private void Troubleshooting_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        CommandsHandler.HandleCommand("cdpiui://Tools/Troubleshooting");
    }

    private void Support_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        UrlOpenHelper.LaunchReportUrl();
    }

    private void NetworkHelp_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        CommandsHandler.HandleCommand("cdpiui://Help/Store/FixDatabaseLoadIssue/");
    }
}
