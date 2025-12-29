using CDPI_UI.Messages;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;
using WinUIEx;

namespace CDPI_UI.Helper
{
    public class WindowHelper
    {
        public WindowHelper()
        {

        }

        private enum WindowResizeOptions 
        { 
            Any,
            ResizeAndPositionMain,
            ResizeAndPositionUtility,
            ResizeAndPositionFollowSystem,
            OnlyPositionWizard,
            Message,
            AlwaysCenter
        }
        private enum WindowPositionVariants
        {
            CenterIn,
            AlwaysCenterIn,
            FollowSystem,
        }


        private static Dictionary<string, WindowResizeOptions> WindowsResizeModes = new()
        {
            { nameof(PrepareWindow), WindowResizeOptions.AlwaysCenter },
            { nameof(ViewWindow), WindowResizeOptions.ResizeAndPositionUtility },
            { nameof(ViewGoodCheckOutputWindow), WindowResizeOptions.ResizeAndPositionUtility },
            { nameof(StoreWindow), WindowResizeOptions.ResizeAndPositionFollowSystem },
            { nameof(ProxySetupUtilWindow), WindowResizeOptions.OnlyPositionWizard },
            { nameof(OfflineHelpWindow), WindowResizeOptions.ResizeAndPositionFollowSystem },
            { nameof(MainWindow), WindowResizeOptions.ResizeAndPositionMain },
            { nameof(CreateConfigUtilWindow), WindowResizeOptions.OnlyPositionWizard },
            { nameof(CreateConfigHelperWindow), WindowResizeOptions.ResizeAndPositionFollowSystem },
            { nameof(CriticalErrorHandlerWindow), WindowResizeOptions.OnlyPositionWizard },
            { nameof(StoreSmallDownloadDialog), WindowResizeOptions.Message },
            { nameof(ModernMainWindow), WindowResizeOptions.OnlyPositionWizard },
        };
        // positionType, Width, Height
        private static Dictionary<WindowResizeOptions, Tuple<WindowPositionVariants, int, int>> WindowsResizeParams = new()
        {
            { WindowResizeOptions.Any, Tuple.Create(WindowPositionVariants.FollowSystem, -1, -1) },
            { WindowResizeOptions.ResizeAndPositionMain, Tuple.Create(WindowPositionVariants.CenterIn, 800, 600) },
            { WindowResizeOptions.ResizeAndPositionUtility, Tuple.Create(WindowPositionVariants.FollowSystem, 800, 600) },
            { WindowResizeOptions.ResizeAndPositionFollowSystem, Tuple.Create(WindowPositionVariants.FollowSystem, -1, -1) },
            { WindowResizeOptions.OnlyPositionWizard, Tuple.Create(WindowPositionVariants.CenterIn, 900, 550) },
            { WindowResizeOptions.AlwaysCenter, Tuple.Create(WindowPositionVariants.AlwaysCenterIn, 400, 150) },
            { WindowResizeOptions.Message, Tuple.Create(WindowPositionVariants.AlwaysCenterIn, 700, 400) },
        };

