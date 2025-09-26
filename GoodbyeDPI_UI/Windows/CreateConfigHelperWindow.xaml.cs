using CDPI_UI.Controls.Dialogs.CreateConfigHelper;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.CreateConfigHelper;
using CDPI_UI.Views.CreateConfigUtil;
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using WinUI3Localizer;
using WinUIEx;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CreateConfigHelperWindow : WindowEx
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private IntPtr _hwnd;
        private WindowProc _newWndProc;
        private IntPtr _oldWndProc;

        public static CreateConfigHelperWindow Instanse { get; private set; }
        public bool IsOperationExitAskAvailable { get; set; } = false;

        private bool IsDialogRequested = false;

        private ILocalizer localizer = Localizer.Get();

        public CreateConfigHelperWindow()
        {
            this.InitializeComponent();
            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("CreateConfigHelperWindowTitle"));
            InitializeWindow();
            TrySetMicaBackdrop(true);

            SetTitleBar(AppTitleBar);

            Instanse = this;

            this.ExtendsContentIntoTitleBar = true;
            ContentFrame.Navigate(typeof(Views.CreateConfigHelper.MainPage), null, new DrillInNavigationTransitionInfo());
            this.Closed += CreateConfigHelperWindow_Closed;

            if (this.Content is FrameworkElement fe)
            {
                fe.Loaded += Fe_Loaded;
            }
        }

        ~CreateConfigHelperWindow(){
            Debug.WriteLine("CreateConfigHelperWindow finalized");
        }

        private void Fe_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsDialogRequested)
            {
                OpenConfigEditPage(true);
            }

            if (this.Content is FrameworkElement fe)
            {
                fe.Loaded -= Fe_Loaded;
            }
        }

        private void CreateConfigHelperWindow_Closed(object sender, WindowEventArgs args)
        {
            Instanse = null;
        }

        private async void AskForExit(NavigatingCancelEventArgs e)
        {
            ContentDialog exitDialog = new ContentDialog()
            {
                Title = "Exit",
                Content = "Are you sure you want to exit? Unsaved changes will be lost.",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = this.Content.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"]
            };
            var result = await exitDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ContentFrame.DispatcherQueue.TryEnqueue(() =>
                {
                    IsOperationExitAskAvailable = false;
                    if (e.SourcePageType == typeof(Views.CreateConfigHelper.MainPage))
                    {
                        if (ContentFrame.CanGoBack)
                        {
                            RemoveAndGoBackTo(typeof(Views.CreateConfigHelper.MainPage), ContentFrame);
                            return;
                        }
                    }
                    ContentFrame.Navigate(e.SourcePageType, e.Parameter, new DrillInNavigationTransitionInfo());
                    
                });
            }
        }

        private void AuditMenuItemsEnabled(Type pageType)
        {
            HomeItem.IsEnabled = true;
            CreateNewConfigButton.IsEnabled = true;

            if (pageType == typeof(Views.CreateConfigHelper.MainPage))
                HomeItem.IsEnabled = false;
            else if (pageType == typeof(CreateNewConfigPage))
                CreateNewConfigButton.IsEnabled = false;
        }

        private void ContentFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (IsOperationExitAskAvailable)
            {
                AskForExit(e);
                e.Cancel = true;
                return;

            }

            if (e.Cancel != true)
            {
                AuditMenuItemsEnabled(e.SourcePageType);
                if (e.SourcePageType == typeof(Views.CreateConfigHelper.MainPage))
                {
                    ContentFrame.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (ContentFrame.CanGoBack)
                            ContentFrame.BackStack.Clear();
                    });
                }
                
            }
        }

        private bool RemoveAndGoBackTo(Type pageType, Frame rootFrame)
        {
            if (rootFrame == null) return false;

            var back = rootFrame.BackStack;
            int targetIndex = -1;
            for (int i = back.Count - 1; i >= 0; i--)
            {
                if (back[i].SourcePageType == pageType)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1) return false;

            for (int i = back.Count - 1; i > targetIndex; i--)
                back.RemoveAt(i);

            if (rootFrame.CanGoBack)
            {
                rootFrame.GoBack();
                return true;
            }
            return false;
        }

        private void HomeItem_Click(object sender, RoutedEventArgs e)
        {
            bool result = RemoveAndGoBackTo(typeof(Views.CreateConfigHelper.MainPage), ContentFrame);
            if (!result)
            {
                ContentFrame.Navigate(typeof(Views.CreateConfigHelper.MainPage), null, new DrillInNavigationTransitionInfo());
            }
        }

        public void OpenConfigEditPage(bool skp = false)
        {
            IsDialogRequested = true;
            if (skp)
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    SelectConfigToEditContentDialog dialog = new SelectConfigToEditContentDialog()
                    {
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                    if (dialog.SelectedConfigResult == SelectResult.Selected)
                    {
                        ConfigItem configItem = dialog.SelectedConfigItem;
                        ContentFrame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGEDIT", configItem), new DrillInNavigationTransitionInfo());
                    }
                });
            }
        }

        public void CreateNewConfigForComponentId(string componentId)
        {
            ContentFrame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGCREATEBYID", componentId), new DrillInNavigationTransitionInfo());
        }

        private void CreateNewConfigButton_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(CreateNewConfigPage), null, new DrillInNavigationTransitionInfo());
        }

        private async void ImportConfigFromFileButton_Click(object sender, RoutedEventArgs e)
        {
            ImportConfigFromFileDialog dialog = new ImportConfigFromFileDialog() { XamlRoot = this.Content.XamlRoot};
            await dialog.ShowAsync();

            string filePath;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Choose config file";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                openFileDialog.FilterIndex = 4;

                openFileDialog.Filter = "JSON configs (*.json)|*.json|BAT config files (*.bat)|*.bat|CMD config files (*.cmd)|*.cmd|All compacible config files (*.bat, *.cmd, *.json)|*.bat;*.cmd;*.json";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                    var (configItem, errorHappens) = ConfigHelper.LoadConfigFromFile(filePath);
                    ContentFrame.Navigate(typeof(CreateNewConfigPage), Tuple.Create("CFGIMPORT", configItem, errorHappens, filePath), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    return;
                }
            }
        }

        private void EditConfigButton_Click(object sender, RoutedEventArgs e)
        {
            OpenConfigEditPage(true);
        }

        public void OpenGoodCheckReportFromFile(string filePath)
        {
            ContentFrame.Navigate(typeof(ViewGoodCheckReportPage), Tuple.Create(NavigationState.LoadFileFromPath, filePath), new DrillInNavigationTransitionInfo());
        }

        private void OpenGoodCheckReportButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Choose GoodCheck report file";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                openFileDialog.Filter = "XML data files (*.xml)|*.xml";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                    ContentFrame.Navigate(typeof(ViewGoodCheckReportPage), Tuple.Create(NavigationState.LoadFileFromPath, filePath), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    return;
                }
            }
            
        }

        private async void RecentGoodCheckSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            RecentGoodCheckSelectionsContentDialog dialog = new RecentGoodCheckSelectionsContentDialog()
            {
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
            if (dialog.SelectedResult == SelectResult.Selected)
            {
                string directory = dialog.SelectedReport;
                ContentFrame.Navigate(typeof(ViewGoodCheckReportPage), Tuple.Create(NavigationState.LoadFileFromPath, directory), new DrillInNavigationTransitionInfo());
            }
        }

        private async void BeginNewGoodCheckSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfigUtilWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigUtilWindow>();
            // window.NavigateToPage<CreateViaGoodCheck>();
        }

        #region WINAPI

        private void InitializeWindow()
        {
            _hwnd = WindowNative.GetWindowHandle(this);
            _newWndProc = new WindowProc(NewWindowProc);
            _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        }

        private delegate IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private IntPtr NewWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                MINMAXINFO minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                minMaxInfo.ptMinTrackSize.x = 484;
                minMaxInfo.ptMinTrackSize.y = 300;
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
        bool TrySetMicaBackdrop(bool useMicaAlt)
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                micaBackdrop.Kind = useMicaAlt ? Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt : Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base;
                this.SystemBackdrop = micaBackdrop;

                return true;
            }

            return false;
        }








        #endregion

        private async void ComponentsStore_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), "C001CS", new SuppressNavigationTransitionInfo());
        }

        private async void AddOnsStore_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), "C003AS", new SuppressNavigationTransitionInfo());
        }

        private async void ConfigsStore_Click(object sender, RoutedEventArgs e)
        {
            StoreWindow window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), "C002CS", new SuppressNavigationTransitionInfo());
        }

        private void Store_Click(object sender, RoutedEventArgs e)
        {
            _ = ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
        }

        private async void ExtiMenuButton_Click(object sender, RoutedEventArgs e)
        {
            CreateConfigHelperWindow window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
            window.Close();
        }

        private void ReportIssueButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchReportUrl();
        }

        private void OfflineHelpButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Add offline help 
        }

        private void OnlineHelpButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchWikiUrl();
        }
    }
}
