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
        private ObservableCollection<ComboBoxItem> items = [];
        private List<string> supportedComponents = ["Zapret", "GoodbyeDPI", "ByeDPI"];
        public MainPage()
        {
            InitializeComponent();
            ComponentChooseComboBox.ItemsSource = items;
            GetReadyVariants();

        }
        private void GetReadyVariants()
        {
            items.Clear();
            foreach (var item in DatabaseHelper.Instance.GetItemsByType("component"))
            {
                ComboBoxItem comboBoxItem = new()
                {
                    Content = item.ShortName,
                    Tag = item.Id
                };
                items.Add(comboBoxItem);
            }
            if (items.Count > 0)
            {
                ComponentChooseComboBox.SelectedIndex = 0;
            }
            else
            {
                GoodCheckSelectionPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfigUtilWindow.Instance.Close();
        }

        private void BeginNewSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CreateViaGoodCheck), ((ComboBoxItem)ComponentChooseComboBox.SelectedItem).Tag, new SuppressNavigationTransitionInfo());
        }

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ComponentChooseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!supportedComponents.Contains(((ComboBoxItem)ComponentChooseComboBox.SelectedItem).Content))
            {
                GoodCheckSelectionPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                GoodCheckSelectionPanel.Visibility = Visibility.Visible;
            }
        }
    }
}
