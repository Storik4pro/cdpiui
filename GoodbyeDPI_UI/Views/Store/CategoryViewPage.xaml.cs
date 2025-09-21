using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using Markdig.Renderers.Normalize;
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
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Store
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CategoryViewPage : Page
    {
        private string CategoryId = string.Empty;

        private ObservableCollection<UIElement> _tiles = new ObservableCollection<UIElement>();

        private ILocalizer localizer = Localizer.Get();
        public CategoryViewPage()
        {
            InitializeComponent();
            StaggeredRepeater.ItemsSource = _tiles;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
            if (anim != null)
            {
                anim.TryStart(CategoryTitleTextBlock);
            }

            if (e.Parameter is string categoryId)
            {
                CategoryId = categoryId;
                LoadCategory();
            }
        }

        private async void LoadCategory()
        {
            bool result = await Helper.StoreHelper.Instance.LoadAllStoreDatabase();
            // bool result = true;
            if (result)
            {
                if (!CreateCategoriesList(StoreHelper.Instance.FormattedStoreDatabase)) return;
                LoadingProgressGrid.Visibility = Visibility.Collapsed;
                StoreScrollViewer.Visibility = Visibility.Visible;
            }
            else             
            {
                LoadingProgressGrid.Visibility = Visibility.Collapsed;
                StoreScrollViewer.Visibility = Visibility.Collapsed;
                ErrorGrid.Visibility = Visibility.Visible;
                ErrorTextBlock.Text = $"Cannot GET category/{CategoryId}";
            }
        }

        private bool CreateCategoriesList(List<Helper.StoreHelper.RepoCategory> values)
        {
            var category = values.FirstOrDefault(c => c.store_id == CategoryId);

            if (category == null)
            {
                ErrorGrid.Visibility = Visibility.Visible;
                ErrorTextBlock.Text = $"Cannot GET category/{CategoryId}";
                return false;
            }

            CategoryTitleTextBlock.Text = Helper.StoreHelper.Instance.GetLocalizedStoreItemName(category.name, Utils.GetStoreLikeLocale());

            foreach (Helper.StoreHelper.RepoCategoryItem repoCategoryItem in category.items)
            {
                _tiles.Add(
                    UIHelper.CreateLargeButton(
                        storeId: repoCategoryItem.store_id,
                        imageSource: Helper.StoreHelper.Instance.ExecuteScript(repoCategoryItem.icon),
                        price: Helper.DatabaseHelper.Instance.IsItemInstalled(repoCategoryItem.store_id) ? localizer.GetLocalizedString("Installed") : localizer.GetLocalizedString("Get"),
                        title: repoCategoryItem.short_name,
                        backgroundColor: repoCategoryItem.background,
                        action: StoreItemButton_Click,
                        developer: repoCategoryItem.developer
                    )
                );
            }
            return true;

        }

        private void StoreItemButton_Click(StoreItemLargeButton button)
        {
            if (button.StoreId is string sid)
            {
                ConnectedAnimationService.GetForCurrentView()
                    .PrepareToAnimate("ForwardConnectedAnimation", button.imageElement);

                StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.ItemViewPage), sid, new SuppressNavigationTransitionInfo());
            }
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            
        }
    }
}
