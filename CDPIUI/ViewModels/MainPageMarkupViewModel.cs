using CDPIUI.Core;
using CDPIUI.Helper;
using CDPIUI.Helper.UserExperience;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.ViewModels
{
    public enum MarkupTypes
    {
        Classic,
        Modern
    }
    public class MainPageMarkupViewModel
    {
        public string Name { get; set; }

        public string Description { get; set; }
        
        public MarkupTypes Type { get; set; }

        public ImageSource ImageSource { get; set; }
    }

    public partial class MainPageMarkupManager : INotifyPropertyChanged
    {
        public static MainPageMarkupManager Instance { get; } = new();

        public readonly ObservableCollection<MainPageMarkupViewModel> Markups = [];

        public bool IsModernMarkupSelected { get => CurrentMarkup.Type == MarkupTypes.Modern; }

        public MainPageMarkupManager()
        {
            LoadMarkups();

            CurrentMarkup = Markups.FirstOrDefault(x => x.Type.ToString() == SettingsManager.Instance.GetValue<string>("APPEARANCE", "mainPageMarkup"));
        }

        private MainPageMarkupViewModel currentMarkup;
        public MainPageMarkupViewModel CurrentMarkup
        {
            get => currentMarkup;
            private set
            {
                if (ReferenceEquals(currentMarkup, value))
                    return;

                currentMarkup = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(CurrentMarkup)));
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(IsModernMarkupSelected)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void LoadMarkups()
        {
            ILocalizer localizer = Localizer.Get();

            Markups.Clear();
            Markups.Add(new()
            {
                Type = MarkupTypes.Classic,
                Name = localizer.GetLocalizedString("ClassicMarkup"),
                Description = localizer.GetLocalizedString("ClassicMarkupDescription"),
                ImageSource = new BitmapImage(UIHelper.GetUriFromString("ms-appx:///Assets/Preview/MainPageMarkup/ClassicMarkup.png"))
            });
            Markups.Add(new()
            {
                Type = MarkupTypes.Modern,
                Name = localizer.GetLocalizedString("ModernMarkup"),
                Description = localizer.GetLocalizedString("ModernMarkupDescription"),
                ImageSource = new BitmapImage(UIHelper.GetUriFromString("ms-appx:///Assets/Preview/MainPageMarkup/ModernMarkup.png"))
            });
        }

        public void SaveMarkupSettings(MainPageMarkupViewModel model)
        {
            if (model == CurrentMarkup)
            {
                return; 
            }

            CurrentMarkup = model;
            SettingsManager.Instance.SetValue<string>("APPEARANCE", "mainPageMarkup", model.Type.ToString());
        }

        public void HandleCollectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is MainPageMarkupViewModel model)
                SaveMarkupSettings(model);
        }
    }
}
