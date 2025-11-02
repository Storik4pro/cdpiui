using CDPI_UI.Controls.Dialogs.Store;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
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
    public sealed partial class StoreSmallDownloadDialog : WindowEx
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private IntPtr _hwnd;
        private WindowProc _newWndProc;
        private IntPtr _oldWndProc;

        private ILocalizer localizer = Localizer.Get();

        private string StoreId = string.Empty;

        private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
        private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
        private Action<Tuple<string, double>> _itemDownloadSpeedChangedHandler;
        private Action<Tuple<string, TimeSpan>> _itemTimeRemainingChangedHandler;
        private Action<Tuple<string, string>> _itemInstallingErrorHappensHandler;
        private Action<string> _itemActionsStoppedHandler;

        private Helper.StoreHelper.RepoCategoryItem item;

        public StoreSmallDownloadDialog()
        {
            InitializeComponent();
            InitializeWindow();

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

            SetTitleBar(WindowMoveAera);

            ToggleItemLoadingMode(true);

            this.Closed += StoreSmallDownloadDialog_Closed;
        }

        private void StoreSmallDownloadDialog_Closed(object sender, WindowEventArgs args)
        {
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

        private void InitializeWindow()
        {
            _hwnd = WindowNative.GetWindowHandle(this);
            _newWndProc = new WindowProc(NewWindowProc);
            _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
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
        }

        public async void SetItemToViewId(string id)
        {
            StoreId = id;
            await StoreHelper.Instance.LoadAllStoreDatabase(forseSync: true);
            LoadItemInfo();
        }

        private void LoadItemInfo()
        {
            item = Helper.StoreHelper.Instance.GetItemInfoFromStoreId(StoreId);

            if (item == null) return;

            ConnectHandlers();
            CheckCurrentStatus();

            ItemNameTextBlock.Text = string.Format(localizer.GetLocalizedString("StoreSmallItemName"), item.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(item.name, Utils.GetStoreLikeLocale()));
            ItemImage.Source = new BitmapImage(new Uri(StoreHelper.Instance.ExecuteScript(item.icon)));
            DeveloperTextBlock.Text = string.Format(localizer.GetLocalizedString("StoreSmallDeveloperText"), item.developer);
            CategoryTextBlock.Text = string.Format(localizer.GetLocalizedString("StoreSmallCategoryText"), StoreHelper.Instance.GetLocalizedStoreItemName(
                    StoreHelper.Instance.GetCategoryFromStoreId(item.category_id).name,
                    Utils.GetStoreLikeLocale()
                ));
            ItemSmallDescriptionTextBlock.Text = StoreHelper.Instance.ExecuteScript(item.small_description, Utils.GetStoreLikeLocale());

            ToggleItemLoadingMode(false);

            if (DatabaseHelper.Instance.IsItemInstalled(StoreId))
            {
                CancelButton.Content = "OK";

                ItemSmallDescriptionStackPanel.Visibility = Visibility.Collapsed;
                DownloadProgressStackPanel.Visibility = Visibility.Collapsed;
                ErrorGrid.Visibility = Visibility.Visible;
                GetButton.Visibility = Visibility.Collapsed;

                ErrorCodeTextBlock.Text = "ERR_ITEM_ALLREADY_INSTALLED";
            }
            
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            StoreHelper.Instance.RemoveItemFromQueue(StoreId);
            this.Close();
        }

        private void GetButton_Click(object sender, RoutedEventArgs e)
        {
            StartDownload();
        }

        private async void ViewInStoreButton_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.ItemViewPage), StoreId, new SuppressNavigationTransitionInfo());
            this.Close();
        }

        private void GetReadyUI()
        {
            GetButton.Visibility = Visibility.Collapsed;
            StatusProgressbar.IsIndeterminate = true;

            ErrorGrid.Visibility = Visibility.Collapsed;
            DownloadProgressStackPanel.Visibility = Visibility.Visible;
        }

        private async void StartDownload()
        {
            if (!await AskLicense()) return;

            GetReadyUI();

            CurrentStatusTextBlock.Text = localizer.GetLocalizedString("QueueWaiting");
            StoreHelper.Instance.AddItemToQueue(StoreId, string.Empty);
        }

        private async Task<bool> AskLicense()
        {
            if (item.license == null || item.license.Count == 0)
                return true;

            AcceptLicenseContentDialog dialog = new()
            {
                Licenses = item.license,
                XamlRoot = this.Content.XamlRoot
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

        private void CheckCurrentStatus()
        {
            string operationId = StoreHelper.Instance.GetOperationIdFromItemId(StoreId);
            if (!string.IsNullOrEmpty(operationId))
            {
                GetReadyUI();

                string status = StoreHelper.Instance.GetQueueItemFromOperationId(operationId)?.DownloadStage;
                PreferItemDownloadingStateActions(status);
            }
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

        private void ConnectHandlers()
        {
            _itemDownloadStageChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                string stage = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                    return;

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
                
                ItemSmallDescriptionStackPanel.Visibility = Visibility.Collapsed;
                DownloadProgressStackPanel.Visibility = Visibility.Collapsed;
                ErrorGrid.Visibility = Visibility.Visible;

                ErrorCodeTextBlock.Text = errorCode;

                CancelButton.Content = "OK";
            };
            StoreHelper.Instance.ItemInstallingErrorHappens += _itemInstallingErrorHappensHandler;

            _itemActionsStoppedHandler = (id) =>
            {
                ShowItemAfterInstallActions();
            };
            StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;
        }

        private void ShowItemAfterInstallActions()
        {
            if (item == null) return;
            DownloadProgressStackPanel.Visibility = Visibility.Collapsed;
            CancelButton.Content = "OK";

            ItemNameTextBlock.Text = string.Format(localizer.GetLocalizedString("StoreSmallItemNameComplete"), item.short_name ?? StoreHelper.Instance.GetLocalizedStoreItemName(item.name, Utils.GetStoreLikeLocale()));
        }

        #region WINAPI

        private delegate IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private IntPtr NewWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                MINMAXINFO minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                Marshal.StructureToPtr(minMaxInfo, lParam, true);
            }
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        private const int GWLP_WNDPROC = -4;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        #endregion

        
    }
}
