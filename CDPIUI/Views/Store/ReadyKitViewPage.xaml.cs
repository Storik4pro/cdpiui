using CDPIUI.Commands;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.Store;
using CDPIUI.Core;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Helper;
using CDPIUI.Helper.LScript;
using CDPIUI.Helper.Parsers;
using CDPIUI.Shared.PrettyErrorConvertionService;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using WinUI3Localizer;

namespace CDPIUI.Views.Store
{
    public sealed class ReadyKitContentItemViewModel : INotifyPropertyChanged
    {
        private string _status = string.Empty;

        public string StoreId { get; set; }
        public string Name { get; set; }
        public string Developer { get; set; }

        public string Category { get; set; }
        public string Description { get; set; }

        public ImageSource ImageSource { get; set; }
        public SolidColorBrush BackgroundBrush { get; set; }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value)
                    return;

                _status = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed partial class ReadyKitViewPage : TemplatePage
    {
        private readonly ILocalizer _localizer = Localizer.Get();
        private readonly Dictionary<string, double> _itemProgress = new(StringComparer.OrdinalIgnoreCase);

        private ReadyKitModel _kit;
        private List<RepoItemModel> _items = [];
        private bool _canInstall;
        private bool _hasInstallationError;
        private bool _handlersConnected;
        private bool _isPageActive;
        private bool _loaded;
        private string _activeItemId = string.Empty;
        private string _loadErrorTitle = string.Empty;
        private string _loadErrorMessage = string.Empty;
        private string _loadErrorCode = string.Empty;

        private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
        private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
        private Action<Tuple<string, double>> _itemDownloadSpeedChangedHandler;
        private Action<Tuple<string, ErrorModel>> _itemInstallingErrorHappensHandler;
        private Action<string> _itemActionsStoppedHandler;

        public MarkdownConfig MarkdownConfig { get; } = new();
        public ObservableCollection<ReadyKitContentItemViewModel> ContentItems { get; } = [];

        public ReadyKitViewPage()
        {
            InitializeComponent();

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = KitImage;

            Loaded += ReadyKitViewPage_Loaded;
        }

        private void ReadyKitViewPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_loaded)
                ShowLoadErrorDialog();

            Loaded -= ReadyKitViewPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _isPageActive = true;

            string kitId = Parameter?.Get("kitId");
            _kit = StoreHelper.Instance.GetReadyKitFromStoreId(kitId);
            if (_kit == null)
            {
                SetLoadError(
                    _localizer.GetLocalizedString("ReadyKitInvalidTitle"),
                    _localizer.GetLocalizedString("ReadyKitInvalidMessage"),
                    "ERR_STORE_READY_KIT_NOT_FOUND");
                return;
            }

            if (!LoadKit())
                return;

            _loaded = true;
            ConnectQueueHandlers();
            RefreshActionState();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _isPageActive = false;
            DisconnectQueueHandlers();

            foreach (ReadyKitContentItemViewModel item in ContentItems)
                item.ImageSource = null;

