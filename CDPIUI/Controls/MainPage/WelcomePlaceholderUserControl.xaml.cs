using CDPI_UI.Helper.Static;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

namespace CDPI_UI.Controls.MainPage;

public sealed partial class WelcomePlaceholderUserControl : UserControl
{
    private ILocalizer localizer = Localizer.Get();
    public WelcomePlaceholderUserControl()
    {
        InitializeComponent();

        LearnMoreAboutUIHyperlink.Content = localizer.GetLocalizedString("/Help/LearnMoreAboutUI");
        FirstStepsHyperlink.Content = localizer.GetLocalizedString("/Help/FirstSteps");
        AddingCustomSiteListsToConfigHyperlink.Content = localizer.GetLocalizedString("/Help/AddingCustomSiteListsToConfig");

        StarsFontIcon.Glyph = Utils.IsOsSupportedNewGlyph() ? "\uF4A5" : "\uE8B0";
    }

    private async void ShowDialog(string message, string title)
    {
        var dlg = new MessageDialog(message, title);
        InitializeWithWindow.Initialize(dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>()));
        await dlg.ShowAsync();
    }

    private void ApplicationSetupHelperButton_Click(object sender, RoutedEventArgs e)
    {
        ShowDialog(localizer.GetLocalizedString("PreviewVersionDescription"), localizer.GetLocalizedString("PreviewVersion"));
    }

    private async void GetNewComponentsFromStoreButton_Click(object sender, RoutedEventArgs e)
    {
        await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
    }

    private async void NavigateToHelpUri(string uri)
    {
        OfflineHelpWindow window = await ((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
        window.NavigateToPage(uri);
    }

    private void LearnMoreAboutUIHyperlink_Click(object sender, RoutedEventArgs e)
    {
        NavigateToHelpUri("/GettingStarted/LearnMoreAboutUI");
    }

    private void FirstStepsHyperlink_Click(object sender, RoutedEventArgs e)
    {
        NavigateToHelpUri("/GettingStarted/FirstSteps");
    }

    private void AddingCustomSiteListsToConfigHyperlink_Click(object sender, RoutedEventArgs e)
    {
        NavigateToHelpUri("/GettingStarted/AddingCustomSiteListsToConfig");
    }
}
