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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CriticalErrorHandlerWindow : Window
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private IntPtr _hwnd;
        private WindowProc _newWndProc;
        private IntPtr _oldWndProc;

        private const string WhereTemplate = "UNKNOWN";
        private const string InfoNotProvided = "This information is not provided.";
        private const string FlashlightError = "ERR_FLASHLIGHT_BUSY";



        public static CriticalErrorHandlerWindow Instance { get; private set; }

        public CriticalErrorHandlerWindow(
            string where = WhereTemplate,
            string why = InfoNotProvided,
            string errorCode = FlashlightError
            )
        {
            InitializeComponent();
            InitializeWindow();
            ((App)Application.Current).ShowWindowModalAsync(this);

            var appWindowPresenter = this.AppWindow.Presenter as OverlappedPresenter;
            appWindowPresenter.IsResizable = false;
            appWindowPresenter.IsMaximizable = false;
            appWindowPresenter.IsMinimizable = false;

            WindowHelper.SetWindowSize(this, 900, 550);

            Instance = this;

            ((App)Application.Current).OpenWindows.Add(this);

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ((App)Application.Current).CurrentTheme;
            }

            ExtendsContentIntoTitleBar = true;

            SetTitleBar(WindowMoveAera);

            this.Closed += CriticalErrorHandlerWindow_Closed;

            Helper.Static.NativeWindowHelper.ForceDisableMaximize(this);

            WhereTextBlock.Text = where;
            WhyTextBlock.Text = why;
            ErrorCodeTextBlock.Text = errorCode;

            string additionalInfo =
                $"Application: CDPI UI\n" +
                $"Version: {StateHelper.Instance.Version}\n" +
                $"System: {Environment.OSVersion.ToString()}\n" +
                $"Architecture: {RuntimeInformation.OSArchitecture.ToString()}";
            AdditionalTextBlock.Text = additionalInfo;
        }

        private void CriticalErrorHandlerWindow_Closed(object sender, WindowEventArgs args)
        {
            ((App)Application.Current).OpenWindows.Remove(this);
            this.Closed -= CriticalErrorHandlerWindow_Closed;
            Process.GetCurrentProcess().Kill();
        }

        ~CriticalErrorHandlerWindow()
        {
            ((App)Application.Current).OpenWindows.Remove(this);
            Process.GetCurrentProcess().Kill();
        }

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

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }

        private async void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Storik4pro/cdpiui/issues"));
        }

        private CancellationTokenSource _copyTimerCts;

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string readyToCopyText =
                $"Critical Exception Info\n" +
                $"```\n" +
                $"Where: {WhereTextBlock.Text}\n" +
                $"Why: {WhyTextBlock.Text}\n" +
                $"ErrCode: {ErrorCodeTextBlock}\n" +
                $"EnvInfo:\n{AdditionalTextBlock.Text}\n" +
                $"```\n" +
                $"Endlog";
            System.Windows.Clipboard.SetText(readyToCopyText);

            _copyTimerCts?.Cancel();
            _copyTimerCts?.Dispose();
            _copyTimerCts = new CancellationTokenSource();
            var token = _copyTimerCts.Token;

            CopyText.Visibility = Visibility.Collapsed;
            CopiedText.Visibility = Visibility.Visible;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);

                CopiedText.Visibility = Visibility.Collapsed;
                CopyText.Visibility = Visibility.Visible;

                _copyTimerCts.Dispose();
                _copyTimerCts = null;
            }
            catch (TaskCanceledException)
            {

            }
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
