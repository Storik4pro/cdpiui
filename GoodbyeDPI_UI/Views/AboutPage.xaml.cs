using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    
    public class AcknowledgmentsModel
    {
        public string Name { get; set; }
    }
    public sealed partial class AboutPage : Page
    {
        private enum UpdateButtonStatus
        {
            Ready,
            Work,
            ActionInstallAsk
        }

        private enum BadgeStatus
        {
            NewestInstalled,
            NewVersionAvailable,
            Work,
            Error
        }

        private ILocalizer localizer = Localizer.Get();

        private ObservableCollection<AcknowledgmentsModel> AcknowledgmentsList = new ObservableCollection<AcknowledgmentsModel>()
        {
            new () { Name = "Lux Fero" },
            new () { Name = "Lumenpearson" },
            new () { Name = "Leaftail1880" },
            new () { Name = "Nek0t" },
            new () { Name = "🔭" },
        };

        private ObservableCollection<AcknowledgmentsModel> RequrementsList = new ObservableCollection<AcknowledgmentsModel>()
        {
            new () { Name = "TextControlBox-WinUI by FrozenAssassine" },
            new () { Name = "WinUI3Localizer by Andrew KeepCoding" },
            new () { Name = "WinUIEx by Morten Nielsen" },
            new () { Name = "TaskScheduler by David Hall" },
            new () { Name = "Newtonsoft.Json by James Newton-King" },
            new () { Name = "CommunityToolkit.Labs" },
            new () { Name = "CommunityToolkit.WinUI" },
            new () { Name = "ini-parser by Ricardo Amores Hernández" },
            new () { Name = "Microsoft.Data.Sqlite" },
        };
        public AboutPage()
        {
            InitializeComponent();

            SetUpdateButtonStatus(ApplicationUpdateHelper.Instance.IsUpdateAvailable ? UpdateButtonStatus.ActionInstallAsk : UpdateButtonStatus.Ready);
            SetBadgeStatus(ApplicationUpdateHelper.Instance.IsUpdateAvailable ? BadgeStatus.NewVersionAvailable : BadgeStatus.NewestInstalled);
            SetCurrentVersion(StateHelper.Instance.Version);

            if (ApplicationUpdateHelper.Instance.ErrorHappened)
            {
                SetServerVersion(ApplicationUpdateHelper.Instance.ErrorInfo);
                SetBadgeStatus(BadgeStatus.Error);
            }
            else
            {
                SetServerVersion(ApplicationUpdateHelper.Instance.ServerVersion);
            }

            ApplicationUpdateHelper.Instance.ErrorHappens += ApplicationUpdateHelper_ErrorHappens;
            ApplicationUpdateHelper.Instance.CheckForUpdatesCompleted += ApplicationUpdateHelper_CheckForUpdatesCompleted;
            ApplicationUpdateHelper.Instance.CheckForUpdatesStarted += ApplicationUpdateHelper_CheckForUpdatesStarted;

            RepoRun.Text = UrlOpenHelper.MainRepoUrl;

            AcknowledgmentsListView.ItemsSource = AcknowledgmentsList;
            RequirementsListView.ItemsSource = RequrementsList;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string param)
            {
                if (param == "START_CHECK")
                {
                    UpdateButton_Click(null, null);
                }
            }
        }

        private void ApplicationUpdateHelper_ErrorHappens()
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                SetUpdateButtonStatus(ApplicationUpdateHelper.Instance.IsUpdateAvailable? UpdateButtonStatus.ActionInstallAsk : UpdateButtonStatus.Ready);
                SetBadgeStatus(BadgeStatus.Error);
                SetServerVersion(ApplicationUpdateHelper.Instance.ErrorInfo);
            });
        }

        private void ApplicationUpdateHelper_CheckForUpdatesStarted()
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                SetUpdateButtonStatus(UpdateButtonStatus.Work);
                SetBadgeStatus(BadgeStatus.Work);
            });
        }
        private void ApplicationUpdateHelper_CheckForUpdatesCompleted()
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                SetUpdateButtonStatus(ApplicationUpdateHelper.Instance.IsUpdateAvailable ? UpdateButtonStatus.ActionInstallAsk : UpdateButtonStatus.Ready);
                SetBadgeStatus(ApplicationUpdateHelper.Instance.IsUpdateAvailable ? BadgeStatus.NewVersionAvailable : BadgeStatus.NewestInstalled);
                SetServerVersion(ApplicationUpdateHelper.Instance.ServerVersion);
            });
        }

        private void SetUpdateButtonStatus(UpdateButtonStatus status)
        {
            UpdateButtonTextBlock.Opacity = 1;
            UpdateProgressRing.Visibility = Visibility.Collapsed;

            switch (status)
            {
                case UpdateButtonStatus.Ready:
                    UpdateButtonTextBlock.Text = localizer.GetLocalizedString("CheckForUpdates");
                    break;
                case UpdateButtonStatus.Work:
                    UpdateButtonTextBlock.Opacity = 0;
                    UpdateProgressRing.Visibility = Visibility.Visible;
                    break;
                case UpdateButtonStatus.ActionInstallAsk:
                    UpdateButtonTextBlock.Text = localizer.GetLocalizedString("InstallUpdates");
                    break;
            }
        }

        private void SetBadgeStatus(BadgeStatus status)
        {

            switch (status)
            {
                case BadgeStatus.NewestInstalled:
                    UpdateBadgeGlyph.Glyph = "\uE73E";
                    UpdateBadgeTextBlock.Text = localizer.GetLocalizedString("NewestVersionInstalled").ToUpper();
                    UpdateBadgeBorder.Background = UIHelper.HexToSolidColorBrushConverter("#4CA0E0");
                    break;
                case BadgeStatus.NewVersionAvailable:
                    UpdateBadgeGlyph.Glyph = "\uE752";
                    UpdateBadgeTextBlock.Text = localizer.GetLocalizedString("NewVersionAvailable").ToUpper();
                    UpdateBadgeBorder.Background = UIHelper.HexToSolidColorBrushConverter("#FFEB3B");
                    break;
                case BadgeStatus.Work:
                    UpdateBadgeGlyph.Glyph = "\uE895";
                    UpdateBadgeTextBlock.Text = localizer.GetLocalizedString("CheckingForUpdates").ToUpper();
                    UpdateBadgeBorder.Background = UIHelper.HexToSolidColorBrushConverter("#4CA0E0");
                    break;
                case BadgeStatus.Error:
                    UpdateBadgeGlyph.Glyph = "\uE783";
                    UpdateBadgeTextBlock.Text = localizer.GetLocalizedString("ErrorOccurred").ToUpper();
                    UpdateBadgeBorder.Background = UIHelper.HexToSolidColorBrushConverter("#F44336");
                    break;
            }
        }

        private void SetCurrentVersion(string version)
        {
            CurrentVersionTextBlock.Text = string.Format(localizer.GetLocalizedString("CurrentVersion"), version) + (Utils.IsApplicationBuildAsMsi ? " MSI-BUILD" : " PORTABLE-BUILD");
        }
        private void SetServerVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                ServerVersionTextBlock.Visibility = Visibility.Collapsed;
            }
            else if (version.StartsWith("ERR_"))
            {
                ServerVersionTextBlock.Visibility = Visibility.Visible;
                ServerVersionTextBlock.Text = localizer.GetLocalizedString("ErrorOccurred") + " " + version;
            }
            else
            {
                ServerVersionTextBlock.Visibility = Visibility.Visible;
                ServerVersionTextBlock.Text = string.Format(localizer.GetLocalizedString("ServerVersion"), version);
            }


        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ApplicationUpdateHelper.Instance.IsUpdateAvailable)
            {
                SetUpdateButtonStatus(UpdateButtonStatus.Work);
                SetBadgeStatus(BadgeStatus.Work);
                StoreHelper.Instance.AddItemToQueue(StateHelper.ApplicationStoreId, ApplicationUpdateHelper.Instance.ServerVersion);
            }
            else
            {
                SetUpdateButtonStatus(UpdateButtonStatus.Work);
                SetBadgeStatus(BadgeStatus.Work);
                bool result = await ApplicationUpdateHelper.Instance.CheckForUpdates();
            }
        }

        private void LicenseButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchLicenseUrl();
        }

        private void RepoHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            UrlOpenHelper.LaunchMainRepoUrl();
        }

        private void ReportABugButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchReportUrl();
        }

        private void DonateButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchDonateUrl();
        }

        private void DeveloperChannelButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchTelegramUrl();
        }
    }
}
