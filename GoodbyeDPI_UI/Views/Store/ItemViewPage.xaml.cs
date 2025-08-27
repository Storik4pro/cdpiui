using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.Static;
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
using System.ComponentModel;
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

        private bool errorHappens = false;

        private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
        private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
        private Action<Tuple<string, TimeSpan>> _itemTimeRemainingChangedHandler;
        private Action<Tuple<string, string>> _itemInstallingErrorHappensHandler;
        private Action<string> _itemActionsStoppedHandler;

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

                ItemName.Text = item.short_name?? StoreHelper.Instance.GetLocalizedStoreItemName(item.name, "RU");
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

                if (DatabaseHelper.Instance.IsItemInstalled(storeId))
                {
                    ItemActionButtonText.Text = "Настроить";
                    ItemMoreButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ItemActionButtonText.Text = "Установить";
                    ItemMoreButton.Visibility = Visibility.Collapsed;
                }

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

        private void ConnectHandlers()
        {
            _itemDownloadStageChangedHandler  = (data) =>
            {
                string operationId = data.Item1;
                string stage = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                    return;

                string stageHeaderText;

                CurrentStatusSpeedTextBlock.Visibility = Visibility.Collapsed;

                switch (stage)
                {
                    case "GETR":
                        stageHeaderText = "Подготовка";
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    case "END":
                        stageHeaderText = "Завершение";
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    case "Downloading":
                        stageHeaderText = "Скачивание";
                        StatusProgressbar.IsIndeterminate = false;
                        CurrentStatusSpeedTextBlock.Visibility = Visibility.Visible;
                        break;
                    case "Extracting":
                        stageHeaderText = "Установка";
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    case "ErrorHappens":
                        stageHeaderText = "Произошла ошибка";
                        break;
                    case "Completed":
                        stageHeaderText = "Завершение";
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    case "CANC":
                        stageHeaderText = "Отмена";
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    default:
                        stageHeaderText = "";
                        break;
                }

                CurrentStatusTextBlock.Text = stageHeaderText;
            };
            StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

            _itemDownloadProgressChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                double speed = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                    return;

                CurrentStatusSpeedTextBlock.Text = $"{Utils.FormatSpeed(speed)}, ";
            };
            StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

            _itemTimeRemainingChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                TimeSpan time = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                    return;

                if (time.Minutes > 0)
                    CurrentStatusTipTextBlock.Text = $"Осталось {time.Minutes} мин.";
                else
                    CurrentStatusTipTextBlock.Text = "Осталось менее минуты";
            };

            StoreHelper.Instance.ItemTimeRemainingChanged += _itemTimeRemainingChangedHandler;

            _itemDownloadProgressChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                double progress = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                    return;

                StatusProgressbar.IsIndeterminate = false;
                StatusProgressbar.Value = progress;
            };

            StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

            _itemInstallingErrorHappensHandler = (data) =>
            {
                string operationId = data.Item1;
                string errorCode = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                    return;

                ErrorStatusGrid.Visibility = Visibility.Visible;
                ItemActionButton.Visibility = Visibility.Collapsed;
                DownloadStatusGrid.Visibility = Visibility.Collapsed;

                ErrorNameTextBlock.Text = errorCode;
                errorHappens = true;
            };
            StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;

            _itemActionsStoppedHandler = (id) =>
            {
                ShowItemAfterInstallActions(id);
            };
            StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (_itemDownloadStageChangedHandler != null)
                StoreHelper.Instance.ItemDownloadStageChanged -= _itemDownloadStageChangedHandler;

            if (_itemDownloadProgressChangedHandler != null)
                StoreHelper.Instance.ItemDownloadProgressChanged -= _itemDownloadProgressChangedHandler;

            if (_itemTimeRemainingChangedHandler != null)
                StoreHelper.Instance.ItemTimeRemainingChanged -= _itemTimeRemainingChangedHandler;

            if (_itemInstallingErrorHappensHandler != null)
                StoreHelper.Instance.ItemInstallingErrorHappens -= _itemInstallingErrorHappensHandler;

            if (_itemActionsStoppedHandler != null)
                StoreHelper.Instance.ItemActionsStopped -= _itemActionsStoppedHandler;

            this.DataContext = null;
            MarkdownConfig = null;
        }

        private void ShowItemAfterInstallActions(string id)
        {
            StatusProgressbar.Value = 0;
            StatusProgressbar.IsIndeterminate = true;

            CurrentStatusSpeedTextBlock.Visibility = Visibility.Collapsed;
            CurrentStatusSpeedTextBlock.Text = "";
            CurrentStatusTipTextBlock.Text = "Идет работа над этим";

            if (id == _storeId && !errorHappens)
            {
                ItemActionButtonText.Text = "Установить";
                ItemMoreButton.Visibility = Visibility.Collapsed;
                ItemActionButton.Visibility = Visibility.Visible;
                DownloadStatusGrid.Visibility = Visibility.Collapsed;

                if (DatabaseHelper.Instance.IsItemInstalled(id))
                {
                    ItemActionButtonText.Text = "Настроить";
                    ItemMoreButton.Visibility = Visibility.Visible;
                }
            }
        }

        private void InstallingItemActions()
        {
            ConnectHandlers();
            errorHappens = false;

            StopActionButton.IsEnabled = true;
            StatusProgressbar.IsIndeterminate = true;
            ErrorStatusGrid.Visibility = Visibility.Collapsed;
            ItemActionButton.Visibility = Visibility.Collapsed;
            DownloadStatusGrid.Visibility = Visibility.Visible;

            CurrentStatusTextBlock.Text = "Ожидание";
            StoreHelper.Instance.AddItemToQueue(_storeId, string.Empty);
        }

        private void ItemCategoryButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ItemWarningButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ItemActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Helper.DatabaseHelper.Instance.IsItemInstalled(_storeId))
            {
                InstallingItemActions();
            }
            else
            {
                //TODO: open settings in main window
            }
        }

        private void StopActionButton_Click(object sender, RoutedEventArgs e)
        {
            StoreHelper.Instance.RemoveItemFromQueue(_storeId);
        }

        private void ErrorHelpButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            StoreHelper.Instance.ItemRemoved += (id) =>
            {
                if (id != _storeId)
                    return;
                ShowItemAfterInstallActions(id);
            };

            StopActionButton.IsEnabled = false;
            CurrentStatusTextBlock.Text = "Ожидание...";
            StatusProgressbar.IsIndeterminate = true;
            ErrorStatusGrid.Visibility = Visibility.Collapsed;
            ItemActionButton.Visibility = Visibility.Collapsed;
            DownloadStatusGrid.Visibility = Visibility.Visible;

            StoreHelper.Instance.RemoveItem(_storeId);
        }

        private void ReinstallButton_Click(object sender, RoutedEventArgs e)
        {
            InstallingItemActions();
        }
    }
}
