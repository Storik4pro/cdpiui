using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Helper.Parsers;

using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Store.Settings.Memory
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MemoryViewSettingsDetailsPage : TemplatePage
    {
        private ILocalizer localizer = Localizer.Get();
        public MemoryViewSettingsDetailsPage()
        {
            InitializeComponent();

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = NavGrid;

            BreadcrumbBar.ItemsSource = BreadcrumbBarModels;

            CreateBreadcrumbBarNavigation();

            CalcSize();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            try
            {
                if (SettingsPage.MemoryNavigationSupportedPages.Contains(e.SourcePageType))
                {
                    PrepareToConnectedBackwardAnimate(NavGrid);
                }
            }
            catch { }
        }

        private ObservableCollection<BreadcrumbBarModel> BreadcrumbBarModels = [];
        public void CreateBreadcrumbBarNavigation()
        {
            BreadcrumbBarModels.Clear();
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("Settings"),
                Tag = typeof(SettingsPage)
            });
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("MemoryUsage"),
                Tag = typeof(MemoryViewPage)
            });
            BreadcrumbBarModels.Add(new()
            {
                DisplayName = localizer.GetLocalizedString("MemoryViewSettingsDetails"),
                Tag = this.GetType()
            });

        }

        private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var item = (BreadcrumbBarModel)args.Item;
            Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveFlyout.Hide();

            ErrorStackPanel.Visibility = Visibility.Collapsed;
            ResetButton.IsEnabled = false;
            try
            {
                Directory.Delete(Path.Combine(CDPIUI.Core.Data.Directories.DataDirectory, "Settings"), true);
                await PipeHelper.SendApplicationPacket(Shared.Pipe.Models.ApplicationMessageIds.HardRestart);
                MemoryTextBlock.Text = UnitsParser.FormatSize(0);
            }
            catch (Exception ex)
            {
                ErrorStackPanel.Visibility = Visibility.Visible;
                ErrorTextBlock.Text = string.Format(localizer.GetLocalizedString("ErrorHappensWhileCleanup"), ErrorsHelper.Convertor.GetPrettyErrorCode("DIR_CLEANUP", ex));
                ResetButton.IsEnabled = true;
            }
            await Task.CompletedTask;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveFlyout.Hide();
        }

        private async void CalcSize()
        {
            MemoryTextBlock.Text = UnitsParser.FormatSize(
                await FileSystemService.GetDirectorySize(CDPIUI.Core.Data.Directories.SettingsDirectory,
                Logger.Instance));
        }
    }
}