        public static void SetWindowSize(Window window, int width, int height)
        {
            WindowResizeOptions resizeOptions = GetWindowResizeOptionsFromWindow(window);
            var hwnd = WindowNative.GetWindowHandle(window);

            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            WindowsResizeParams.TryGetValue(resizeOptions, out Tuple<WindowPositionVariants, int, int> value);

            if ((width == 0 && height == 0) || width < value.Item2 || height < value.Item3)
            {
                if (value == null) return;

                width = value.Item2;
                height = value.Item3;
            }
            if (width != -1 || height != -1)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32((int)Math.Round(width * GetScaleFactor(window)), (int)Math.Round(height * GetScaleFactor(window))));
            }

            
        }

        public static void SetWindowPosition(Window window, int x, int y)
        {
            WindowResizeOptions resizeOptions = GetWindowResizeOptionsFromWindow(window);
            var hwnd = WindowNative.GetWindowHandle(window);

            WindowsResizeParams.TryGetValue(resizeOptions, out Tuple<WindowPositionVariants, int, int> value);

            if (value == null) return;
            WindowPositionVariants positionOptions = value.Item1;

            if (positionOptions == WindowPositionVariants.AlwaysCenterIn)
            {
                Center(window);
                return;
            }
            else if (x == 0 && y == 0)
            {
                if (positionOptions == WindowPositionVariants.CenterIn)
                {
                    Center(window);
                    return;
                }
                else if (positionOptions == WindowPositionVariants.FollowSystem)
                {
                    return;
                }
            }
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOZORDER | SWP_NOSIZE);
        }

        public static void SetCustomWindowSizeAndPositionFromSettings(Window window)
        {
            WindowResizeOptions resizeOptions = GetWindowResizeOptionsFromWindow(window);

            int width = SettingsManager.Instance.GetValue<int>(["WINDOWS", window.GetType().Name], "width");
            int height = SettingsManager.Instance.GetValue<int>(["WINDOWS", window.GetType().Name], "height");

            int x = SettingsManager.Instance.GetValue<int>(["WINDOWS", window.GetType().Name], "x");
            int y = SettingsManager.Instance.GetValue<int>(["WINDOWS", window.GetType().Name], "y");

            bool isMaximized = SettingsManager.Instance.GetValue<bool>(["WINDOWS", window.GetType().Name], "isMaximized");

            RECT desiredRect = new RECT { left = x, top = y, right = x + width, bottom = y + height };
            RECT workArea;
            if (!TryGetMonitorWorkArea(desiredRect, out workArea))
            {
                workArea = new RECT { left = 0, top = 0, right = GetSystemMetrics(SM_CXSCREEN), bottom = GetSystemMetrics(SM_CYSCREEN) };
            }

            int maxWidth = workArea.right - workArea.left;
            int maxHeight = workArea.bottom - workArea.top;
            int adjWidth = Math.Min(width, Math.Max(1, maxWidth));
            int adjHeight = Math.Min(height, Math.Max(1, maxHeight));

            int adjX = x;
            if (adjX < workArea.left) adjX = workArea.left;
            if (adjX + adjWidth > workArea.right) adjX = Math.Max(workArea.left, workArea.right - adjWidth);

            int adjY = y;
            if (adjY < workArea.top) adjY = workArea.top;
            if (adjY + adjHeight > workArea.bottom) adjY = Math.Max(workArea.top, workArea.bottom - adjHeight);

            SetWindowSize(window, adjWidth, adjHeight);
            SetWindowPosition(window, adjX, adjY);

            if (isMaximized) window.Maximize();

        }

        private static bool TryGetMonitorWorkArea(RECT rect, out RECT workArea)
        {
            workArea = new RECT();
            IntPtr hMon = MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);
            if (hMon == IntPtr.Zero) return false;

            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(hMon, ref mi)) return false;

            workArea = mi.rcWork;
            return true;
        }

        public static void SaveWindowSizeAndPostionsettings(Window window)
        {
            var hwnd = WindowNative.GetWindowHandle(window);

            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            var presenter = appWindow.Presenter as OverlappedPresenter;

            int width = appWindow.Size.Width;
            int height = appWindow.Size.Height;

            int x = appWindow.Position.X;
            int y = appWindow.Position.Y;

            bool isMaximized = presenter.State == OverlappedPresenterState.Maximized;
            if (!isMaximized)
            {
                SettingsManager.Instance.SetValue(["WINDOWS", window.GetType().Name], "width", (int)Math.Round(width / GetScaleFactor(window)));
                SettingsManager.Instance.SetValue(["WINDOWS", window.GetType().Name], "height", (int)Math.Round(height / GetScaleFactor(window)));
                SettingsManager.Instance.SetValue(["WINDOWS", window.GetType().Name], "x", x);
                SettingsManager.Instance.SetValue(["WINDOWS", window.GetType().Name], "y", y);
            }
            SettingsManager.Instance.SetValue(["WINDOWS", window.GetType().Name], "isMaximized", isMaximized);
        }

        private static void Center(Window window)
        {
            if (window is WindowEx exWindow)
            {
                exWindow.CenterOnScreen();
                return;
            }
            IntPtr hWnd = WindowNative.GetWindowHandle(window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);

            if (AppWindow.GetFromWindowId(windowId) is AppWindow appWindow &&
                DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest) is DisplayArea displayArea)
            {
                PointInt32 CenteredPosition = appWindow.Position;
                CenteredPosition.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                CenteredPosition.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                appWindow.Move(CenteredPosition);
            }
        }

        private static WindowResizeOptions GetWindowResizeOptionsFromWindow(Window window)
        {
            var pair = WindowsResizeModes.FirstOrDefault(x => x.Key == window.GetType().Name, new KeyValuePair<string, WindowResizeOptions> (key:string.Empty, value:WindowResizeOptions.Any));
            return pair.Value;
        }

        public static float GetScaleFactor(Window window)
        {
            return (float)window.GetDpiForWindow()/96;
        }

        #region Win32 interop
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromRect([In] ref RECT lprc, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
        #endregion

    }
}
