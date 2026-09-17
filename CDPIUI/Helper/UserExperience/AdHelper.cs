using CDPIUI.Core;
using CDPIUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.Helper.UserExperience
{
    public enum AdType
    {
        DeveloperProjects,
        DeveloperAsk,
        Internal,
        TrustedServices
    }
    public class AdViewModel : INotifyPropertyChanged
    {
        public required AdType Type { get; set; }
        public string Name { get; set; }

        private bool isActive = false;
        public bool IsActive 
        {
            get => isActive;
            set
            {
                if (Equals(isActive, value))
                    return;

                SettingsManager.Instance.SetValue("AD", $"Show{Type}Ad", value);

                isActive = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static AdViewModel CreateFromType(AdType type)
        {
            ILocalizer localizer = Localizer.Get();
            return new() { Type = type, Name = localizer.GetLocalizedString($"{type}"), IsActive = SettingsManager.Instance.GetValue<bool>("AD", $"Show{type}Ad") };
        }
    }

    public class AdHelper : INotifyPropertyChanged
    {
        public static AdHelper Instance { get; } = new();

        public ObservableCollection<AdViewModel> AdSettings { get; } = [];

        private bool showAd = true;
        public bool ShowAd
        {
            get => showAd;
            set
            {
                if (Equals(showAd, value))
                    return;

                SettingsManager.Instance.SetValue("AD", $"ShowAd", value);

                showAd = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(ShowAd)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private AdHelper()
        {
            LoadAdSettings();

            ShowAd = SettingsManager.Instance.GetValue<bool>("AD", "ShowAd");
        }

        private void LoadAdSettings()
        {
            AdSettings.Clear();
            AdSettings.Add(AdViewModel.CreateFromType(AdType.DeveloperProjects));
            AdSettings.Add(AdViewModel.CreateFromType(AdType.DeveloperAsk));
            AdSettings.Add(AdViewModel.CreateFromType(AdType.Internal));
            AdSettings.Add(AdViewModel.CreateFromType(AdType.TrustedServices));
        }
    }
}
