using CDPI_UI.Helper.Troubleshooting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Troubleshooting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    private ILocalizer localizer = Localizer.Get();
    public MainPage()
    {
        InitializeComponent();
    }

    private async void ShowDialog(string message, string title)
    {
        var dlg = new MessageDialog(message, title);
        InitializeWithWindow.Initialize(dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<TroubleshootingWindow>()));
        await dlg.ShowAsync();
    }

    private async void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var window = await((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
        window.NavigateToPage("/Utils/TroubleshootingUtility");
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        var window = await ((App)Application.Current).SafeCreateNewWindow<TroubleshootingWindow>();
        window.Close();
    }

    private void NotOneConfigDoesWorkCard_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(WorkPage), NavigationParameters.BeginBasicCheck, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    private void MyProblemNotInListCard_Click(object sender, RoutedEventArgs e)
    {
        ShowDialog(localizer.GetLocalizedString("PreviewVersionDescription"), localizer.GetLocalizedString("PreviewVersion"));
    }

    private async void ComponentDoesNotRunCard_Click(object sender, RoutedEventArgs e)
    {
        var helpWindow = await ((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
        helpWindow.NavigateToPage("/TroubleshootingComponentexceptions/BasicTroubleshooting");

        var window = await((App)Application.Current).SafeCreateNewWindow<TroubleshootingWindow>();
        window.Close();
    }

    private void StoreCannotLoadCard_Click(object sender, RoutedEventArgs e)
    {

    }

    private void ApplicationCannotDownloadUpdateCard_Click(object sender, RoutedEventArgs e)
    {

    }
}
