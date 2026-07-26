using CDPIUI.Controls.Default;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Specialized;
using Windows.Foundation.Metadata;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Troubleshooting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : TemplatePage
{
    private ILocalizer localizer = Localizer.Get();
    public MainPage()
    {
        InitializeComponent();

        IsBackwardAnimationToPageAvailable = true;
        ElementToAnimateBackwardConnectedAnimation = ActionButtonsGrid;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        try
        {
            PrepareToConnectedForwardAnimate(ActionButtonsGrid);            
        }
        catch { }
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
        Frame.Navigate(typeof(WorkPage), 
            new NameValueCollection() { {"action", NavigationParameters.BeginBasicCheck.ToString() } }, 
            new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
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
        Frame.Navigate(typeof(WorkPage),
            new NameValueCollection() { { "action", NavigationParameters.BeginStoreRepoCheck.ToString() } }
            , new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    private void ApplicationCannotDownloadUpdateCard_Click(object sender, RoutedEventArgs e)
    {

    }
}
