using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Basic;
using CDPIUI.Default;
using Microsoft.UI.Xaml;
using System;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class ServiceWindow : TemplateWindow
{
    public ServiceWindow()
    {
        InitializeComponent();
        var localizer = Localizer.Get();
        WindowTitle = localizer.GetLocalizedString("ServiceWindowTitle");
        Heading.Text = localizer.GetLocalizedString("ConfirmationRequired");
        Description.Text = localizer.GetLocalizedString("ServiceAskToStopMessage");
        StopButton.Content = localizer.GetLocalizedString("YesStopService");
        CancelButton.Content = localizer.GetLocalizedString("Cancel");
        IconUri = @"Assets/favicon.ico";
        CustomTitleBarUserControl = TitleBarControl;
        DisableResizeFeature();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        ErrorBar.IsOpen = false;
        try
        {
            await ProcessService.StopService();
            Close();
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateErrorLog(nameof(ServiceWindow), exception.ToString());
            ErrorBar.Message = string.Format(Localizer.Get().GetLocalizedString("ServiceStopException"), "WINDIVERT_STOP_ERROR");
            ErrorBar.IsOpen = true;
            StopButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
