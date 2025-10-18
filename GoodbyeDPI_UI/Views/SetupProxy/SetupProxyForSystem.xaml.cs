using CDPI_UI.Helper;
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
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.SetupProxy;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SetupProxyForSystem : Page
{
    private ILocalizer localizer = Localizer.Get();
    public SetupProxyForSystem()
    {
        InitializeComponent();

        SetSettings();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
        if (anim != null)
        {
            anim.TryStart(ActionButtonsGrid);
        }
    }

    private void GoBackButton_Click(object sender, RoutedEventArgs e)
    {
        var anim = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("BackwardConnectedAnimation", ActionButtonsGrid);

        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
        {
            anim.Configuration = new DirectConnectedAnimationConfiguration();
        }
        if (Frame.CanGoBack)
            Frame.GoBack();
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

    private void SetSettings() 
    {
        Dictionary<string, string> currentSettings = Helper.Static.RegeditHelper.ReadProxySettings();

        IPValue.Text = (currentSettings["ProxyServer"].Split("]:").Length >= 2 ? 
            currentSettings["ProxyServer"].Split("]:")[0] : 
            currentSettings["ProxyServer"].Split(":")[0])
            .Replace("[", "").Replace("]", "").Replace("socks=", "");
        PortValue.Text = currentSettings["ProxyServer"].Split("]:").Length >= 2 ?
            currentSettings["ProxyServer"].Split("]:")[1] :
            currentSettings["ProxyServer"].Split(":")[1];

        BypassLocalCheckBox.IsChecked = currentSettings["ProxyOverride"].Contains("<local>");

        WhiteListTextBox.Text = currentSettings["ProxyOverride"]
            .Replace(";<local>", "")
            .Replace("<local>", "");
    }

    private bool SaveSettings()
    {
        string proxyServer = "";

        try
        {
            IPAddress address;
            if (IPAddress.TryParse(IPValue.Text, out address))
            {
                proxyServer = address.AddressFamily switch
                {
                    System.Net.Sockets.AddressFamily.InterNetwork => $"socks={IPValue.Text}:{PortValue.Text}",
                    System.Net.Sockets.AddressFamily.InterNetworkV6 => $"socks=[{IPValue.Text}]:{PortValue.Text}",
                    _ => "",
                };
            }

            if (string.IsNullOrEmpty(proxyServer)) throw new ArgumentOutOfRangeException("ERR_INVALID_IP");

            string proxyOverride = WhiteListTextBox.Text.Replace(" ", "");
            if (BypassLocalCheckBox.IsChecked == true)
            {
                if (string.IsNullOrEmpty(proxyOverride))
                    proxyOverride = "<local>";
                else
                    proxyOverride += $";<local>";
            }
            Helper.Static.RegeditHelper.SaveProxySettings(proxyServer, proxyOverride, 0);

            SettingsManager.Instance.SetValue("PROXY", "IPAddress", IPValue.Text);
            SettingsManager.Instance.SetValue("PROXY", "port", PortValue.Text);
            SettingsManager.Instance.SetValue("PROXY", "proxyType", StateHelper.ProxySetupTypes.AllSystem.ToString());

            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.CreateErrorLog(nameof(Helper.Static.RegeditHelper), ex.Message);
            _ = new ContentDialog()
            {
                Title = localizer.GetLocalizedString("ErrorOccurred"),
                Content = string.Format(localizer.GetLocalizedString("ExceptionSystemProxySet"), $"HKEY_CURRENT_USER/{Helper.Static.RegeditHelper.InternetSettingsKeyPath}", ex.Message),
                CloseButtonText = "OK",
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
        return false;
    }

    private void GoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (SaveSettings())
            Navigate<ProxySetupCompletePage>();
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: open help
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        IPValue.Text = "127.0.0.1";
        PortValue.Text = "1080";
    }
}
