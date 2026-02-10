using CDPI_UI.Helper;
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
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Store.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    
    public class MemoryDiskViewModel
    {
        public string DiskLetter { get; set; }
    }
    public sealed partial class MemoryViewPage : Page
    {
        private ObservableCollection<MemoryDiskViewModel> MemoryDisks = [];

        private ILocalizer localizer = Localizer.Get();
        public MemoryViewPage()
        {
            InitializeComponent();

            BreadcrumbBar.ItemsSource = BreadcrumbBarModels;
            DiskInfoListView.ItemsSource = MemoryDisks;

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

            var backAnim = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackwardConnectedAnimation");
            if (backAnim != null)
            {
                backAnim.TryStart(NavGrid);
            }

            CreateDiskView();
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
                Tag = this.GetType()
            });
        }

        public void CreateDiskView()
        {
            MemoryDisks.Clear();

            string appDir = StateHelper.Instance.workDirectory;
            string dataDir = StateHelper.GetDataDirectory();

            DriveInfo appDirDriveInfo = new(appDir);
            DriveInfo dataDirDriveInfo = new(dataDir);

            if (appDirDriveInfo.VolumeLabel == dataDirDriveInfo.VolumeLabel) MemoryDisks.Add(new() { DiskLetter = appDirDriveInfo.VolumeLabel });
            else
            {
                MemoryDisks.Add(new() { DiskLetter = appDirDriveInfo.VolumeLabel });
                MemoryDisks.Add(new() { DiskLetter = dataDirDriveInfo.VolumeLabel });
            }
        }

        private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var item = (BreadcrumbBarModel)args.Item;
            Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
        }
    }
}
