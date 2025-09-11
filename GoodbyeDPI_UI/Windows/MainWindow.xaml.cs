using CDPI_UI.Helper;
using CDPI_UI.Views;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
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
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using WinRT.Interop;
using WinUIEx;


namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    
    public sealed partial class MainWindow : WindowEx
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private IntPtr _hwnd;
        private WindowProc _newWndProc;
        private IntPtr _oldWndProc;

        private CanvasSwapChain _swapChain;

        public static MainWindow Instance { get; private set; }

        public MainWindow()
        {
            this.InitializeComponent();
            ApplyDarkThemeToSystemMenu();
            InitializeWindow();

            Instance = this;

            WindowHelper.SetWindowSize(this, 800, 600);
            ((App)Application.Current).OpenWindows.Add(this);

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ((App)Application.Current).CurrentTheme;
            }

            ExtendsContentIntoTitleBar = true;

            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(Views.MainPage));
            SetTitleBar(WindowMoveAera);
            NavView.SelectionChanged += NavView_SelectionChanged;

            StoreHelper.Instance.ItemRemoved += Instance_ItemRemoved;
            StoreHelper.Instance.ItemActionsStopped += Instance_ItemActionsStopped;

            AuditNavigationItems();
        }

        private void AuditNavigationItems()
        {
            if (DatabaseHelper.Instance.IsItemInstalled(StateHelper.Instance.FindKeyByValue("Zapret")))
            {
                ZapretNavigationViewItem.Visibility = Visibility.Visible;
            }
            else
            {
                ZapretNavigationViewItem.Visibility = Visibility.Collapsed;
            }

            if (ContentFrame.CurrentSourcePageType == typeof(ZapretSettingsPage))
            {
                ContentFrame.Navigate(typeof(MainPage));
            }
        }

        private void Instance_ItemActionsStopped(string obj)
        {
            AuditNavigationItems();
        }

        private void Instance_ItemRemoved(string obj)
        {
            AuditNavigationItems();
        }

        ~MainWindow()
        {
            ((App)Application.Current).OpenWindows.Remove(this);
        }

        private void InitializeWindow()
        {
            _hwnd = WindowNative.GetWindowHandle(this);
            _newWndProc = new WindowProc(NewWindowProc);
            _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        }

        private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            AppTitleBar.Margin = new Thickness()
            {
                Left = AppTitleBar.Margin.Left,
                Top = AppTitleBar.Margin.Top,
                Right = AppTitleBar.Margin.Right,
                Bottom = AppTitleBar.Margin.Bottom
            };
        }
        private double NavViewCompactModeThresholdWidth { get { return NavView.CompactModeThresholdWidth; } }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigated += On_Navigated;

            NavView.SelectedItem = NavView.MenuItems[0];

            NavView_Navigate(typeof(Views.MainPage), new EntranceNavigationTransitionInfo());
        }

        private void NavView_ItemInvoked(NavigationView sender,
                                         NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer.Tag.ToString() == "AddNewComponent")
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (AddNavigationViewFlyout.IsOpen)
                        AddNavigationViewFlyout.Hide();

                    AddNavigationViewFlyout.ShowAt(AddNaviagationViewItem);
                });
                Logger.Instance.CreateDebugLog(nameof(MainWindow), "FLY OPEN");
                return;
            }
            if (args.IsSettingsInvoked == true)
            {
                // pass
            }
            else if (args.InvokedItemContainer != null)
            {
                Type navPageType = Type.GetType(args.InvokedItemContainer.Tag.ToString());
                NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
            }
        }

        private void NavView_SelectionChanged(NavigationView sender,
                                              NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected == true)
            {
                // pass
            }
            else if (args.SelectedItemContainer != null)
            {
                Type navPageType = Type.GetType(args.SelectedItemContainer.Tag.ToString());
                NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
                
            }
        }

        private void NavView_Navigate(
            Type navPageType,
            NavigationTransitionInfo transitionInfo)
        {
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            if (navPageType is not null && !Type.Equals(preNavPageType, navPageType))
            {
                ContentFrame.Navigate(navPageType, null, transitionInfo);
            }
        }

        private void NavView_BackRequested(NavigationView sender,
                                           NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            NavView.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.SourcePageType != null)
            {
                Debug.WriteLine(ContentFrame.SourcePageType.FullName.ToString());
                try
                {
                    NavView.SelectedItem = NavView.MenuItems
                                .OfType<NavigationViewItem>()
                                .First(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));
                    BackButton.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex) { Debug.WriteLine(ex);  }
            }
        }

        public void NavigateSubPage(Type page, SlideNavigationTransitionEffect effect)
        {
            try
            {
                ContentFrame.Navigate(page, null, new SlideNavigationTransitionInfo() { Effect = effect });
                BackButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex) 
            { 
            Debug.WriteLine(ex);
            }

        }

        private void SetWindowSize(int width, int height)
        {
            var hwnd = WindowNative.GetWindowHandle(this);

            Microsoft.UI.WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        }

        private void BackButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(this.SearchAnimatedIcon, "PointerOver");
        }

        private void BackButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(this.SearchAnimatedIcon, "Normal");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }
        #region WINAPI

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

        private void ApplyDarkThemeToSystemMenu()
        {
            SetPreferredAppMode(2);
            FlushMenuThemes();
        }

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SetPreferredAppMode(int preferredAppMode);

        [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern void FlushMenuThemes();

        #endregion
    }
}
