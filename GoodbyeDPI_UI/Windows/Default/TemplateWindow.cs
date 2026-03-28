using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using WinUIEx;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using Application = Microsoft.UI.Xaml.Application;
using Size = System.Windows.Size;

namespace CDPI_UI.Default
{
    public partial class TemplateWindow : WindowEx
    {
        public new AppWindow AppWindow = null;
        public OverlappedPresenter OverlappedPresenter = null;

        public Action NewIdSet;

        private Size WindowSizeProperty = new(484, 300);
        public Size WindowMinSize
        {
            get
            {
                return WindowSizeProperty;
            }
            set
            {
                if (WindowSizeProperty == value) { return; }
                WindowSizeProperty = value;
                UpdateWindowMinSize();
            }
        }

        private string IconUriProperty = "";
        public string IconUri
        {
            get
            {
                return IconUriProperty;
            }
            set
            {
                IconUriProperty = value;
                UpdateWindowIcon();
            }
        }

        private FrameworkElement TitleBarProperty = null;
        public FrameworkElement TitleBar
        {
            get
            {
                return TitleBarProperty;
            }
            set
            {
                TitleBarProperty = value;
            }
        }

        private FrameworkElement TitleIconProperty = null;
        public FrameworkElement TitleIcon
        {
            get
            {
                return TitleIconProperty;
            }
            set
            {
                TitleIconProperty = value;
                AssignActionHandlerToWindowIcon();
            }
        }

        private bool IsAppShownInSwitchersProperty = false;
        public bool IsAppShownInSwitchers
        {
            get
            {
                return IsAppShownInSwitchersProperty;
            }
            set
            {
                IsAppShownInSwitchersProperty = value;
                UpdateAppShownInSwitcher();
            }
        }

        private string IdProperty = string.Empty;
        public string Id
        {
            get
            {
                return IdProperty;
            }
            set
            {
                IdProperty = value;
                NewIdSet?.Invoke();
            }
        }

        public TemplateWindow()
        {
            GetAppWindowAndPresenter();

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ((App)Application.Current).CurrentTheme;
            }

            WindowHelper.SetWindowBorderMargin(this);

            ExtendsContentIntoTitleBar = true;

            Activated += DefaultWindow_Activated;
            Closed += DefaultWindow_Closed;

            if (((App)Application.Current).CurrentTheme == ElementTheme.Dark)
            {
                ApplyDarkThemeToSystemMenu();
            }

            UpdateWindowMinSize();
        }

        public void DisableResizeFeature(bool isMinimizable = false)
        {
            var appWindowPresenter = this.AppWindow.Presenter as OverlappedPresenter;
            appWindowPresenter.IsResizable = false;
            appWindowPresenter.IsMaximizable = false;
            appWindowPresenter.IsMinimizable = isMinimizable;

            NativeWindowHelper.ForceDisableMaximize(this);
        }

        private void DefaultWindow_Closed(object sender, WindowEventArgs args)
        {
            if (args.Handled) return;
            Closed -= DefaultWindow_Closed;
            Activated -= DefaultWindow_Activated;
            if (TitleIcon != null) TitleIcon.PointerPressed -= TitleIcon_PointerPressed;
        }

        private void DefaultWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            UpdateWindowMinSize();

            if (TitleBar == null) return;
            if (args.WindowActivationState == WindowActivationState.CodeActivated ||
                args.WindowActivationState == WindowActivationState.PointerActivated)
            {
                AssignStateToAllFrameworkElementChildrens(TitleBar, true);
            }
            else
            {
                AssignStateToAllFrameworkElementChildrens(TitleBar, false);
            }
        }

        private static void AssignStateToAllFrameworkElementChildrens(FrameworkElement frameworkElement, bool isEnabled)
        {
            try
            {
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(frameworkElement); i++)
                {
                    var child = VisualTreeHelper.GetChild(frameworkElement, i);
                    if (child is TextBlock textBlock)
                    {
                        Style captionStyle = (Style)((App)Application.Current).Resources["AppBarTipTextBlockStyle"];
                        if (textBlock.Style != captionStyle)
                        {
                            textBlock.Style = (Style)(isEnabled ? ((App)Application.Current).Resources["DefaultTextBlockStyle"] : ((App)Application.Current).Resources["DisabledTextBlockStyle"]);
                        }
                    }
                }
            }
            catch { }
        }

        public void GetAppWindowAndPresenter()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow = AppWindow.GetFromWindowId(myWndId);
            OverlappedPresenter = AppWindow.Presenter as OverlappedPresenter;
        }

        private void UpdateAppShownInSwitcher()
        {
            AppWindow.IsShownInSwitchers = IsAppShownInSwitchers;
        }

        private long LastTimestamp = 0;
        private readonly int DoubleClickTimeMS = 250;

        private void AssignActionHandlerToWindowIcon()
        {
            if (TitleIcon == null) return;

            TitleIcon.PointerPressed += TitleIcon_PointerPressed;
        }

        private void TitleIcon_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            if (milliseconds - LastTimestamp < DoubleClickTimeMS && milliseconds != 0)
            {
                this.Close();
            }
            else
            {
                LastTimestamp = milliseconds;
            }

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            GetWindowRect(hWnd, out RECT pos);
            IntPtr hMenu = GetSystemMenu(hWnd, false);
            int cmd = TrackPopupMenu(hMenu, 0x100, pos.left + 10, pos.top + 40, 0, hWnd, IntPtr.Zero);
            if (cmd > 0) SendMessage(hWnd, 0x112, (IntPtr)cmd, IntPtr.Zero);
        }

        private void UpdateWindowIcon()
        {
            WindowHelper.SetWindowIcon(this, IconUri);
        }

        private void UpdateWindowMinSize()
        {
            MinWidth = (int)WindowMinSize.Width;
            MinHeight = (int)WindowMinSize.Height;
        }

        public static bool RemoveAndGoBackTo(Type pageType, Frame rootFrame)
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

        public void ToggleLoadingState(TaskbarProgressBarState loadingState, int currentLoadingValue = 0, int maxLoadingValue = 100)
        {
            TaskbarManager.Instance.SetProgressState(loadingState);
            if (loadingState != TaskbarProgressBarState.Indeterminate)
                TaskbarManager.Instance.SetProgressValue(currentLoadingValue, maxLoadingValue);
        }

        #region WINAPI


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

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
           int nReserved, IntPtr hWnd, IntPtr prcRect);
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        struct RECT { public int left, top, right, bottom; }

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
