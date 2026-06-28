using CDPI_UI.Controls.WindowControls;
using CDPI_UI.DesktopWap.Helper;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public enum TitleBarModes
    {
        System = 0,
        Custom = 1,
    }
    public partial class TemplateWindow : WindowEx
    {
        public new AppWindow AppWindow = null;
        public OverlappedPresenter OverlappedPresenter = null;

        public Action NewIdSet;

        private string WindowTitleProperty = "CDPI UI";
        public string WindowTitle
        {
            get
            {
                return WindowTitleProperty;
            }
            set
            {
                WindowTitleProperty = value;
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (WindowTitle != null)
                    {
                        Title = UIHelper.GetWindowName(WindowTitle);
                        if (CustomTitleBarUserControl != null) CustomTitleBarUserControl.Title = WindowTitle;
                    }
                });
            }
        }

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

        private TitleBarUserControl CustomTitleBarUserControlProperty = null;
        public TitleBarUserControl CustomTitleBarUserControl
        {
            get
            {
                return CustomTitleBarUserControlProperty;
            }
            set
            {
                CustomTitleBarUserControlProperty = value;
                InitializeTitleBar();
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

        private TitleBarModes titleBarMode = TitleBarModes.Custom;
        public TitleBarModes TitleBarMode
        {
            get
            {
                return titleBarMode;
            }
            set
            {
                titleBarMode = value;
                SetTitleBarMode(TitleBarMode);
            }
        }

        public TemplateWindow()
        {
            this.Title = "CDPI UI";

            GetAppWindowAndPresenter();

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ((App)Application.Current).CurrentTheme;
            }

            WindowHelper.SetWindowBorderMargin(this);

            TitleBarMode = (TitleBarModes)SettingsManager.Instance.GetValue<int>("APPEARANCE", "titleBarMode");

            Activated += DefaultWindow_Activated;
            Closed += DefaultWindow_Closed;

            if (((App)Application.Current).CurrentTheme == ElementTheme.Dark)
            {
                ApplyDarkThemeToSystemMenu();
            }

            UpdateWindowMinSize();
        }

        private void SetTitleBarMode(TitleBarModes mode)
        {
            
                switch (mode)
                {
                    case TitleBarModes.System:
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (CustomTitleBarUserControl != null) CustomTitleBarUserControl.Visibility = Visibility.Collapsed;
                        });
                        ExtendsContentIntoTitleBar = false;
                        break;
                    case TitleBarModes.Custom:
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (CustomTitleBarUserControl != null) CustomTitleBarUserControl.Visibility = Visibility.Visible;
                        });
                        ExtendsContentIntoTitleBar = true;
                        break;
                }
        }

        private void InitializeTitleBar()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (CustomTitleBarUserControlProperty == null) return;

                SetTitleBar(CustomTitleBarUserControl.WindowMoveAera);
                AssignActionHandlerToWindowIcon();
                CustomTitleBarUserControl.Visibility = TitleBarMode == TitleBarModes.Custom ? Visibility.Visible : Visibility.Collapsed;
                CustomTitleBarUserControl.Title = WindowTitle;
                if (!string.IsNullOrEmpty(IconUri)) CustomTitleBarUserControl.IconSource = new BitmapImage(new Uri($"ms-appx:///{IconUri}"));
            });
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
            if (CustomTitleBarUserControl?.ImageAera != null) CustomTitleBarUserControl.ImageAera.PointerPressed -= TitleIcon_PointerPressed;
        }

        private void DefaultWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            UpdateWindowMinSize();

            if (CustomTitleBarUserControl?.AppTitleTextBlock == null) return;
            if (args.WindowActivationState == WindowActivationState.CodeActivated ||
                args.WindowActivationState == WindowActivationState.PointerActivated)
            {
                AssignStateToTextBlock(CustomTitleBarUserControl.AppTitleTextBlock, true);
            }
            else
            {
                AssignStateToTextBlock(CustomTitleBarUserControl.AppTitleTextBlock, false);
            }
        }

        private static void AssignStateToTextBlock(TextBlock textBlock, bool isEnabled)
        {
            Style captionStyle = (Style)((App)Application.Current).Resources["AppBarTipTextBlockStyle"];
            if (textBlock.Style != captionStyle)
            {
                textBlock.Style = (Style)(isEnabled ? ((App)Application.Current).Resources["DefaultTextBlockStyle"] : ((App)Application.Current).Resources["DisabledTextBlockStyle"]);
            }
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
            if (CustomTitleBarUserControl?.ImageAera == null) return;

            CustomTitleBarUserControl.ImageAera.PointerPressed += TitleIcon_PointerPressed;
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
            DispatcherQueue.TryEnqueue(() =>
            {
                if (CustomTitleBarUserControl != null) CustomTitleBarUserControl.IconSource = new BitmapImage(new Uri($"ms-appx:///{IconUri}"));
            });
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

        public static void ToggleLoadingState(TaskbarProgressBarState loadingState, int currentLoadingValue = 0, int maxLoadingValue = 100)
        {
            TaskbarManager.Instance.SetProgressState(loadingState);
            if (loadingState != TaskbarProgressBarState.Indeterminate)
                TaskbarManager.Instance.SetProgressValue(currentLoadingValue, maxLoadingValue);
        }

        public event PropertyChangedEventHandler PropertyChanged;

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
