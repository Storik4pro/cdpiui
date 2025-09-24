using CDPI_UI.Helper;
using CDPI_UI.Properties;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
using WinUI3Localizer;
using static CDPI_UI.Helper.Static.UIHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views
{
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
    public sealed partial class SettingsPage : Page
    {
        private ObservableCollection<ThemeSelectModel> themes = [];
        private ObservableCollection<LanguageSelectModel> languages = [];
        private ObservableCollection<ComboBoxModel> components = [];

        private ILocalizer localizer = Localizer.Get();
        public SettingsPage()
        {
            InitializeComponent();

            AutorunToggleSwitch.IsOn = SettingsManager.Instance.GetValue<bool>("SYSTEM", "autorun");
            SettingsManager.Instance.PropertyChanged += SettingsManager_PropertyChanged;

            ComponentComboBox.ItemsSource = components;
            CreateComponents();
            ComponentComboBox.SelectedItem = components.FirstOrDefault(x => x.Id == SettingsManager.Instance.GetValue<string>("COMPONENTS", "nowUsed"));
            ComponentComboBox.SelectionChanged += ComponentComboBox_SelectionChanged;

            ThemeComboBox.ItemsSource = themes;
            CreateThemes();
            ElementTheme theme = ((App)Application.Current).GetCurrentTheme();
            ThemeComboBox.SelectedItem = themes.FirstOrDefault(x => x.Id == theme);
            ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;

            LanguageComboBox.ItemsSource = languages;
            CreateLanguages();
            LanguageComboBox.SelectedItem = languages.FirstOrDefault(x => string.Equals(x.Id, localizer.GetCurrentLanguage(), StringComparison.OrdinalIgnoreCase));
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;

            ProcessStateToast.IsChecked = SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "procState");
            AppRunnedInTrayToast.IsChecked = SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "trayHide");
            AppUpdatesToast.IsChecked = SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "appUpdates");
            StoreUpdatesToast.IsChecked = SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "storeUpdates");
        }

        private void SettingsManager_PropertyChanged(string propertyName)
        {
            AutorunToggleSwitch.IsOn = SettingsManager.Instance.GetValue<bool>("SYSTEM", "autorun");
        }

        private void AutorunToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (SettingsManager.Instance.GetValue<bool>("SYSTEM", "autorun") != AutorunToggleSwitch.IsOn)
            {
                if (AutorunToggleSwitch.IsOn)
                {
                    AutoStartManager.AddToAutorun();
                }
                else
                {
                    AutoStartManager.RemoveFromAutorun();
                }
            }
        }

        private void ColorSelectorButton_Click(object sender, RoutedEventArgs e)
        {
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:personalization-colors"));
        }

        private void CreateComponents()
        {
            components.Clear();
            foreach (var component in DatabaseHelper.Instance.GetItemsByType("component"))
            {
                components.Add(new()
                {
                    Id = component.Id,
                    DisplayName = component.ShortName
                });
            }
        }

        private void CreateThemes()
        {
            themes.Add(new() 
            { 
                Id = ElementTheme.Dark,
                DisplayName = localizer.GetLocalizedString("DarkTheme")
            });
            themes.Add(new() 
            { 
                Id = ElementTheme.Light,
                DisplayName = localizer.GetLocalizedString("LightTheme")
            });
            themes.Add(new() 
            { 
                Id = ElementTheme.Default,
                DisplayName = localizer.GetLocalizedString("SystemTheme")
            });
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

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Content is FrameworkElement frameworkElement)
            {

                ElementTheme newTheme = ElementTheme.Default;

                newTheme = ((ThemeSelectModel)ThemeComboBox.SelectedItem).Id;
                frameworkElement.RequestedTheme = newTheme;

                SettingsManager.Instance.SetValue<string>("APPEARANCE", "Theme", newTheme.ToString());

                ((App)Application.Current).UpdateThemeForAllWindows(newTheme);
            }
        }

        private void ProcessStateToast_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue("NOTIFICATIONS", "procState", ProcessStateToast.IsChecked);
            _ = PipeClient.Instance.SendMessage("SETTINGS:RELOAD");
        }

        private void AppRunnedInTrayToast_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue("NOTIFICATIONS", "trayHide", AppRunnedInTrayToast.IsChecked);
            _ = PipeClient.Instance.SendMessage("SETTINGS:RELOAD");

        }

        private void AppUpdatesToast_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue("NOTIFICATIONS", "appUpdates", AppUpdatesToast.IsChecked);
            _ = PipeClient.Instance.SendMessage("SETTINGS:RELOAD");
        }

        private void StoreUpdatesToast_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SetValue("NOTIFICATIONS", "storeUpdates", StoreUpdatesToast.IsChecked);
            _ = PipeClient.Instance.SendMessage("SETTINGS:RELOAD");
        }

        private void ComponentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingsManager.Instance.SetValue<string>("COMPONENTS", "nowUsed", ((ComboBoxModel)ComponentComboBox.SelectedItem).Id);
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            localizer.SetLanguage(((LanguageSelectModel)LanguageComboBox.SelectedItem).Id);
            SettingsManager.Instance.SetValue<string>("SYSTEM", "language", ((LanguageSelectModel)LanguageComboBox.SelectedItem).Id);
        }
    }
}
