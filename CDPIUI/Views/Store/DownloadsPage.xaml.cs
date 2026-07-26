using CDPIUI.Core.Static;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Helper.LScript;
using CDPIUI.Helper.Static;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Specialized;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using CDPIUI.Controls.Default;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Store
{
    public class DownloadItemModel
    {
        public string StoreId { get; set; }
        public string OperationId { get; set; }
        public string Title { get; set; }
        public string Developer { get; set; }
        public string Category { get; set; }
        public string Descripition { get; set; }
        public string CurrentVersion { get; set; }
        public string ServerVersion { get; set; }
        public ImageSource ImageSource { get; set; }
        public Brush CardBackgroundBrush { get; set; }
        public bool IsErrorHappens { get; set; } = false;
    }
    public sealed partial class DownloadsPage : TemplatePage
    {
        private ObservableCollection<DownloadItemModel> downloads = [];
        private ObservableCollection<DownloadItemModel> updates = [];

        public ICommand ActionCommand { get; }
        public ICommand UpdateCommand { get; }

        public DownloadsPage()
        {
            InitializeComponent();
            this.DataContext = this;

            DownloadsListView.ItemsSource = downloads;
            UpdatesListView.ItemsSource = updates;

            UpdateCurrentDownloadsList();
            UpdateCurrentDownloadItem();
            StoreHelper.Instance.QueueUpdated += UpdateCurrentDownloadsList;
            StoreHelper.Instance.ErrorListUpdated += UpdateCurrentDownloadsList;
            StoreHelper.Instance.NowProcessItemActions += StoreHelper_NowProcessItemActions;
            StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;

            StoreHelper.Instance.UpdateCheckStarted += StoreHelper_UpdateCheckStarted;
            StoreHelper.Instance.UpdateCheckStopped += StoreHelper_UpdateCheckStopped;

            ActionCommand = new RelayCommand(p => RemoveItem((DownloadItemModel)p));
            UpdateCommand = new RelayCommand(p => UpdateElement((DownloadItemModel)p));

            LoadUpdatesList();
            ToggleUpdateMode(StoreHelper.Instance.IsNowUpdatesChecked);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // support NameValueCollection or legacy string parameter
            if (Parameter != null && bool.TryParse(Parameter.Get("isUpdateRequested"), out var isUpdateRequested))
            {
                if (isUpdateRequested)
                {
                    if (!StoreHelper.Instance.IsNowUpdatesChecked)
                        StoreHelper.Instance.CheckUpdates();
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            StoreHelper.Instance.QueueUpdated -= UpdateCurrentDownloadsList;
            StoreHelper.Instance.ErrorListUpdated -= UpdateCurrentDownloadsList;
        }

        private void AuditSubHeaderVisible()
        {
            if (downloads.Count == 0) NowDownloadingTextBlock.Visibility = Visibility.Collapsed;
            else NowDownloadingTextBlock.Visibility = Visibility.Visible;
        }

        private void RemoveItem(DownloadItemModel item)
        {
            downloads.Remove(item);
            AuditSubHeaderVisible();
        }

        private void StoreHelper_ItemActionsStopped(string obj)
        {
            var item = downloads.FirstOrDefault(x => x.StoreId == obj);
            if (item != null)
            {
                downloads.Remove(item);
            }
            AuditSubHeaderVisible();
        }

        private void StoreHelper_NowProcessItemActions(string obj)
        {
            UpdateCurrentDownloadItem();
        }

        private void UpdateCurrentDownloadItem()
        {
            string opId = StoreHelper.Instance.GetCurrentQueueOperationId();
            Debug.WriteLine("WTF1");
            var curItem = downloads.FirstOrDefault(x => x.StoreId == StoreHelper.Instance.GetItemIdFromOperationId(opId));
            if (curItem != null) downloads.Remove(curItem);

            Debug.WriteLine("WTF2");

            var item = StoreHelper.Instance.GetItemInfoFromStoreId(StoreHelper.Instance.GetItemIdFromOperationId(opId));

            if (item == null) return;

            BitmapImage image = new(UIHelper.GetUriFromString(LScriptLangHelper.ExecuteScript(item.icon)));

            SolidColorBrush solidColorBrush = UIHelper.HexToSolidColorBrushConverter(item.background);

            DownloadItemModel downloadItemModel = new();

            downloadItemModel.StoreId = item.store_id;
            downloadItemModel.OperationId = StoreHelper.Instance.GetCurrentQueueOperationId();
            downloadItemModel.Title = StoreHelper.Instance.GetLocalizedStoreItemName(item.name, StoreLocalizationHelper.GetStoreLikeLocale());
            downloadItemModel.Developer = item.developer;

            if (item.category_id != null) 
                downloadItemModel.Category = StoreHelper.Instance.GetLocalizedStoreItemName(StoreHelper.Instance.GetCategoryFromStoreId(item.category_id).name, StoreLocalizationHelper.GetStoreLikeLocale());

            downloadItemModel.ImageSource = image;
            downloadItemModel.CardBackgroundBrush = solidColorBrush;
            downloadItemModel.IsErrorHappens = false;

            downloads.Add(downloadItemModel);
            AuditSubHeaderVisible();
        }

        private void UpdateCurrentDownloadsList()
        {
            downloads.Clear();
            UpdateCurrentDownloadItem();

            Queue<QueueItemModel> queueItems = StoreHelper.Instance.GetQueue();

            List<string> opIds = [];

            Debug.WriteLine($"QUEUE UPDATE {queueItems.Count}");

            foreach (QueueItemModel item in queueItems)
            {
                opIds.Add(item.OperationId); 
                Debug.WriteLine($"{item.ItemId}");
                var curItem = downloads.FirstOrDefault(x => x.StoreId == item.ItemId);
                if (curItem != null && !curItem.IsErrorHappens) continue;
                else downloads.Remove(curItem);

                var storeItem = StoreHelper.Instance.GetItemInfoFromStoreId(item.ItemId);

                string eImageSource = LScriptLangHelper.ExecuteScript(storeItem.icon);
                BitmapImage image = new BitmapImage(new Uri(eImageSource));

                SolidColorBrush solidColorBrush = UIHelper.HexToSolidColorBrushConverter(storeItem.background);

                DownloadItemModel downloadItem = new()
                {
                    StoreId = storeItem.store_id,
                    OperationId = item.OperationId,
                    Title = StoreHelper.Instance.GetLocalizedStoreItemName(storeItem.name, StoreLocalizationHelper.GetStoreLikeLocale()),
                    Developer = storeItem.developer,
                    Category = StoreHelper.Instance.GetLocalizedStoreItemName(StoreHelper.Instance.GetCategoryFromStoreId(storeItem.category_id).name, StoreLocalizationHelper.GetStoreLikeLocale()),
                    ImageSource = image,
                    CardBackgroundBrush = solidColorBrush,
                };
                downloads.Add(downloadItem);
            }

            List<QueueItemModel> failureItems = StoreHelper.Instance.GetFailedToInstallItems();

            foreach (QueueItemModel item in failureItems)
            {
                opIds.Add(item.OperationId);
                Debug.WriteLine($">>> {item.ItemId}");
                var curItem = downloads.FirstOrDefault(x => x.StoreId == item.ItemId);
                if (curItem != null) continue;

                var storeItem = StoreHelper.Instance.GetItemInfoFromStoreId(item.ItemId);

                string eImageSource = LScriptLangHelper.ExecuteScript(storeItem.icon);
                BitmapImage image = new BitmapImage(new Uri(eImageSource));

                SolidColorBrush solidColorBrush = UIHelper.HexToSolidColorBrushConverter(storeItem.background);

                DownloadItemModel downloadItem = new()
                {
                    StoreId = storeItem.store_id,
                    OperationId = item.OperationId,
                    Title = StoreHelper.Instance.GetLocalizedStoreItemName(storeItem.name, StoreLocalizationHelper.GetStoreLikeLocale()),
                    Developer = storeItem.developer,
                    Category = StoreHelper.Instance.GetLocalizedStoreItemName(StoreHelper.Instance.GetCategoryFromStoreId(storeItem.category_id).name, StoreLocalizationHelper.GetStoreLikeLocale()),
                    ImageSource = image,
                    CardBackgroundBrush = solidColorBrush,
                    IsErrorHappens = true
                };
                downloads.Add(downloadItem);
            }

            AuditSubHeaderVisible();
        }

        private void LoadUpdatesList()
        {
            updates.Clear();
            foreach (var item in StoreHelper.Instance.UpdatesAvailableList)
            {
                if (!string.IsNullOrEmpty(StoreHelper.Instance.GetOperationIdFromItemId(item.StoreId))) continue;
                var storeItem = StoreHelper.Instance.GetItemInfoFromStoreId(item.StoreId);

                string eImageSource = LScriptLangHelper.ExecuteScript(storeItem.icon);
                BitmapImage image = new BitmapImage(new Uri(eImageSource));

                SolidColorBrush solidColorBrush = UIHelper.HexToSolidColorBrushConverter(storeItem.background);

                DownloadItemModel downloadItem = new()
                {
                    StoreId = item.StoreId,
                    Title = StoreHelper.Instance.GetLocalizedStoreItemName(storeItem.name, StoreLocalizationHelper.GetStoreLikeLocale()),
                    Developer = storeItem.developer,
                    Category = StoreHelper.Instance.GetLocalizedStoreItemName(StoreHelper.Instance.GetCategoryFromStoreId(storeItem.category_id).name, StoreLocalizationHelper.GetStoreLikeLocale()),
                    ImageSource = image,
                    CardBackgroundBrush = solidColorBrush,
                    CurrentVersion = item.CurrentVersion,
                    ServerVersion = item.ServerVersion,
                    Descripition = item.VersionInfo
                };

                updates.Add(downloadItem);
            }

            if (updates.Count > 0)
                EmptyGrid.Visibility = Visibility.Collapsed;
            else 
                EmptyGrid.Visibility = Visibility.Visible;
        }

        private void StoreHelper_UpdateCheckStarted()
        {
            updates.Clear();
            ToggleUpdateMode(true);
        }

        private void StoreHelper_UpdateCheckStopped()
        {
            LoadUpdatesList();
            ToggleUpdateMode(false);
        }

        private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!StoreHelper.Instance.IsNowUpdatesChecked)
                StoreHelper.Instance.CheckUpdates();
        }

        private void UpdateElement(DownloadItemModel downloadItemModel)
        {
            updates.Remove(downloadItemModel);

            StoreHelper.Instance.AddItemToQueue(downloadItemModel.StoreId, downloadItemModel.ServerVersion);

            if (updates.Count <= 0)
            {
                EmptyGrid.Visibility = Visibility.Visible;
            }
        }

        public void ToggleUpdateMode(bool mode)
        {
            if (mode)
            {
                UpdateCheckProgressRing.Visibility = Visibility.Visible;
                CheckText.Opacity = 0;
            }
            else
            {
                UpdateCheckProgressRing.Visibility = Visibility.Collapsed;
                CheckText.Opacity = 100;
            }
        }

    }
}
