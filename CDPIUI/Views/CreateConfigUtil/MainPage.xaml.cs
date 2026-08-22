using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Data;
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
using WinUI3Localizer;
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
        private readonly ObservableCollection<ViewComponentModel> items = [];
        private readonly HashSet<string> supportedComponents =
        [
            .. HardcodedItemIds.GoodCheckSupportedComponents
                .Select(component => HardcodedItemIds.ComponentIds[component]),
            HardcodedItemIds.ComponentIds[Components.Zapret2],
        ];
        private readonly HashSet<string> configTestUnsupportedComponents =
        [
            HardcodedItemIds.ComponentIds[Components.TgWsProxy],
        ];
        private readonly ILocalizer localizer = Localizer.Get();
        private bool navigationActionHandled;

        private string TargetId = string.Empty;
        public MainPage()
        {
            InitializeComponent();
            ComponentChooseComboBox.ItemsSource = items;
            GetReadyVariants();

            StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;
            localizer.LanguageChanged += Localizer_LanguageChanged;


            IsBackwardAnimationToPageAvailable = true;
            ElementToAnimateBackwardConnectedAnimation = ActionButtonsGrid;
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

            if (Parameter != null && items.Count > 0)
            {
                TargetId = Parameter.Get("componentId");
                if (!string.IsNullOrEmpty(TargetId)) 
                    ComponentChooseComboBox.SelectedItem = 
                        items.FirstOrDefault(x => x.StoreId == TargetId) ?? items.First();

                string action = Parameter.Get("action");
                if (!navigationActionHandled && !string.IsNullOrWhiteSpace(action))
                {
                    navigationActionHandled = true;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (string.Equals(action, "BlockCheck2", StringComparison.OrdinalIgnoreCase))
                        {
                            NavigateToBlockCheck2(false);
                            Frame.BackStack.Clear();
                        }
                        else if (string.Equals(action, "BlockCheck2Reports", StringComparison.OrdinalIgnoreCase))
                        {
                            NavigateToBlockCheck2Reports();
                            Frame.BackStack.Clear();
                        }
                    });
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            StoreHelper.Instance.ItemActionsStopped -= StoreHelper_ItemActionsStopped;
            localizer.LanguageChanged -= Localizer_LanguageChanged;
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
            if (ComponentChooseComboBox.SelectedItem is not ViewComponentModel component)
            {
                return;
            }

            SettingsManager.Instance.SetValue<string>("AUTOSELECTION", "lastComponentSelectedId", component.StoreId);
            if (IsZapret2(component))
            {
                NavigateToBlockCheck2();
                return;
            }

            if (!DatabaseHelper.Instance.IsItemInstalled("ASGKOI001"))
            {
                var window = await((App)Application.Current).UnsafeCreateNewWindow<StoreSmallDownloadDialog>(id: "ASGKOI001");
                return;
            }

            Frame.Navigate(typeof(CreateViaGoodCheck), 
                new NameValueCollection()
                {
                    {"componentId", component.StoreId }
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
            UpdateSelectedComponentActions();
        }

        private void Localizer_LanguageChanged(object sender, LanguageChangedEventArgs e) =>
            UpdateSelectedComponentActions();

        private void UpdateSelectedComponentActions()
        {
            if (ComponentChooseComboBox.SelectedItem is not ViewComponentModel component)
            {
                BeginNewSelectionButton.Visibility = Visibility.Collapsed;
                BlockCheck2ReportsSettingsCard.Visibility = Visibility.Collapsed;
                return;
            }

            bool isZapret2 = IsZapret2(component);
            BeginNewSelectionButton.Visibility = supportedComponents.Contains(component.StoreId)
                ? Visibility.Visible
                : Visibility.Collapsed;
            BlockCheck2ReportsSettingsCard.Visibility = isZapret2
                ? Visibility.Visible
                : Visibility.Collapsed;
            TestBestConfigSettingsCard.Visibility = configTestUnsupportedComponents.Contains(component.StoreId)
                ? Visibility.Collapsed
                : Visibility.Visible;

            BeginNewSelectionButton.Header = localizer.GetLocalizedString(isZapret2
                ? "BlockCheck2StartSelectionCardTitle"
                : "CreateNewViaGoodCheckSettingsCardTitle");
            BeginNewSelectionButton.Description = localizer.GetLocalizedString(isZapret2
                ? "BlockCheck2StartSelectionCardDescription"
                : "CreateNewViaGoodCheckSettingsCardDescription");
        }

        private static bool IsZapret2(ViewComponentModel component) =>
            string.Equals(
                component.StoreId,
                HardcodedItemIds.ComponentIds[Components.Zapret2],
                StringComparison.OrdinalIgnoreCase);

        private void NavigateToBlockCheck2(bool animate = true)
        {
            if (animate) PrepareToConnectedForwardAnimate(ActionButtonsGrid);
            Frame.Navigate(typeof(Views.BlockCheck2.MainPage), new NameValueCollection() { }, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }
        private void NavigateToBlockCheck2Reports() =>
            Frame.Navigate(typeof(Views.BlockCheck2.ReportHistoryPage), new NameValueCollection() { }, new SuppressNavigationTransitionInfo());

        private void BlockCheck2ReportsSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            if (ComponentChooseComboBox.SelectedItem is ViewComponentModel component && IsZapret2(component))
            {
                SettingsManager.Instance.SetValue<string>("AUTOSELECTION", "lastComponentSelectedId", component.StoreId);
                NavigateToBlockCheck2Reports();
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
