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
using CDPI_UI.Helper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UtilsPage : Page
    {
        public UtilsPage()
        {
            this.InitializeComponent();

            CreateConfigUtilSettingsCard.IsEnabled = DatabaseHelper.Instance.IsItemInstalled(StateHelper.Instance.FindKeyByValue("GoodCheck"));
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

        private void OfflineHelpSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
        }

        private void ProxySettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<ProxySetupUtilWindow>();
        }

        private void TroubleshootinSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<TroubleshootingWindow>();
        }
    }
}
