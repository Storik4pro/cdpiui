using CDPI_UI.Controls.Dialogs.Store;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.Components;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
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
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Store
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
        private bool loaded = false;

        private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
        private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
        private Action<Tuple<string, double>> _itemDownloadSpeedChangedHandler;
        private Action<Tuple<string, TimeSpan>> _itemTimeRemainingChangedHandler;
        private Action<Tuple<string, string>> _itemInstallingErrorHappensHandler;
        private Action<string> _itemActionsStoppedHandler;

        private ILocalizer localizer = Localizer.Get();

        public ItemViewPage()
        {
            InitializeComponent();
            _config = new MarkdownConfig();

            this.Loaded += ItemViewPage_Loaded;
        }

        private void ItemViewPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!loaded) ShowErrorDialog();

            this.Loaded -= ItemViewPage_Loaded;
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

                ConnectHandlers();

                item = Helper.StoreHelper.Instance.GetItemInfoFromStoreId(storeId);

                if (item == null)
                {
                    return;
                }

                ItemName.Text = item.short_name?? StoreHelper.Instance.GetLocalizedStoreItemName(item.name, Utils.GetStoreLikeLocale());
                ItemImage.Source = new BitmapImage(new Uri(StoreHelper.Instance.ExecuteScript(item.icon)));
                ItemDeveloper.Text = item.developer;
                Logger.Instance.CreateDebugLog(nameof(ItemViewPage), item.category_id);
                StarCount.Text = item.stars ?? "NaN";
                ItemCategoryButton.Content = StoreHelper.Instance.GetLocalizedStoreItemName(
                    StoreHelper.Instance.GetCategoryFromStoreId(item.category_id).name,
                    Utils.GetStoreLikeLocale()
                );
                SmallDescriptionText.Text = StoreHelper.Instance.ExecuteScript(item.small_description, Utils.GetStoreLikeLocale());
                ItemFullDescriptionText.Text = StoreHelper.Instance.ExecuteScript(item.description, Utils.GetStoreLikeLocale());
                ItemWarningAera.Visibility = item.display_warning ? Visibility.Visible : Visibility.Collapsed;
                ItemWarningText.Text = StoreHelper.Instance.ExecuteScript(item.warning_text, Utils.GetStoreLikeLocale());

                if (DatabaseHelper.Instance.IsItemInstalled(storeId))
                {
                    ItemActionButton.IsEnabled = item.type == "component";
                    ItemActionButtonText.Text = localizer.GetLocalizedString("Setup");
                    ItemMoreButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ItemActionButtonText.Text = localizer.GetLocalizedString("Install");
                    ItemMoreButton.Visibility = Visibility.Collapsed;
                    ItemActionButton.IsEnabled = IsItemSupported();
                }

                if (item.links.Count > 0)
                    CreateLinks(item.links);
                else 
                    LinksGrid.Visibility = Visibility.Collapsed;

                loaded = true;
            }
            else
            {
                ItemName.Text = "Template page";
            }
        }

        private bool IsItemSupported()
        {
            Version curVer = new Version(StateHelper.Instance.Version);
            Version minV = new Version(item.target_minversion);

            if (curVer < minV) return false;

            if (item.target_maxversion != "NaN")
            {
                Version maxV = new Version(item.target_maxversion);
                if (curVer > maxV) return false;
            }
            return true;
        }

        private void ShowErrorDialog()
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                var dialog = new ContentDialog()
                {
                    XamlRoot = this.XamlRoot,
                    Title = localizer.GetLocalizedString("ElementNotExistTitle"),
                    Content = string.Format(localizer.GetLocalizedString("ElementNotExistMessage"), _storeId, "ERR_STORE_ITEM_NOT_FOUND"),
                    PrimaryButtonText = "OK"
                };
                await dialog.ShowAsync();
                if (Frame.CanGoBack)
                    Frame.GoBack();
                else
                    Frame.Navigate(typeof(HomePage));
            });
        }

        private void CreateLinks(List<StoreHelper.Link> links)
        {
            LinksStackPanel.Children.Clear();

            ObservableCollection<StoreItemLinkButton> linkButtons = new ObservableCollection<StoreItemLinkButton>();

            foreach (StoreHelper.Link link in links)
            {
                linkButtons.Add(new StoreItemLinkButton
                {
                    Text = StoreHelper.Instance.GetLocalizedStoreItemName(link.name, Utils.GetStoreLikeLocale()),
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
                        stageHeaderText = localizer.GetLocalizedString("GettingReady");
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    case "END":
                        stageHeaderText = localizer.GetLocalizedString("Finishing");
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    case "Downloading":
                        stageHeaderText = localizer.GetLocalizedString("Downloading");
                        StatusProgressbar.IsIndeterminate = false;
                        CurrentStatusSpeedTextBlock.Visibility = Visibility.Visible;
                        break;
                    case "Extracting":
                        stageHeaderText = localizer.GetLocalizedString("Installing");
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    case "ErrorHappens":
                        stageHeaderText = localizer.GetLocalizedString("ErrorHappens");
                        break;
                    case "Completed":
                        stageHeaderText = localizer.GetLocalizedString("Finishing");
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    case "CANC":
                        stageHeaderText = localizer.GetLocalizedString("Cancel");
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    case "ConnectingToService":
                        stageHeaderText = localizer.GetLocalizedString("ConnectingToService");
                        CurrentStatusTipTextBlock.Text = localizer.GetLocalizedString("ConnectingToServiceTip");
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                    default:
                        stageHeaderText = localizer.GetLocalizedString(stage);
                        StatusProgressbar.IsIndeterminate = true;
                        break;
                }

                CurrentStatusTextBlock.Text = stageHeaderText;
            };
            StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

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

            _itemTimeRemainingChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                TimeSpan time = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                    return;

                if (time.Minutes > 0)
                    CurrentStatusTipTextBlock.Text = string.Format(localizer.GetLocalizedString("/UIHelper/LeftMinutes"), time.Minutes);
                else
                    CurrentStatusTipTextBlock.Text = localizer.GetLocalizedString("/UIHelper/LeftSmall");
            };

            StoreHelper.Instance.ItemTimeRemainingChanged += _itemTimeRemainingChangedHandler;

            _itemDownloadSpeedChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                double speed = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != _storeId)
                    return;

                CurrentStatusSpeedTextBlock.Text = $"{Utils.FormatSpeed(speed)}, ";
            };

            StoreHelper.Instance.ItemDownloadSpeedChanged += _itemDownloadSpeedChangedHandler;

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

            if (_itemDownloadSpeedChangedHandler != null)
                StoreHelper.Instance.ItemDownloadSpeedChanged -= _itemDownloadSpeedChangedHandler;

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
            CurrentStatusTipTextBlock.Text = localizer.GetLocalizedString("WorkingOnIt");

            if (id == _storeId && !errorHappens)
            {
                ItemActionButtonText.Text = localizer.GetLocalizedString("Install");
                ItemMoreButton.Visibility = Visibility.Collapsed;
                ItemActionButton.Visibility = Visibility.Visible;
                DownloadStatusGrid.Visibility = Visibility.Collapsed;

                if (DatabaseHelper.Instance.IsItemInstalled(id))
                {
                    ItemActionButtonText.Text = localizer.GetLocalizedString("Setup");
                    ItemMoreButton.Visibility = Visibility.Visible;
                }
            }
        }

        private async void InstallingItemActions()
        {
            if (!await AskLicense()) return;
            ConnectHandlers();
            errorHappens = false;

            StopActionButton.IsEnabled = true;
            StatusProgressbar.IsIndeterminate = true;
            ErrorStatusGrid.Visibility = Visibility.Collapsed;
            ItemActionButton.Visibility = Visibility.Collapsed;
            DownloadStatusGrid.Visibility = Visibility.Visible;

            CurrentStatusTextBlock.Text = localizer.GetLocalizedString("QueueWaiting");
            StoreHelper.Instance.AddItemToQueue(_storeId, string.Empty);
        }

        private void ItemCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.CategoryViewPage), item.category_id, new SuppressNavigationTransitionInfo());
        }

        private async void ItemWarningButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog()
            {
                Title = localizer.GetLocalizedString("MessageFromDeveloper"),
                Content = localizer.GetLocalizedString("MessageFromDeveloperTip"),
                PrimaryButtonText = "OK",
                XamlRoot = this.XamlRoot,
            };
            await dialog.ShowAsync();
        }

        private async void ItemActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Helper.DatabaseHelper.Instance.IsItemInstalled(_storeId))
            {
                InstallingItemActions();
            }
            else
            {
                MainWindow window = await ((App)Application.Current).SafeCreateNewWindow<MainWindow>();

                window.NavView_Navigate(typeof(ViewComponentSettingsPage), _storeId, new DrillInNavigationTransitionInfo());
            }
        }

        private void StopActionButton_Click(object sender, RoutedEventArgs e)
        {
            StoreHelper.Instance.RemoveItemFromQueue(_storeId);
        }

        private async void ErrorHelpButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog()
            {
                Title = localizer.GetLocalizedString("AvailableActions"),
                Content = localizer.GetLocalizedString("AvailableActionsTip"),
                PrimaryButtonText = "OK",
                XamlRoot = this.XamlRoot,
            };
            await dialog.ShowAsync();
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
            CurrentStatusTextBlock.Text = localizer.GetLocalizedString("QueueWaiting");
            StatusProgressbar.IsIndeterminate = true;
            ErrorStatusGrid.Visibility = Visibility.Collapsed;
            ItemActionButton.Visibility = Visibility.Collapsed;
            DownloadStatusGrid.Visibility = Visibility.Visible;

            StoreHelper.Instance.RemoveItem(_storeId);
        }

        private void ReinstallButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: downloading from saved URL
            StoreHelper.Instance.RemoveItem(_storeId);
            InstallingItemActions();
        }

        private async Task<bool> AskLicense()
        {
            if (item.license.Count == 0)
                return true;

            AcceptLicenseContentDialog dialog = new()
            {
                Licenses = item.license,
                XamlRoot = this.XamlRoot
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
