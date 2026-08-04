using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;
using CDPIUI.Views.CreateConfigUtil;
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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.CreateConfigHelper
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : TemplatePage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Frame.BackStack.Clear();
        }

        private void CreateNewConfigButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CreateNewConfigPage), null, new DrillInNavigationTransitionInfo());
        }

        private async void ImportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            await ((App)Application.Current).SafeCreateNewWindow<ConfigImportUtilWindow>();
            CreateConfigHelperWindow.Instanse?.Close();
        }

        private async void EditConfigButton_Click(object sender, RoutedEventArgs e)
        {
            SelectConfigToEditContentDialog dialog = new SelectConfigToEditContentDialog() 
            { 
                XamlRoot = this.Content.XamlRoot 
            };
            await dialog.ShowAsync();
            if (dialog.SelectedConfigResult == SelectResult.Selected)
            {
                ConfigItem configItem = dialog.SelectedConfigItem;
                Frame.Navigate(typeof(CreateNewConfigPage), 
                    new NameValueCollection()
                    {
                        { "type", "CFGEDIT" },
                        { "configItem", JSONConvertor.SerializeObject(configItem) }
                    }, 
                    new DrillInNavigationTransitionInfo());
            }
        }

        private async void GoodCheckRecentReportButton_Click(object sender, RoutedEventArgs e)
        {
            RecentGoodCheckSelectionsContentDialog dialog = new RecentGoodCheckSelectionsContentDialog()
            {
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
            if (dialog.SelectedResult == SelectResult.Selected)
            {
                string directory = dialog.SelectedReport;
                Frame.Navigate(typeof(ViewGoodCheckReportPage), 
                    new NameValueCollection()
                    {
                        { "type", NavigationState.LoadFileFromPath.ToString() },
                        { "filePath", directory }
                    }
                    , new DrillInNavigationTransitionInfo());
            }
        }

        private async void GoodCheckBeginSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfigUtilWindow window = await((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
            // window.NavigateToPage<CreateViaGoodCheck>();
        }

        private void StoreButton_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        }
    }
}
