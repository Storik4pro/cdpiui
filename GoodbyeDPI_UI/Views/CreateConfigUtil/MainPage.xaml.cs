using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.CreateConfigUtil
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<ViewComponentModel> items = [];
        private List<string> supportedComponents = ["Zapret", "GoodbyeDPI", "ByeDPI"];
        public MainPage()
        {
            InitializeComponent();
            ComponentChooseComboBox.ItemsSource = items;
            GetReadyVariants();

        }
        private void GetReadyVariants()
        {
            UIHelper.LoadInstalledComponentsList(items);
            if (items.Count > 0)
            {
                ComponentChooseComboBox.SelectedIndex = 0;
                PlaceholderGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                GoodCheckSelectionPanel.Visibility = Visibility.Collapsed;
                PlaceholderGrid.Visibility = Visibility.Visible;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfigUtilWindow.Instance.Close();
        }

        private void BeginNewSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CreateViaGoodCheck), ((ViewComponentModel)ComponentChooseComboBox.SelectedItem).StoreId, new SuppressNavigationTransitionInfo());
        }

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ComponentChooseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!supportedComponents.Contains(((ViewComponentModel)ComponentChooseComboBox.SelectedItem).DisplayName))
            {
                GoodCheckSelectionPanel.Visibility = Visibility.Collapsed;
                PlaceholderGrid.Visibility = Visibility.Visible;
            }
            else
            {
                GoodCheckSelectionPanel.Visibility = Visibility.Visible;
                PlaceholderGrid.Visibility = Visibility.Collapsed;
            }
        }

        private async void ViewOtherButton_Click(object sender, RoutedEventArgs e)
        {
            var window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
            window.CreateNewConfigForComponentId((string)((ViewComponentModel)ComponentChooseComboBox.SelectedItem).StoreId);
            CreateConfigUtilWindow.Instance.Close();
        }

        private void ViewHelpButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
