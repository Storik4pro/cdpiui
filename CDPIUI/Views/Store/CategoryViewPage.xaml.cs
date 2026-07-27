using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper.LScript;

using Markdig.Renderers.Normalize;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Specialized;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using CDPIUI.Controls.Default;
using CDPIUI.Helper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Store
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CategoryViewPage : TemplatePage
    {
        private string CategoryId = string.Empty;

        private ObservableCollection<UIElement> _tiles = new ObservableCollection<UIElement>();

        private ILocalizer localizer = Localizer.Get();
        public CategoryViewPage()
        {
            InitializeComponent();

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = CategoryTitleTextBlock;

            StaggeredRepeater.ItemsSource = _tiles;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (Parameter != null)
            {
                var categoryId = Parameter.Get("categoryId");
                if (!string.IsNullOrEmpty(categoryId))
                {
                    CategoryId = categoryId;
                    LoadCategory();
                }
            }
        }

        private async void LoadCategory()
        {
            bool result = await StoreHelper.Instance.LoadAllStoreDatabase(forseSync:false);
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

        private bool CreateCategoriesList(List<RepoCategoryModel> values)
        {
            var category = values.FirstOrDefault(c => c.store_id == CategoryId);

            if (category == null)
            {
                ErrorGrid.Visibility = Visibility.Visible;
                ErrorTextBlock.Text = $"Cannot GET category/{CategoryId}";
                return false;
            }

            CategoryTitleTextBlock.Text = StoreHelper.Instance.GetLocalizedStoreItemName(category.name, StoreLocalizationHelper.GetStoreLikeLocale());

            foreach (RepoItemModel repoCategoryItem in category.items)
            {
                _tiles.Add(
                    UIHelper.CreateLargeButton(
                        storeId: repoCategoryItem.store_id,
                        imageSource: LScriptLangHelper.ExecuteScript(repoCategoryItem.icon),
                        price: DatabaseHelper.Instance.IsItemInstalled(repoCategoryItem.store_id) ? localizer.GetLocalizedString("Installed") : localizer.GetLocalizedString("Get"),
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
                PrepareToConnectedForwardAnimate(button.imageElement);

                StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.ItemViewPage), new NameValueCollection()
                {
                    { "itemId", sid }
                }, 
                new SuppressNavigationTransitionInfo());
            }
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            
        }
    }
}
