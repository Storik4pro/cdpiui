using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using GoodbyeDPI_UI.Helper;
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
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Views.Store
{
    public sealed partial class ItemViewPage : Page
    {
        private string _storeId;
        private Helper.StoreHelper.RepoCategoryItem item;

        private MarkdownConfig _config;

        public MarkdownConfig MarkdownConfig
        {
            get => _config;
            set => _config = value;
        }

        public ItemViewPage()
        {
            InitializeComponent();
            _config = new MarkdownConfig();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
            if (anim != null)
            {
                anim.TryStart(ItemImage);
            }


            if (e.Parameter is string storeId)
            {
                _storeId = storeId;

                item = Helper.StoreHelper.Instance.GetItemInfoFromStoreId(storeId);

                ItemName.Text = StoreHelper.Instance.GetLocalizedStoreItemName(item.name, "RU");
                ItemImage.Source = new BitmapImage(new Uri(StoreHelper.Instance.ExecuteScript(item.icon)));
                ItemDeveloper.Text = item.developer;
                Logger.Instance.CreateDebugLog(nameof(ItemViewPage), item.category_id);
                StarCount.Text = item.stars ?? "NaN";
                ItemCategoryButton.Content = StoreHelper.Instance.GetLocalizedStoreItemName(
                    StoreHelper.Instance.GetCategoryFromStoreId(item.category_id).name,
                    "RU"
                );
                SmallDescriptionText.Text = StoreHelper.Instance.ExecuteScript(item.small_description, "RU");
                ItemFullDescriptionText.Text = StoreHelper.Instance.ExecuteScript(item.description, "RU");
                ItemWarningAera.Visibility = item.display_warning ? Visibility.Visible : Visibility.Collapsed;
                ItemWarningText.Text = StoreHelper.Instance.ExecuteScript(item.warning_text, "RU");

                ItemActionButtonText.Text = StoreHelper.Instance.IsItemInstalled(storeId) ? "Настроить" : "Установить";

                if (item.links.Count > 0)
                    CreateLinks(item.links);
                else 
                    LinksGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                ItemName.Text = "Ошибка: StoreId не получен";
            }
        }

        private void CreateLinks(List<StoreHelper.Link> links)
        {
            LinksStackPanel.Children.Clear();

            ObservableCollection<StoreItemLinkButton> linkButtons = new ObservableCollection<StoreItemLinkButton>();

            foreach (StoreHelper.Link link in links)
            {
                linkButtons.Add(new StoreItemLinkButton
                {
                    Text = StoreHelper.Instance.GetLocalizedStoreItemName(link.name, "RU"),
                    Url = link.url,
                });
            }

            var itemsControl = new ItemsControl
            {
                Margin = new Thickness(0, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = linkButtons,
                ItemsPanel = (ItemsPanelTemplate)this.Resources["DefaultItemsPanel"]
            };

            LinksStackPanel.Children.Add(itemsControl);

        }
        private void OnActionButtonClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Действие",
                Content = $"Будет выполнено действие по товару {_storeId}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot,
            };
            _ = dialog.ShowAsync();
        }

        private void ItemCategoryButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ItemWarningButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ItemActionButton_Click(object sender, RoutedEventArgs e)
        {
            await StoreHelper.Instance.InstallItem(_storeId);
        }

        private void StopActionButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
