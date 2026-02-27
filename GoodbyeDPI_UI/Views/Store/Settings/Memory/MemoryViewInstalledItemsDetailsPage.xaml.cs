using CDPI_UI.Controls.Store.Settings;
using CDPI_UI.Extensions;
using CDPI_UI.Helper;
using CDPI_UI.Helper.LScript;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
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
    public sealed partial class MemoryViewInstalledItemsDetailsPage : Page
    {
        private ObservableCollection<LibraryItemModel> LibraryItems = [];

        private ILocalizer localizer = Localizer.Get();
        public MemoryViewInstalledItemsDetailsPage()
        {
            InitializeComponent();
            BreadcrumbBar.ItemsSource = BreadcrumbBarModels;
            ItemsListView.ItemsSource = LibraryItems;

            CreateBreadcrumbBarNavigation();
            StoreHelper.Instance.ItemRemoved += StoreHelper_ItemRemoved;
            StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;
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

            LoadItems();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            try
            {
                if (SettingsPage.MemoryNavigationSupportedPages.Contains(e.SourcePageType))
                {
                    var animq = ConnectedAnimationService.GetForCurrentView()
                    .PrepareToAnimate("BackwardConnectedAnimation", NavGrid);

                    if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
                    {
                        animq.Configuration = new BasicConnectedAnimationConfiguration();
                    }
                }
            }
            catch { }

            StoreHelper.Instance.ItemRemoved -= StoreHelper_ItemRemoved;
            StoreHelper.Instance.ItemActionsStopped -= StoreHelper_ItemActionsStopped;


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
                DisplayName = localizer.GetLocalizedString("MemoryViewInstalledItemsDetails"),
                Tag = this.GetType()
            });

        }

        private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            var item = (BreadcrumbBarModel)args.Item;
            Frame.Navigate(item.Tag, null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
        }

        public async void LoadItems()
        {
            LibraryItems.Clear();
            List<DatabaseStoreItem> databaseStoreItems = DatabaseHelper.Instance.GetAllInstalledItems();

            foreach (DatabaseStoreItem item in databaseStoreItems)
            {
                if (item.Id == StateHelper.ApplicationStoreId) continue;

                string title = StoreHelper.Instance.GetLocalizedStoreItemName(item.Name, Utils.GetStoreLikeLocale());
                title = title.StartsWith("slocale:") ? item.ShortName : title;

                string category = localizer.GetLocalizedString(item.Type);
                category = string.IsNullOrEmpty(category) ? item.Type : category;

                string eImageSource = LScriptLangHelper.ExecuteScript(item.IconPath);
                BitmapImage image = new BitmapImage(new Uri(eImageSource));

                SolidColorBrush solidColorBrush = UIHelper.HexToSolidColorBrushConverter(item.BackgroudColor);

                LibraryItemModel libraryItemModel = new()
                {
                    StoreId = item.Id,
                    Title = title,
                    Developer = item.Developer,
                    Category = category,
                    ImageSource = image,
                    CardBackgroundBrush = solidColorBrush,
                    Size = await Utils.GetDirectorySize(item.Directory)
                };
                LibraryItems.Add(libraryItemModel);
            }
            LibraryItems.Sort();
            // _libraryItems.Reverse();
            await Task.CompletedTask;
        }



        private void StoreHelper_ItemActionsStopped(string obj)
        {
            LoadItems();
            CalcSize();
        }

        private void StoreHelper_ItemRemoved(string obj)
        {
            LoadItems();
            CalcSize();
        }

        private async void CalcSize()
        {
            MemoryTextBlock.Text = Utils.FormatSize(await Utils.GetDirectorySize(Path.Combine(StateHelper.GetDataDirectory(), StateHelper.StoreDirName, StateHelper.StoreItemsDirName)));
        }


    }
}
