using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper;
using CDPIUI.Messages;
using CDPIUI.ViewModels;
using CDPIUI.Views.Store;
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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static WinUI3Localizer.LanguageDictionary;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.CreateConfigUtil
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : TemplatePage
    {
        private ObservableCollection<ViewComponentModel> items = [];
        private List<string> supportedComponents = ["Zapret", "GoodbyeDPI", "ByeDPI"];
        private List<string> ConfigTestUnsupportedComponents = ["TG WS Proxy"]; // TODO: Check for component Ids, not names

        private string TargetId = string.Empty;
        public MainPage()
        {
            InitializeComponent();
            ComponentChooseComboBox.ItemsSource = items;
            GetReadyVariants();

            StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;
            
        }

        private void StoreHelper_ItemActionsStopped(string obj)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (DatabaseHelper.Instance.GetItemById(obj)?.Type == "component")
                    GetReadyVariants();
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (Parameter != null)
            {
                TargetId = Parameter.Get("componentId");
                if (!string.IsNullOrEmpty(TargetId)) 
                    ComponentChooseComboBox.SelectedItem = 
                        items.FirstOrDefault(x => x.StoreId == TargetId) ?? items.First();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            StoreHelper.Instance.ItemActionsStopped -= StoreHelper_ItemActionsStopped;
        }


        private void GetReadyVariants()
        {
            items?.Clear();
            UIHelper.LoadInstalledComponentsList(items);
            if (items.Count > 0)
            {
                string lastSelection = SettingsManager.Instance.GetValue<string>("AUTOSELECTION", "lastComponentSelectedId");
                ComponentChooseComboBox.SelectedItem = items.FirstOrDefault(x => x.StoreId == TargetId) ?? items.FirstOrDefault(x => x.StoreId == lastSelection)?? items.First();


                PlaceholderGrid.Visibility = Visibility.Collapsed;
                WelcomePanel.Visibility = Visibility.Visible;
                MainPanel.Visibility = Visibility.Visible;
            }
            else
            {
                PlaceholderGrid.Visibility = Visibility.Visible;
                WelcomePanel.Visibility = Visibility.Collapsed;
                MainPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfigUtilWindow.Instance.Close();
        }

        private async void BeginNewSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!DatabaseHelper.Instance.IsItemInstalled("ASGKOI001"))
            {
                var window = await((App)Application.Current).UnsafeCreateNewWindow<StoreSmallDownloadDialog>(id: "ASGKOI001");
                return;
            }

            SettingsManager.Instance.SetValue<string>("AUTOSELECTION", "lastComponentSelectedId", ((ViewComponentModel)ComponentChooseComboBox.SelectedItem).StoreId);
            Frame.Navigate(typeof(CreateViaGoodCheck), 
                new NameValueCollection()
                {
                    {"componentId", ((ViewComponentModel)ComponentChooseComboBox.SelectedItem).StoreId }
                }, 
                new SuppressNavigationTransitionInfo());
        }

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {
            Commands.CommandsHandler.HandleCommand(
                "cdpiui://Help/Autoselection/1WhatAutoSelectionIs/");
        }

        private void ComponentChooseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!supportedComponents.Contains(((ViewComponentModel)ComponentChooseComboBox.SelectedItem)?.DisplayName))
            {
                BeginNewSelectionButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                BeginNewSelectionButton.Visibility = Visibility.Visible;
            }
            if (ConfigTestUnsupportedComponents.Contains(((ViewComponentModel)ComponentChooseComboBox.SelectedItem)?.DisplayName))
            {
                TestBestConfigSettingsCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                TestBestConfigSettingsCard.Visibility = Visibility.Visible;
            }
        }

        private async void ViewOtherButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue<string>("AUTOSELECTION", "lastComponentSelectedId", ((ViewComponentModel)ComponentChooseComboBox.SelectedItem).StoreId);
            var window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
            window.CreateNewConfigForComponentId((string)((ViewComponentModel)ComponentChooseComboBox.SelectedItem).StoreId);
            CreateConfigUtilWindow.Instance.Close();
        }

        private async void TestBestConfigSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue<string>("AUTOSELECTION", "lastComponentSelectedId", ((ViewComponentModel)ComponentChooseComboBox.SelectedItem).StoreId);
            var window = await((App)Application.Current).SafeCreateNewWindow<ConfigTestWindow>();
            window.ComponentIdToTest = ((ViewComponentModel)ComponentChooseComboBox.SelectedItem).StoreId;
            CreateConfigUtilWindow.Instance.Close();
        }

        private async void GetNewComponentsFromStoreButton_Click(object sender, RoutedEventArgs e)
        {
            var window =  await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(CategoryViewPage), new NameValueCollection() { { "categoryId", "C001CS" } }, new DrillInNavigationTransitionInfo());
        }
    }
}
