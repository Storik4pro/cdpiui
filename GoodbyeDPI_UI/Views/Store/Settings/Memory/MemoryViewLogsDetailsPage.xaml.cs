using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
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

namespace CDPI_UI.Views.Store.Settings.Memory
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MemoryViewLogsDetailsPage : Page
    {
        private ILocalizer localizer = Localizer.Get();
        public MemoryViewLogsDetailsPage()
        {
            InitializeComponent();
            BreadcrumbBar.ItemsSource = BreadcrumbBarModels;

            CreateBreadcrumbBarNavigation();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
            if (anim != null)
            {
                anim.TryStart(NavGrid);
            }

            if (e.Parameter is string param)
            {
                MemoryTextBlock.Text = param;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            try
            {

                var animq = ConnectedAnimationService.GetForCurrentView()
                    .PrepareToAnimate("BackwardConnectedAnimation", NavGrid);

                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
                {
                    animq.Configuration = new BasicConnectedAnimationConfiguration();
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
                DisplayName = localizer.GetLocalizedString("MemoryViewLogsDetails"),
                Tag = this.GetType()
            });

        }

        private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var item = (BreadcrumbBarModel)args.Item;
            Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        private void ViewAppDirButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Text = string.Format(localizer.GetLocalizedString("ErrorHappensWhileCleanup"), "DIR_CLEANUP_UNKNOWN");
            Utils.OpenFolderInExplorer(Path.Combine(StateHelper.GetDataDirectory(), "Logs"));
        }

        private async void CleanupDirButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorStackPanel.Visibility = Visibility.Collapsed;
            CleanupDirButton.IsEnabled = false;
            try
            {
                Directory.Delete(Path.Combine(StateHelper.GetDataDirectory(), "Logs"), true);
                MemoryTextBlock.Text = Utils.FormatSize(0);
            }
            catch (Exception ex) 
            {
                ErrorStackPanel.Visibility = Visibility.Visible;
                ErrorTextBlock.Text = string.Format(localizer.GetLocalizedString("ErrorHappensWhileCleanup"), ErrorsHelper.GetPrettyErrorCode("DIR_CLEANUP", ex));
                CleanupDirButton.IsEnabled = true;
            }
            await Task.CompletedTask;
        }
    }
}
