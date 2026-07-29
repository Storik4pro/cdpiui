using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.Communication;
using CDPIUI.Core.System;

using CDPIUI.Properties;
using CDPIUI.ViewModels;
using CDPIUI.Views.Settings;
using CDPIUI.Views.Store;
using CDPIUI.Views.Store.Settings;
using CDPIUI.Views.Store.Settings.Memory;
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;
using static CDPIUI.Helper.UIHelper;
using CDPIUI.Shared.Extentions;
using CDPIUI.Controls.Default;
using CDPIUI.Helper.Basic;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views
{
    public class GridColumnsCountModel
    {
        public int Count { get; set; }
        public string DisplayName { get; set; }
    }
    public class ThemeSelectModel
    {
        public ElementTheme Id { get; set; }
        public string DisplayName { get; set; }
    }
    public class LanguageSelectModel
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
    }
    public sealed partial class SettingsPage : TemplatePage
    {
        
        private ObservableCollection<LanguageSelectModel> languages = [];
        private ObservableCollection<ComboBoxModel> components = [];

        public static readonly List<Type> MainSettingsNavigationSupportedPages = [
            typeof(SettingsPage),
            typeof(PersonalizePage),
            typeof(AutorunPage)
            ];

        private ILocalizer localizer = Localizer.Get();
        public SettingsPage()
        {
            InitializeComponent();

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = NavGrid;
            IsBackwardAnimationToPageAvailable = true;
            ElementToAnimateBackwardConnectedAnimation = NavGrid;

            this.NavigationCacheMode = NavigationCacheMode.Disabled;


            

            LanguageComboBox.ItemsSource = languages;
            CreateLanguages();
            LanguageComboBox.SelectedItem = languages.FirstOrDefault(x => string.Equals(x.Id, localizer.GetCurrentLanguage(), StringComparison.OrdinalIgnoreCase));
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;

            

            ProcessStateToast.IsChecked = SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "procState");
            AppRunnedInTrayToast.IsChecked = SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "trayHide");
            AppUpdatesToast.IsChecked = SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "appUpdates");
            StoreUpdatesToast.IsChecked = SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "storeUpdates");
            ConditionalLaunchActionToast.IsChecked = SettingsManager.Instance.GetValueOrDefault<bool>(
                "NOTIFICATIONS",
                "conditionalLaunchActions",
                defaultValue: true);

            

            

            UpdateTextFileOpenSettings();

            BreadcrumbBar.ItemsSource = BreadcrumbBarModels;
            CreateBreadcrumbBarNavigation();
        }

        private ObservableCollection<BreadcrumbBarModel> BreadcrumbBarModels = [];
        public void CreateBreadcrumbBarNavigation()
        {
            BreadcrumbBarModels.Clear();
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("Settings"),
                Tag = this.GetType()
            });
        }

        private void PrepareAnimate()
        {
            PrepareToConnectedForwardAnimate(NavGrid);
        }

        private void UpdateTextFileOpenSettings()
        {
            int mode = SettingsManager.Instance.GetValue<int>("FILEOPENACTIONS", "mode");
            string appPath = SettingsManager.Instance.GetValue<string>("FILEOPENACTIONS", "applicationPath");
            OpenComponentSiteListToEditCard.Description =
                string.Format(localizer.GetLocalizedString(
                    "OpenComponentSiteListToEditCardDescription"),
                    mode == (int)TextFileOpenModes.UserChoose ? Path.GetFileNameWithoutExtension(appPath).FirstCharToUpper() : localizer.GetLocalizedString("FollowSystem"));
        }

        

        

        

        private void CreateLanguages()
        {
            languages.Add(new()
            {
                Id = "en-us",
                DisplayName = localizer.GetLocalizedString("en-us")
            });
            languages.Add(new()
            {
                Id = "ru",
                DisplayName = localizer.GetLocalizedString("ru")
            });
        }

        

        

        private void ProcessStateToast_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue("NOTIFICATIONS", "procState", ProcessStateToast.IsChecked);
            _ = PipeHelper.SendSettingsPacket(Shared.Pipe.Models.SettingsMessageIds.ReloadSettings);
        }

        private void AppRunnedInTrayToast_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue("NOTIFICATIONS", "trayHide", AppRunnedInTrayToast.IsChecked);
            _ = PipeHelper.SendSettingsPacket(Shared.Pipe.Models.SettingsMessageIds.ReloadSettings);

        }

        private void AppUpdatesToast_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue("NOTIFICATIONS", "appUpdates", AppUpdatesToast.IsChecked);
            _ = PipeHelper.SendSettingsPacket(Shared.Pipe.Models.SettingsMessageIds.ReloadSettings);
        }

        private void StoreUpdatesToast_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue("NOTIFICATIONS", "storeUpdates", StoreUpdatesToast.IsChecked);
            _ = PipeHelper.SendSettingsPacket(Shared.Pipe.Models.SettingsMessageIds.ReloadSettings);
        }

        private void ConditionalLaunchActionToast_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue(
                "NOTIFICATIONS",
                "conditionalLaunchActions",
                ConditionalLaunchActionToast.IsChecked);
            _ = PipeHelper.SendSettingsPacket(Shared.Pipe.Models.SettingsMessageIds.ReloadSettings);
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            localizer.SetLanguage(((LanguageSelectModel)LanguageComboBox.SelectedItem).Id);
            SettingsManager.Instance.SetValue<string>("SYSTEM", "language", ((LanguageSelectModel)LanguageComboBox.SelectedItem).Id);
        }
        

        private async void OpenComponentSiteListToEditCard_Click(object sender, RoutedEventArgs e)
        {
            EditSitelistAskApplicationContentDialog editSitelistAskApplicationContentDialog = new()
            {
                XamlRoot = this.XamlRoot
            };
            await editSitelistAskApplicationContentDialog.ShowAsync();
            UpdateTextFileOpenSettings();
        }

        private async void OpenStoreSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            var window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.SettingsPage), null, new DrillInNavigationTransitionInfo());
        }

        private void InstallFileAssociationsButton_Click(object sender, RoutedEventArgs e)
        {
            FileAssociationSettingsCard.IsEnabled = false;

            try
            {
                FileAssociationService.RegisterManually();
            }
            finally
            {
                FileAssociationSettingsCard.IsEnabled = true;
            }
        }

        

        private void PersonalizationSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            PrepareAnimate();

            Frame.Navigate(typeof(PersonalizePage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void AutorunSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            PrepareAnimate();

            Frame.Navigate(typeof(AutorunPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }
    }
}
