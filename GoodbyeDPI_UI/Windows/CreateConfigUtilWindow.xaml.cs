using CDPI_UI.Helper;
using CDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.CreateConfigUtil;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
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
    public sealed partial class CreateConfigUtilWindow : WindowEx
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private IntPtr _hwnd;
        private WindowProc _newWndProc;
        private IntPtr _oldWndProc;

        public static CreateConfigUtilWindow Instance { get; private set; }

        private ILocalizer localizer = Localizer.Get();

        public CreateConfigUtilWindow()
        {
            InitializeComponent();
            InitializeWindow();

            this.Title = localizer.GetLocalizedString("CreateConfigUtilWindowTitle");

            var appWindowPresenter = this.AppWindow.Presenter as OverlappedPresenter;
            appWindowPresenter.IsResizable = false;
            appWindowPresenter.IsMaximizable = false;
            appWindowPresenter.IsMinimizable = false;

            WindowHelper.SetWindowSize(this, 900, 550);

            NativeWindowHelper.ForceDisableMaximize(this);

            Instance = this;

            ((App)Application.Current).OpenWindows.Add(this);

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ((App)Application.Current).CurrentTheme;
            }

            ExtendsContentIntoTitleBar = true;

            ContentFrame.Navigate(typeof(Views.CreateConfigUtil.MainPage));
            SetTitleBar(WindowMoveAera);

            this.Closed += CreateConfigUtilWindow_Closed;
        }



        public void ToggleLoadingState(TaskbarProgressBarState loadingState, int currentLoadingValue = 0, int maxLoadingValue = 100)
        {
            TaskbarManager.Instance.SetProgressState(loadingState);
            if (loadingState != TaskbarProgressBarState.Indeterminate)
                TaskbarManager.Instance.SetProgressValue(currentLoadingValue, maxLoadingValue);
        }

        public void NavigateToPage<T>(object parameter = null)
        {
            this.Activate();
            if (ContentFrame.CurrentSourcePageType == typeof(MainPage))
            {

                ContentFrame.Navigate(typeof(T), parameter);
                ContentFrame.BackStack.Clear();
            }
            
        }

        private readonly SemaphoreSlim _dialogLock = new SemaphoreSlim(1, 1);

        private async void AskForExit() // TODO: close if close button double clicked
        {
            await _dialogLock.WaitAsync();
            try
            {
                ContentDialog dialog = new()
                {
                    Title = localizer.GetLocalizedString("ConfirmationRequired"),
                    Content = localizer.GetLocalizedString("GoodCheckAskStopSelection"),
                    PrimaryButtonText = localizer.GetLocalizedString("Yes"),
                    CloseButtonText = localizer.GetLocalizedString("No"),
                    XamlRoot = this.Content.XamlRoot
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    GoodCheckProcessHelper.Instance.Stop();
                    this.Close();
                }
            }
            catch
            {

            }
            finally
            {
                _dialogLock.Release();
            }
        }

        private void CreateConfigUtilWindow_Closed(object sender, WindowEventArgs args)
        {
            if (GoodCheckProcessHelper.Instance.IsRunned())
            {
                AskForExit();
                args.Handled = true;
                return;
            }
            Instance = null;
        }

        ~CreateConfigUtilWindow()
        {
            if (!GoodCheckProcessHelper.Instance.IsRunned())
            {
                Instance = null;
            }
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

        private void ApplyDarkThemeToSystemMenu()
        {
            SetPreferredAppMode(2);
            FlushMenuThemes();
        }

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SetPreferredAppMode(int preferredAppMode);

        [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern void FlushMenuThemes();

    }
}
