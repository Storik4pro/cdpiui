using CDPI_UI.Helper;
using CDPI_UI.Helper.LScript;
using CDPI_UI.Helper.Static;
using CDPI_UI.Helper.Store;
using Microsoft.UI;
using Microsoft.UI.Windowing;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.WindowManagement;
using WinRT.Interop;
using WinUI3Localizer;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Messages
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StoreLocalItemInstallingDialog : WindowEx
    {
        private string StoreId = string.Empty;
        private string Name = string.Empty;
        private string PackFilePath = string.Empty;

        private ILocalizer localizer = Localizer.Get();

        private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
        private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
        private Action<Tuple<string, double>> _itemDownloadSpeedChangedHandler;
        private Action<Tuple<string, TimeSpan>> _itemTimeRemainingChangedHandler;
        private Action<Tuple<string, string>> _itemInstallingErrorHappensHandler;
        private Action<string> _itemActionsStoppedHandler;

        private bool IsErrorHappens = false;

        public StoreLocalItemInstallingDialog()
        {
            InitializeComponent();

            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("StoreWindowsTitle"));

            var appWindowPresenter = this.AppWindow.Presenter as OverlappedPresenter;
            appWindowPresenter.IsResizable = false;
            appWindowPresenter.IsMaximizable = false;
            appWindowPresenter.IsMinimizable = false;
            

            NativeWindowHelper.ForceDisableMaximize(this);

            ((App)Application.Current).OpenWindows.Add(this);

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ((App)Application.Current).CurrentTheme;
            }

            ExtendsContentIntoTitleBar = true;

            ToggleItemLoadingMode(true);
            SetLoadingState(true, localizer.GetLocalizedString("SatusGettingPackInfo"));

            SetTitleBar(WindowMoveAera);

            this.Closed += StoreLocalItemInstallingDialog_Closed;
            this.Activated += StoreLocalItemInstallingDialog_Activated;
        }

        private void StoreLocalItemInstallingDialog_Activated(object sender, WindowActivatedEventArgs args)
        {
            ((App)Application.Current).ShowWindowModalAsync(this);
            this.Activated -= StoreLocalItemInstallingDialog_Activated;
        }

        private void StoreLocalItemInstallingDialog_Closed(object sender, WindowEventArgs args)
        {
            LocalItemsInstallerHelper.Instance.ErrorHappens -= LocalItemsInstallerHelper_ErrorHappens;

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
        }

        public async void SetPackFilePath(string packFile)
        {
            if (!string.IsNullOrEmpty(PackFilePath)) return; // TODO: Show notification : "Only one operation..."
            
            PackFilePath = packFile;
            ConnectHandlers();
            var model = await LocalItemsInstallerHelper.Instance.ImportStoreItemPackFile(PackFilePath);

            try
            {

                if (model != null)
                {
                    StoreId = model.StoreId;
                    Name = model.ShortName ?? StoreHelper.Instance.GetLocalizedStoreItemName(model.Name, Utils.GetStoreLikeLocale());

                    ItemNameTextBlock.Text = string.Format(localizer.GetLocalizedString("StoreSmallInstallItemName"), Name);
                    ItemImage.Source = new BitmapImage(new Uri(LScriptLangHelper.ExecuteScript(model.Icon)));
                    DeveloperTextBlock.Text = string.Format(localizer.GetLocalizedString("StoreSmallDeveloperText"), model.Developer);
                    CategoryTextBlock.Text = string.Format(localizer.GetLocalizedString("Source"), packFile);
                    VersionTextBlock.Text = string.Format(localizer.GetLocalizedString("Version"), model.Version);

                    if (!string.IsNullOrEmpty(model.Color))
                        ItemColorRectangle.Fill = UIHelper.HexToSolidColorBrushConverter(model.Color);

                    ToggleItemLoadingMode(false);
                    DownloadProgressStackPanel.Visibility = Visibility.Collapsed;

                    if (DatabaseHelper.Instance.IsItemInstalled(StoreId))
                    {
                        string curV = DatabaseHelper.Instance.GetItemById(StoreId)?.CurrentVersion ?? "0.0.0.0";
                        string iV = model.Version;

                        if (!string.IsNullOrEmpty(curV) && !string.IsNullOrEmpty(iV))
                        {
                            if (curV.Split(".").Length <= 2)
                            {
                                curV += ".0";
                            }
                            
                            if (iV.Split(".").Length <= 2)
                            {
                                iV += ".0";
                            }
                            var currentVersion = new Version(curV.Replace("v", ""));
                            var serverVersion = new Version(iV.Replace("v", ""));

                            if (currentVersion >= serverVersion)
                            {
                                ErrorHappens(localizer.GetLocalizedString("ItemAlreadyInstalledWarn"));
                            }
                        }
                        else
                        {
                            ErrorHappens(localizer.GetLocalizedString("ItemAlreadyInstalledWarn"));
                        }

                        
                    }

                }
            }
            catch (Exception ex)
            {
                ErrorHappens($"{ex.Message}");
            }
        }

        private void ToggleItemLoadingMode(bool isLoading)
        {
            Visibility visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

            GetButton.IsEnabled = !isLoading;
            ViewInStoreButton.IsEnabled = !isLoading;

            TitleShimmer.Visibility = visibility;
            DeveloperShimmer.Visibility = visibility;
            CategoryShimmer.Visibility = visibility;
            ImageShimmer.Visibility = visibility;
            DescriptionShimmer.Visibility = visibility;
            VersionShimmer.Visibility = visibility;
        }

        private void SetLoadingState(bool isLoading, string nowLoading = "")
        {
            DownloadProgressStackPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            CurrentStatusTextBlock.Text = nowLoading;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            StoreHelper.Instance.RemoveItemFromQueue(StoreId);
            this.Close();
        }

        private async void ViewInStoreButton_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.ItemViewPage), StoreId, new SuppressNavigationTransitionInfo());
            this.Close();
        }

        private void GetButton_Click(object sender, RoutedEventArgs e)
        {
            LocalItemsInstallerHelper.Instance.BeginLocalItemInstalling(PackFilePath);
        }

        private void ErrorHappens(string message)
        {
            ItemSmallDescriptionStackPanel.Visibility = Visibility.Collapsed;
            DownloadProgressStackPanel.Visibility = Visibility.Collapsed;
            GetButton.Visibility = Visibility.Collapsed;
            ErrorGrid.Visibility = Visibility.Visible;

            ErrorCodeTextBlock.Text = message;

            CancelButton.Content = "OK";
        }

        private void GetReadyUI()
        {
            GetButton.Visibility = Visibility.Collapsed;
            StatusProgressbar.IsIndeterminate = true;

            ErrorGrid.Visibility = Visibility.Collapsed;
            DownloadProgressStackPanel.Visibility = Visibility.Visible;
        }

        #region ActionHandlers

        private void ConnectHandlers()
        {
            LocalItemsInstallerHelper.Instance.ErrorHappens += LocalItemsInstallerHelper_ErrorHappens;
            
            _itemDownloadStageChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                string stage = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                    return;

                if (IsErrorHappens) return;

                GetReadyUI();

                CurrentStatusSpeedTextBlock.Visibility = Visibility.Collapsed;

                PreferItemDownloadingStateActions(stage);
            };
            StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

            _itemDownloadProgressChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                double progress = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                    return;

                if (IsErrorHappens) return;

                StatusProgressbar.IsIndeterminate = false;
                StatusProgressbar.Value = progress;
            };
            StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;

            _itemTimeRemainingChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                TimeSpan time = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                    return;
            };

            StoreHelper.Instance.ItemTimeRemainingChanged += _itemTimeRemainingChangedHandler;

            _itemDownloadSpeedChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                double speed = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                    return;

                CurrentStatusSpeedTextBlock.Text = $"{Utils.FormatSpeed(speed)}";
            };

            StoreHelper.Instance.ItemDownloadSpeedChanged += _itemDownloadSpeedChangedHandler;

            _itemInstallingErrorHappensHandler = (data) =>
            {
                string operationId = data.Item1;
                string errorCode = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                    return;

                IsErrorHappens = true;

                ItemSmallDescriptionStackPanel.Visibility = Visibility.Collapsed;
                DownloadProgressStackPanel.Visibility = Visibility.Collapsed;
                ErrorGrid.Visibility = Visibility.Visible;

                ErrorCodeTextBlock.Text = errorCode;

                CancelButton.Content = "OK";
            };
            StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;

            _itemActionsStoppedHandler = (id) =>
            {
                if (!IsErrorHappens)
                    ShowItemAfterInstallActions();
            };
            StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;
            
        }

        private void PreferItemDownloadingStateActions(string state)
        {
            string stageHeaderText = string.Empty;
            switch (state)
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
                    StatusProgressbar.IsIndeterminate = true;
                    break;
                default:
                    stageHeaderText = localizer.GetLocalizedString(state);
                    StatusProgressbar.IsIndeterminate = true;
                    break;
            }
            CurrentStatusTextBlock.Text = stageHeaderText;
        }

        private void ShowItemAfterInstallActions()
        {
            DownloadProgressStackPanel.Visibility = Visibility.Collapsed;
            CancelButton.Content = "OK";

            ItemNameTextBlock.Text = string.Format(localizer.GetLocalizedString("StoreSmallItemNameComplete"), Name);
        }

        private void LocalItemsInstallerHelper_ErrorHappens(string errorCode)
        {
            ErrorHappens(errorCode);
        }

        #endregion

        
    }
}
