
using CDPIUI.Core.Store;
using CDPIUI.Shared;
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

namespace CDPIUI.Controls.MainPage;

public sealed partial class WelcomePlaceholderUserControl : UserControl
{
    private ILocalizer localizer = Localizer.Get();
    public WelcomePlaceholderUserControl()
    {
        InitializeComponent();

        LearnMoreAboutUIHyperlink.Content = localizer.GetLocalizedString("/Help/LearnMoreAboutUI");
        FirstStepsHyperlink.Content = localizer.GetLocalizedString("/Help/FirstSteps");
        AddingCustomSiteListsToConfigHyperlink.Content = localizer.GetLocalizedString("/Help/AddingCustomSiteListsToConfig");

        StoreHelper.Instance.QueueUpdated += Store_QueueUpdated;
        Unloaded += WelcomePlaceholderUserControl_Unloaded;
    }

    private void WelcomePlaceholderUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        StoreHelper.Instance.QueueUpdated -= Store_QueueUpdated;
        Unloaded -= WelcomePlaceholderUserControl_Unloaded;
    }

    private void Store_QueueUpdated()
    {
        CheckStoreQueue();
    }

    private void CheckStoreQueue()
    {
        if (StoreHelper.Instance.GetQueue().Count > 0)
        {
            LoadingPlaceholder.Visibility = Visibility.Visible;
        }
        else
        {
            LoadingPlaceholder.Visibility = Visibility.Collapsed;
        }
    }

    private async void ShowDialog(string message, string title)
    {
        var dlg = new MessageDialog(message, title);
        InitializeWithWindow.Initialize(dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>()));
        await dlg.ShowAsync();
    }

    private async void GetNewComponentsFromStoreButton_Click(object sender, RoutedEventArgs e)
    {
        await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
    }

    private void NavigateToHelpUri(string uri)
    {
        Commands.CommandsHandler.HandleCommand(
            $"cdpiui://Help/{uri.Trim('/')}/");
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
