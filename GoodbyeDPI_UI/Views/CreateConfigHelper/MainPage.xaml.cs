using GoodbyeDPI_UI.Controls.Dialogs.CreateConfigHelper;
using GoodbyeDPI_UI.Helper.Items;
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
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Views.CreateConfigHelper
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void CreateNewConfigButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CreateNewConfigPage), null, new DrillInNavigationTransitionInfo());
        }

        private async void ImportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            ImportConfigFromFileDialog dialog = new ImportConfigFromFileDialog() { XamlRoot = this.Content.XamlRoot };
            await dialog.ShowAsync();

            string filePath;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Choose config file";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                openFileDialog.FilterIndex = 4;

                openFileDialog.Filter = "JSON configs (*.json)|*.json|BAT config files (*.bat)|*.bat|CMD config files (*.cmd)|*.cmd|All compacible config files (*.bat, *.cmd, *.json)|*.bat;*.cmd;*.json";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                    var (configItem, errorHappens) = ConfigHelper.LoadConfigFromFile(filePath);
                    Frame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGIMPORT", configItem, errorHappens, filePath), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    return;
                }
            }
        }

        private async void EditConfigButton_Click(object sender, RoutedEventArgs e)
        {
            SelectConfigToEditContentDialog dialog = new SelectConfigToEditContentDialog() 
            { 
                XamlRoot = this.Content.XamlRoot 
            };
            await dialog.ShowAsync();
        }
    }
}
