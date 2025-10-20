using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using WinUI3Localizer;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ProxySetupUtilWindow : WindowEx
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private IntPtr _hwnd;
        private WindowProc _newWndProc;
        private IntPtr _oldWndProc;

        private ILocalizer localizer = Localizer.Get();
        public ProxySetupUtilWindow()
        {
            InitializeComponent();
            InitializeWindow();

            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("ProxyWindowTitle"));

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

            ContentFrame.Navigate(typeof(Views.SetupProxy.MainPage));
            SetTitleBar(WindowMoveAera);
        }


        private void InitializeWindow()
        {
            _hwnd = WindowNative.GetWindowHandle(this);
            _newWndProc = new WindowProc(NewWindowProc);
            _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
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
