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
            CurrentVersionTextBlock.Text = string.Format(localizer.GetLocalizedString("CurrentVersion"), version);
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

                SetUpdateButtonStatus(result ? UpdateButtonStatus.ActionInstallAsk : UpdateButtonStatus.Ready);
                SetBadgeStatus(result ? BadgeStatus.NewVersionAvailable : BadgeStatus.NewestInstalled);
                SetServerVersion(ApplicationUpdateHelper.Instance.ServerVersion);
            }
        }
    }
}
