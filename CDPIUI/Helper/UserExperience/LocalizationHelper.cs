using CDPIUI.Core;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using WinUI3Localizer;

namespace CDPIUI.Helper.UserExperience
{
    internal sealed class LocalizationHelper : INotifyPropertyChanged
    {
        public static LocalizationHelper Instance { get; } = new();

        private readonly ILocalizer localizer;
        private LanguageSelectModel currentLanguage;

        public ObservableCollection<LanguageSelectModel> Languages { get; } = [];

        public LanguageSelectModel CurrentLanguage
        {
            get => currentLanguage;
            private set
            {
                if (ReferenceEquals(currentLanguage, value))
                    return;

                currentLanguage = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private LocalizationHelper()
        {
            localizer = Localizer.Get();

            Languages.Add(new()
            {
                Id = "en-us",
                DisplayName = localizer.GetLocalizedString("en-us")
            });

            Languages.Add(new()
            {
                Id = "ru",
                DisplayName = localizer.GetLocalizedString("ru")
            });

            CurrentLanguage = FindLanguage(localizer.GetCurrentLanguage());
            localizer.LanguageChanged += Localizer_LanguageChanged;
        }

        private LanguageSelectModel FindLanguage(string id)
        {
            return Languages.FirstOrDefault(
                language => string.Equals(
                    language.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void Localizer_LanguageChanged(
            object sender,
            LanguageChangedEventArgs e)
        {
            CurrentLanguage = FindLanguage(e.CurrentLanguage);

            ApplicationInfo.Instance.SetLocalization(CurrentLanguage.Id);
        }

        public async Task SaveLanguageSettings(string id)
        {
            if (string.Equals(
                id,
                localizer.GetCurrentLanguage(),
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await localizer.SetLanguage(id);
            SettingsManager.Instance.SetValue<string>("SYSTEM", "language", id);
        }

        public async void HandleCollectionLanguageChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (e.AddedItems.FirstOrDefault() is LanguageSelectModel language)
                await SaveLanguageSettings(language.Id);
        }
    }
}
