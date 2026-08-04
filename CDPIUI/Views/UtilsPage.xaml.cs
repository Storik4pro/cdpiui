using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Specialized;
using CDPIUI.Core;
using CDPIUI.Controls.Default;


namespace CDPIUI.Views
{
    public sealed partial class UtilsPage : TemplatePage
    {
        public UtilsPage()
        {
            this.InitializeComponent();
        }

        private void ConditionalLaunchSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<ConditionalLaunchWindow>();
        }

        private void PseudoconsoleSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<ViewWindow>();
        }

        private void StoreSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        }

        private void CreateConfigUtilSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
        }

        private void CreateConfigHelperSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
        }

        private void ConfigImportUtilSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<ConfigImportUtilWindow>();
        }

        private void OfflineHelpSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            Commands.CommandsHandler.HandleCommand("cdpiui://Help/");
        }

        private void ProxySettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<ProxySetupUtilWindow>();
        }

        private void TroubleshootinSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<TroubleshootingWindow>();
        }

        private void PresetTestSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<ConfigTestWindow>();
        }

        private void EditHostsFileSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<EditHostFileWindow>();
        }
    }
}
