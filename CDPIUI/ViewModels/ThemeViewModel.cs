using CDPIUI.Core;
using CDPIUI.Helper;
using CDPIUI.Helper.UserExperience;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;

namespace CDPIUI.ViewModels
{
    public class ThemeViewModel
    {
        public Guid Guid { get; set; }
        public ElementTheme FriendlyThemeId { get; set; } // Will be removed soon.

        public string Name { get; set; }
        public string Description { get; set; }

        public bool ShowDescription { get => !string.IsNullOrEmpty(Description); }

        public string FirstBackgroundColorHEX { get; set; } = "#000000";
        public Brush FirstBackgrounBrush { get => UIHelper.HexToSolidColorBrushConverter(FirstBackgroundColorHEX); }
        public string SecondBackgroundColorHEX { get; set; } = "#000000";
        public Brush SecondBackgrounBrush { get => UIHelper.HexToSolidColorBrushConverter(SecondBackgroundColorHEX); }

        public ImageSource ImageSource;
    }

    public class ApplicationThemeManager : INotifyPropertyChanged
    {
        private static ApplicationThemeManager _instance;
        private static readonly object _lock = new();
        public static ApplicationThemeManager Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new ApplicationThemeManager();
                    return _instance;
                }
            }
        }

        public readonly ObservableCollection<ThemeViewModel> Themes = [];

        public ApplicationThemeManager()
        {
            LoadThemes();

            ElementTheme theme = ((App)Application.Current).GetCurrentTheme();
            CurrentTheme = Themes.FirstOrDefault(x => x.FriendlyThemeId == theme);
        }

        private ThemeViewModel currentTheme;
        public ThemeViewModel CurrentTheme
        {
            get => currentTheme;
            private set
            {
                if (ReferenceEquals(currentTheme, value))
                    return;

                currentTheme = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(CurrentTheme)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void LoadThemes()
        {
            ILocalizer localizer = Localizer.Get();

            Themes.Clear();
            Themes.Add(new()
            {
                Guid = new Guid(),
                FriendlyThemeId = ElementTheme.Dark,
                Name = localizer.GetLocalizedString("DarkTheme"),
                FirstBackgroundColorHEX = "#000000",
                SecondBackgroundColorHEX = "#000000",
            });
            Themes.Add(new()
            {
                Guid = new Guid(),
                FriendlyThemeId = ElementTheme.Light,
                Name = localizer.GetLocalizedString("LightTheme"),
                FirstBackgroundColorHEX = "#F3F3F3",
                SecondBackgroundColorHEX = "#F3F3F3",
            });
            Themes.Add(new()
            {
                Guid = new Guid(),
                FriendlyThemeId = ElementTheme.Default,
                Name = localizer.GetLocalizedString("SystemTheme"),
                FirstBackgroundColorHEX = "#000000",
                SecondBackgroundColorHEX = "#F3F3F3",
            });
        }

        public void SaveThemeSettings(ThemeViewModel model)
        {
            if (model == CurrentTheme)
            {
                return;
            }

            ElementTheme newTheme = ElementTheme.Default;

            newTheme = model.FriendlyThemeId;

            CurrentTheme = model;
            SettingsManager.Instance.SetValue<string>("APPEARANCE", "Theme", newTheme.ToString());

            ((App)Application.Current).UpdateThemeForAllWindows(newTheme);

            
        }

        public void HandleCollectionThemeChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is ThemeViewModel model)
                SaveThemeSettings(model);
        }
    }
}