            KitImage.Source = null;
        }

        private bool LoadKit()
        {
            _items = (_kit.items ?? [])
                .Select(StoreHelper.Instance.GetItemInfoFromStoreId)
                .OfType<RepoItemModel>()
                .ToList();

            string locale = StoreLocalizationHelper.GetStoreLikeLocale();
            KitNameTextBlock.Text = _kit.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(
                _kit.name,
                locale);
            KitSmallDescriptionTextBlock.Text = LScriptLangHelper.ExecuteScript(_kit.small_description, locale);
            KitDescriptionTextBlock.Text = LScriptLangHelper.ExecuteScript(_kit.description, locale);
            KitImage.Source = new BitmapImage(new Uri(LScriptLangHelper.ExecuteScript(_kit.icon)));
            KitIconBackground.Background = ReadyKitBrushFactory.Create(
                _items.Select(item => item.background),
                _kit.background);
            ContentsTitleTextBlock.Text = _localizer.GetLocalizedString("ReadyKitContents");
            ReadyKitsCategoryTextBlock.Text = _localizer.GetLocalizedString("ReadyKitsPageTitle");
            KitItemsCountTextBlock.Text = _items.Count.ToString();

            ContentItems.Clear();
            foreach (RepoItemModel item in _items)
            {
                ContentItems.Add(new ReadyKitContentItemViewModel
                {
                    StoreId = item.store_id,
                    Name = GetItemDisplayName(item),
                    Developer = item.developer,
                    Category = StoreHelper.Instance.GetLocalizedStoreItemName(
                               StoreHelper.Instance.GetCategoryFromStoreId(item.category_id)?.name ?? string.Empty,
                               StoreLocalizationHelper.GetStoreLikeLocale()),
                    Description = LScriptLangHelper.ExecuteScript(item.small_description, StoreLocalizationHelper.GetStoreLikeLocale()),
                    ImageSource = new BitmapImage(new Uri(LScriptLangHelper.ExecuteScript(item.icon))),
                    BackgroundBrush = UIHelper.HexToSolidColorBrushConverter(item.background)
                });
            }

            int missingItemCount = (_kit.items ?? [])
                .Count(itemId => StoreHelper.Instance.GetItemInfoFromStoreId(itemId) == null);
            if (_items.Count == 0)
            {
                SetLoadError(
                    _localizer.GetLocalizedString("ReadyKitInvalidTitle"),
                    _localizer.GetLocalizedString("ReadyKitInvalidMessage"),
                    "ERR_STORE_READY_KIT_EMPTY");
                return false;
            }

            if (missingItemCount > 0)
            {
                SetLoadError(
                    _localizer.GetLocalizedString("ReadyKitInvalidTitle"),
                    string.Format(
                        _localizer.GetLocalizedString("ReadyKitMissingItemsMessage"),
                        missingItemCount),
                    "ERR_STORE_READY_KIT_ITEM_NOT_FOUND");
                return false;
            }

            if (_items.Any(item => !IsItemSupported(item)))
            {
                DisableInstallation(_localizer.GetLocalizedString("ReadyKitUnsupportedMessage"));
                return true;
            }

            _canInstall = true;
            return true;
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_canInstall)
                return;

            List<RepoItemModel> itemsToInstall = _items
                .Where(item => !DatabaseHelper.Instance.IsItemInstalled(item.store_id))
                .ToList();
            if (itemsToInstall.Count == 0)
                return;

            List<ItemLicenseModel> licenses = itemsToInstall
                .SelectMany(item => item.license ?? [])
                .GroupBy(license => $"{license.name}\n{license.url}")
                .Select(group => group.First())
                .ToList();

            if (licenses.Count > 0)
            {
                AcceptLicenseContentDialog dialog = new()
                {
                    Licenses = licenses,
                    XamlRoot = XamlRoot
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    return;
            }

            _hasInstallationError = false;
            ErrorStatusGrid.Visibility = Visibility.Collapsed;
            foreach (RepoItemModel item in itemsToInstall)
                _itemProgress[item.store_id] = 0;

            ShowDownloadState();
            CurrentStatusTextBlock.Text = _localizer.GetLocalizedString("QueueWaiting");
            CurrentStatusSpeedTextBlock.Visibility = Visibility.Collapsed;

            HashSet<string> pendingIds = itemsToInstall
                .Select(item => item.store_id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string itemId in _kit.items ?? [])
            {
                if (pendingIds.Contains(itemId))
                    StoreHelper.Instance.AddItemToQueue(itemId, string.Empty);
            }

            RefreshActionState();
        }

        private void RefreshActionState()
        {
            if (!_isPageActive || _kit == null)
                return;

            UpdateContentItemStatuses();

            if (_hasInstallationError)
            {
                ErrorStatusGrid.Visibility = Visibility.Visible;
                InstallButton.Visibility = Visibility.Collapsed;
                DownloadStatusGrid.Visibility = Visibility.Collapsed;
                return;
            }

            bool isFullyInstalled = _items.Count > 0 &&
                _items.All(item => DatabaseHelper.Instance.IsItemInstalled(item.store_id));
            if (isFullyInstalled)
            {
                _activeItemId = string.Empty;
                InstallButtonTextBlock.Text = _localizer.GetLocalizedString("Installed");
                InstallButton.IsEnabled = false;
                InstallButton.Visibility = Visibility.Visible;
                DownloadStatusGrid.Visibility = Visibility.Collapsed;
                ErrorStatusGrid.Visibility = Visibility.Collapsed;
                StatusProgressBar.Value = 100;
                return;
            }

            RepoItemModel activeItem = null;
            QueueItemModel activeQueueItem = null;
            string currentOperationId = StoreHelper.Instance.GetCurrentQueueOperationId();
            bool hasPendingItems = false;

            foreach (RepoItemModel item in _items)
            {
                string operationId = StoreHelper.Instance.GetOperationIdFromItemId(item.store_id);
                if (string.IsNullOrEmpty(operationId))
                    continue;

                hasPendingItems = true;
                QueueItemModel queueItem = StoreHelper.Instance.GetQueueItemFromOperationId(operationId);
                if (activeItem == null || string.Equals(operationId, currentOperationId, StringComparison.OrdinalIgnoreCase))
                {
                    activeItem = item;
                    activeQueueItem = queueItem;
                }

                if (string.Equals(operationId, currentOperationId, StringComparison.OrdinalIgnoreCase))
                    break;
            }

            if (hasPendingItems)
            {
                _activeItemId = activeItem?.store_id ?? string.Empty;
                ShowDownloadState();
                ApplyDownloadStage(activeQueueItem?.DownloadStage, activeItem);
                UpdateAggregateProgress();
                return;
            }

            _activeItemId = string.Empty;
            DownloadStatusGrid.Visibility = Visibility.Collapsed;
            ErrorStatusGrid.Visibility = Visibility.Collapsed;
            InstallButton.Visibility = Visibility.Visible;
            InstallButtonTextBlock.Text = _localizer.GetLocalizedString("Get");
            InstallButton.IsEnabled = _canInstall;

            if (!_hasInstallationError)
                StatusProgressBar.Value = CalculateAggregateProgress();
        }

        private void ApplyDownloadStage(string stage, RepoItemModel activeItem)
        {
            CurrentStatusTextBlock.Text = GetStageText(stage);
            CurrentStatusSpeedTextBlock.Visibility = string.Equals(stage, "Downloading", StringComparison.Ordinal)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (activeItem == null)
            {
                CurrentStatusTipTextBlock.Text = string.Empty;
                return;
            }

            int itemNumber = _items.FindIndex(item =>
                string.Equals(item.store_id, activeItem.store_id, StringComparison.OrdinalIgnoreCase)) + 1;
            CurrentStatusTipTextBlock.Text = string.Format(
                _localizer.GetLocalizedString("ReadyKitItemProgress"),
                itemNumber,
                _items.Count,
                GetItemDisplayName(activeItem));
        }

        private string GetStageText(string stage) => stage switch
        {
            "GETR" => _localizer.GetLocalizedString("GettingReady"),
            "END" => _localizer.GetLocalizedString("Finishing"),
            "Downloading" => _localizer.GetLocalizedString("Downloading"),
            "Extracting" => _localizer.GetLocalizedString("Installing"),
            "Completed" => _localizer.GetLocalizedString("Finishing"),
            "CANC" => _localizer.GetLocalizedString("Cancel"),
            "ConnectingToService" => _localizer.GetLocalizedString("ConnectingToService"),
            "ErrorHappens" => _localizer.GetLocalizedString("ErrorHappens"),
            _ => _localizer.GetLocalizedString("QueueWaiting")
        };

        private void UpdateAggregateProgress() =>
            StatusProgressBar.Value = CalculateAggregateProgress();

        private double CalculateAggregateProgress()
        {
            if (_items.Count == 0)
                return 0;

            double totalProgress = 0;
            foreach (RepoItemModel item in _items)
            {
                if (DatabaseHelper.Instance.IsItemInstalled(item.store_id))
                {
                    totalProgress += 100;
                    continue;
                }

                double progress = 0;
                string operationId = StoreHelper.Instance.GetOperationIdFromItemId(item.store_id);
                if (!string.IsNullOrEmpty(operationId))
                {
                    QueueItemModel queueItem = StoreHelper.Instance.GetQueueItemFromOperationId(operationId);
                    progress = queueItem?.DownloadProgress ?? 0;
                }

                if (_itemProgress.TryGetValue(item.store_id, out double observedProgress))
                    progress = Math.Max(progress, observedProgress);

                totalProgress += Math.Clamp(progress, 0, 100);
            }

            return totalProgress / _items.Count;
        }

        private void UpdateContentItemStatuses()
        {
            foreach (ReadyKitContentItemViewModel contentItem in ContentItems)
            {
                if (DatabaseHelper.Instance.IsItemInstalled(contentItem.StoreId))
                {
                    contentItem.Status = _localizer.GetLocalizedString("Installed");
                    continue;
                }

                string operationId = StoreHelper.Instance.GetOperationIdFromItemId(contentItem.StoreId);
                if (string.IsNullOrEmpty(operationId))
                {
                    contentItem.Status = _localizer.GetLocalizedString("Get");
                    continue;
                }

                QueueItemModel queueItem = StoreHelper.Instance.GetQueueItemFromOperationId(operationId);
                contentItem.Status = GetStageText(queueItem?.DownloadStage);
            }
        }

        private void ConnectQueueHandlers()
        {
            if (_handlersConnected)
                return;

            _itemDownloadStageChangedHandler = data =>
            {
                string itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
                if (!ContainsItem(itemId))
                    return;

                RunOnUIThread(() =>
                {
                    _activeItemId = itemId;
                    CurrentStatusSpeedTextBlock.Text = string.Empty;
                    RefreshActionState();
                });
            };
            StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

            _itemDownloadProgressChangedHandler = data =>
            {
                string itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
                if (!ContainsItem(itemId))
                    return;

                RunOnUIThread(() =>
                {
                    _activeItemId = itemId;
                    _itemProgress[itemId] = data.Item2;
                    if (DownloadStatusGrid.Visibility != Visibility.Visible)
                        RefreshActionState();
                    else
                        UpdateAggregateProgress();
                });
            };
            StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

            _itemDownloadSpeedChangedHandler = data =>
            {
                string itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
                if (!ContainsItem(itemId))
                    return;

                RunOnUIThread(() =>
                {
                    if (!string.Equals(_activeItemId, itemId, StringComparison.OrdinalIgnoreCase))
                        return;

                    CurrentStatusSpeedTextBlock.Text = $"{UnitsParser.FormatSpeed(data.Item2)},";
                    CurrentStatusSpeedTextBlock.Visibility = Visibility.Visible;
                });
            };
            StoreHelper.Instance.ItemDownloadSpeedChanged += _itemDownloadSpeedChangedHandler;

            _itemInstallingErrorHappensHandler = data =>
            {
                string itemId = StoreHelper.Instance.GetItemIdFromOperationId(data.Item1);
                if (!ContainsItem(itemId))
                    return;

                RunOnUIThread(() =>
                {
                    _hasInstallationError = true;
                    ErrorNameTextBlock.Text = data.Item2.ErrorCode;
                    ErrorStatusGrid.Visibility = Visibility.Visible;
                    InstallButton.Visibility = Visibility.Collapsed;
                    DownloadStatusGrid.Visibility = Visibility.Collapsed;
                });
            };
            StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;

            _itemActionsStoppedHandler = itemId =>
            {
                if (!ContainsItem(itemId))
                    return;

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!_isPageActive)
                        return;

                    _itemProgress[itemId] = DatabaseHelper.Instance.IsItemInstalled(itemId) ? 100 : 0;
                    RefreshActionState();
                });
            };
            StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;

            _handlersConnected = true;
        }

        private void DisconnectQueueHandlers()
        {
            if (!_handlersConnected)
                return;

            StoreHelper.Instance.ItemDownloadStageChanged -= _itemDownloadStageChangedHandler;
            StoreHelper.Instance.ItemDownloadProgressChanged -= _itemDownloadProgressChangedHandler;
            StoreHelper.Instance.ItemDownloadSpeedChanged -= _itemDownloadSpeedChangedHandler;
            StoreHelper.Instance.ItemInstallingErrorHappens -= _itemInstallingErrorHappensHandler;
            StoreHelper.Instance.ItemActionsStopped -= _itemActionsStoppedHandler;
            _handlersConnected = false;
        }

        private void StopActionButton_Click(object sender, RoutedEventArgs e)
        {
            StopActionButton.IsEnabled = false;
            CurrentStatusTextBlock.Text = _localizer.GetLocalizedString("Cancel");
            _hasInstallationError = false;

            foreach (RepoItemModel item in _items)
                StoreHelper.Instance.RemoveItemFromQueue(item.store_id);
        }

        private void ShowDownloadState()
        {
            StopActionButton.IsEnabled = true;
            ErrorStatusGrid.Visibility = Visibility.Collapsed;
            InstallButton.Visibility = Visibility.Collapsed;
            DownloadStatusGrid.Visibility = Visibility.Visible;
        }

        private async void ErrorHelpButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new()
            {
                Title = _localizer.GetLocalizedString("AvailableActions"),
                Content = _localizer.GetLocalizedString("AvailableActionsTip"),
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void LaunchKitUnsupportedHelp_Click(object sender, RoutedEventArgs e)
        {
            CommandsHandler.HandleCommand("cdpiui://Help/Store/ItemUnsupportedWarning/");
        }

        private void ContentItemButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not StoreItemSmallButton button)
                return;

            button.Click -= ContentItemButton_Click;
            button.Click += ContentItemButton_Click;
        }

        private void ContentItemButton_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is StoreItemSmallButton button)
                button.Click -= ContentItemButton_Click;
        }

        private void ContentItemButton_Click(StoreItemSmallButton button)
        {
            PrepareToConnectedForwardAnimate(button.imageElement);
            StoreWindow.Instance.NavigateSubPage(
                typeof(ItemViewPage),
                new NameValueCollection { { "itemId", button.StoreId } },
                new SuppressNavigationTransitionInfo());
        }

        private void ReadyKitsCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow.Instance.NavigateSubPage(
                typeof(ReadyKitsViewPage),
                null,
                new SuppressNavigationTransitionInfo());
        }

        private void DisableInstallation(string message)
        {
            _canInstall = false;
            InstallButtonTextBlock.Text = _localizer.GetLocalizedString("Get");
            InstallButton.IsEnabled = false;
            KitUnsupportedWarningTextBlock.Text = message;
            KitUnsupportedWarningGrid.Visibility = Visibility.Visible;
        }

        private void SetLoadError(string title, string message, string errorCode)
        {
            _canInstall = false;
            _loadErrorTitle = title;
            _loadErrorMessage = message;
            _loadErrorCode = errorCode;
        }

        private void ShowLoadErrorDialog()
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                ContentDialog dialog = new()
                {
                    XamlRoot = XamlRoot,
                    Title = _loadErrorTitle,
                    Content = $"{_loadErrorMessage} {_loadErrorCode}",
                    PrimaryButtonText = "OK"
                };
                await dialog.ShowAsync();

                if (Frame.CanGoBack)
                    Frame.GoBack();
                else
                    Frame.Navigate(typeof(HomePage));
            });
        }

        private bool ContainsItem(string itemId) =>
            !string.IsNullOrEmpty(itemId) && _items.Any(item =>
                string.Equals(item.store_id, itemId, StringComparison.OrdinalIgnoreCase));

        private string GetItemDisplayName(RepoItemModel item) =>
            item.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(
                item.name,
                StoreLocalizationHelper.GetStoreLikeLocale());

        private void RunOnUIThread(Action action)
        {
            if (!_isPageActive)
                return;

            if (DispatcherQueue.HasThreadAccess)
                action();
            else
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_isPageActive)
                        action();
                });
        }

        private static bool IsItemSupported(RepoItemModel item)
        {
            if (!Version.TryParse(ApplicationInfo.Version, out Version currentVersion) ||
                !Version.TryParse(item.target_minversion, out Version minVersion))
            {
                return false;
            }

            if (currentVersion < minVersion)
                return false;

            return item.target_maxversion == "NaN" ||
                (Version.TryParse(item.target_maxversion, out Version maxVersion) && currentVersion <= maxVersion);
        }
    }
}
