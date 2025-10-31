using CDPI_UI.Helper;
using CDPI_UI.Messages;
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
using Windows.Foundation.Metadata;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.SetupProxy;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackwardConnectedAnimation");
        if (anim != null)
        {
            anim.TryStart(ActionButtonsGrid);
        }
    }

    private void SetupForSystemCard_Click(object sender, RoutedEventArgs e)
    {
        Navigate<SetupProxyForSystem>();
    }

    private void Navigate<T>() where T : Page
    {
        var anim = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ForwardConnectedAnimation", ActionButtonsGrid);

        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
        {
            anim.Configuration = new DirectConnectedAnimationConfiguration();
        }

        Frame.Navigate(typeof(T), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).CloseWindow<ProxySetupUtilWindow>();
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Open help link
    }

    private void SetupManuallyCard_Click(object sender, RoutedEventArgs e)
    {
        Navigate<ManualSetupPage>();
    }

    private async void ProxiFyreCard_Click(object sender, RoutedEventArgs e)
    {
        if (!DatabaseHelper.Instance.IsItemInstalled("ASPEWK002"))
        {
            var window = await ((App)Application.Current).SafeCreateNewWindow<StoreSmallDownloadDialog>();
            window.SetItemToViewId("ASPEWK002");
        }
        else
        {
            Navigate<ProxiFyreSetupPage>();
        }
    }
}
