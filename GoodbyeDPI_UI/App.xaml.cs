using GoodbyeDPI_UI.Common;
//using Windows.ApplicationModel.Activation;
using GoodbyeDPI_UI.DesktopWap.DataModel;
using GoodbyeDPI_UI.DesktopWap.Helper;
//using GoodbyeDPI_UI.Data;
using GoodbyeDPI_UI.Helper;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI;
using WinRT.Interop;
using WinUIEx;
using static GoodbyeDPI_UI.Win32;
using WASDK = Microsoft.WindowsAppSDK;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>

    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>

        public ElementTheme CurrentTheme { get; set; } = ElementTheme.Default;

        public List<Window> OpenWindows { get; private set; } = new List<Window>();

        private Dictionary<IntPtr, bool> _disabledWindows = new Dictionary<IntPtr, bool>();
        private object _modalLock = new object();

        public App()
        {
            this.InitializeComponent();

            UpdateThemeForAllWindows(GetThemeFromString(SettingsManager.Instance.GetValue<string>("APPEARANCE", "Theme")));

            PipeClient.Instance.Init();
            PipeClient.Instance.Connected += PipeConnected;

            GetReadyFeatures();
        }

        private void PipeConnected()
        {
            _ = SafeCreateNewWindow<MainWindow>();
            PipeClient.Instance.Connected -= PipeConnected;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _ = SafeCreateNewWindow<PrepareWindow>();

            PipeClient.Instance.Start();
        }
        
        public async void GetReadyFeatures()
        {
            // TODO: Make async and show progress
            DatabaseHelper.Instance.QuickRestore();

            await Task.CompletedTask;
        }

        public bool CheckWindow<TWindow>() where TWindow : Window
        {
            return (OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null) != null);
        }

        public void ShowWindow<TWindow>() where TWindow : Window
        {
            var activeFindWindow = OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);
            if (activeFindWindow != null)
            {
                activeFindWindow.Activate();
            }
        }

        public async Task<TWindow> SafeCreateNewWindow<TWindow>() where TWindow : Window, new()
        {
            var findWindows = OpenWindows.OfType<TWindow>().ToList();
            int findWindowCount = findWindows.Count;

            var activeFindWindow = OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);

            if (activeFindWindow != null && findWindowCount == 1)
            {
                activeFindWindow.Activate();
                await Task.CompletedTask;
                return activeFindWindow;
            }
            else
            {
                foreach (var viewWindow in OpenWindows.OfType<TWindow>().ToList())
                {
                    viewWindow.Close();
                    OpenWindows.Remove(viewWindow);
                }

                var newViewWindow = new TWindow();
                newViewWindow.Activate();

                RegisterWindow(newViewWindow);
                await Task.CompletedTask;
                return newViewWindow;
            }
        }

        private void RegisterWindow(Window window)
        {
            if (window == null) return;

            if (!OpenWindows.Contains(window))
                OpenWindows.Add(window);

            window.Closed -= Window_ClosedHandler;
            window.Closed += Window_ClosedHandler;
        }

        private void Window_ClosedHandler(object sender, WindowEventArgs e)
        {
            if (sender is not Window window) return;
            
            try
            {
                window.Closed -= Window_ClosedHandler;

                try
                {
                    if (window.Content is FrameworkElement fe)
                    {
                        fe.DataContext = null;

                        TryDisposeFrameworkElement(fe);
                    }
                }
                catch { }

                window.Content = null;
            }
            catch { }
            finally
            {
                try { OpenWindows.Remove(window); } catch { }
                GC.SuppressFinalize(window);
                GC.Collect();
            }
        }

        private void TryDisposeFrameworkElement(FrameworkElement fe)
        {
            if (fe == null) return;

            if (fe is IDisposable d)
            {
                try { d.Dispose(); } catch { }
            }

            try
            {
                var feType = fe.GetType();

                var webviewProp = feType.GetProperty("CoreWebView2");
                if (webviewProp != null)
                {
                    var core = webviewProp.GetValue(fe);
                    if (core != null)
                    {
                        var closeMethod = core.GetType().GetMethod("Close") ?? core.GetType().GetMethod("Dispose");
                        if (closeMethod != null)
                        {
                            try { closeMethod.Invoke(core, null); } catch { }
                        }
                    }

                    if (fe is IDisposable wc)
                    {
                        try { wc.Dispose(); } catch { }
                    }
                }
            }
            catch { }

            try
            {
                if (fe is ContentControl cc && cc.Content is IDisposable ccd)
                {
                    try { ccd.Dispose(); } catch { }
                    cc.Content = null;
                }

                if (fe is Panel panel) 
                {
                    foreach (var child in panel.Children)
                    {
                        if (child is IDisposable childD)
                        {
                            try { childD.Dispose(); } catch { }
                        }
                    }
                    panel.Children.Clear();
                }
            }
            catch { }
        }

        public Window GetCurrentWindowFromType<TWindow>() where TWindow:Window
        {
            return OpenWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);
        }

        private Window m_window;
        public static TEnum GetEnum<TEnum>(string text) where TEnum : struct
        {
            if (!typeof(TEnum).GetTypeInfo().IsEnum)
            {
                throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
            }
            return (TEnum)Enum.Parse(typeof(TEnum), text);
        }

        public Task ShowWindowModalAsync(Window modalWindow)
        {
            if (modalWindow == null) throw new ArgumentNullException(nameof(modalWindow));

            var tcs = new TaskCompletionSource<bool>();

            var dq = modalWindow.DispatcherQueue;
            if (dq == null)
            {
                _MakeModalAndAwait(modalWindow, tcs);
            }
            else
            {
                dq.TryEnqueue(() => _MakeModalAndAwait(modalWindow, tcs));
            }

            return tcs.Task;
        }

        private void _MakeModalAndAwait(Window modalWindow, TaskCompletionSource<bool> tcs)
        {
            lock (_modalLock)
            {
                try
                {
                    IntPtr modalHwnd = WindowNative.GetWindowHandle(modalWindow);
                    if (modalHwnd == IntPtr.Zero)
                    {
                        tcs.SetException(new InvalidOperationException("HWND err"));
                        return;
                    }

                    _disabledWindows.Clear();
                    foreach (var win in OpenWindows)
                    {
                        if (win == null) continue;
                        try
                        {
                            IntPtr h = WindowNative.GetWindowHandle(win);
                            if (h == IntPtr.Zero) continue;

                            if (win == modalWindow)
                            {
                                continue;
                            }

                            _disabledWindows[h] = true;

                            EnableWindow(h, false);
                        }
                        catch
                        {
                        }
                    }

                    SetWindowPos(modalHwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    SetForegroundWindow(modalHwnd);

                    void ClosedHandler(object s, WindowEventArgs e)
                    {
                        try
                        {
                            var dq2 = modalWindow.DispatcherQueue;
                            if (dq2 != null)
                            {
                                dq2.TryEnqueue(() => _RestoreAfterModal(modalWindow, modalHwnd));
                            }
                            else
                            {
                                _RestoreAfterModal(modalWindow, modalHwnd);
                            }
                        }
                        finally
                        {
                            modalWindow.Closed -= ClosedHandler;
                            tcs.TrySetResult(true);
                        }
                    }

                    modalWindow.Closed += ClosedHandler;
                }
                catch (Exception ex)
                {
                    try { _RestoreAfterModal(modalWindow, WindowNative.GetWindowHandle(modalWindow)); } catch { }
                    tcs.TrySetException(ex);
                }
            }
        }

        private void _RestoreAfterModal(Window modalWindow, IntPtr modalHwnd)
        {
            lock (_modalLock)
            {
                try
                {
                    if (modalHwnd != IntPtr.Zero)
                    {
                        SetWindowPos(modalHwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    }
                }
                catch { }

                foreach (var kv in _disabledWindows.ToList())
                {
                    try
                    {
                        EnableWindow(kv.Key, true);
                    }
                    catch { }
                }

                _disabledWindows.Clear();
            }
        }

        public FrameworkElement GetRootFrame()
        {
            foreach (var window in OpenWindows)
            {
                try
                {
                    if (window.Content is FrameworkElement rootElement)
                    {
                        return rootElement;
                    }
                } catch
                {

                }
            }
            throw new Exception($"Unable to get root frame");
        }

        private ElementTheme GetThemeFromString(string theme)
        {
            if (theme == "Dark")
            {
                return ElementTheme.Dark;
            } else if (theme == "Light")
            {
                return ElementTheme.Light;
            } else
            {
                return ElementTheme.Default;
            }
        }

        public ElementTheme GetCurrentTheme()
        {
            return CurrentTheme;
        }

        public void UpdateThemeForAllWindows(ElementTheme theme)
        {
            CurrentTheme = theme;
            

            foreach (var window in OpenWindows)
            {
                try
                {
                    if (window.Content is FrameworkElement rootElement)
                    {
                        rootElement.RequestedTheme = theme;
                        if (theme == ElementTheme.Dark)
                        {
                            TitleBarHelper.SetCaptionButtonColors(window, Colors.White);
                        } else if (theme == ElementTheme.Light)
                        {
                            TitleBarHelper.SetCaptionButtonColors(window, Colors.Black);
                        } else
                        {
                            TitleBarHelper.ApplySystemThemeToCaptionButtons(window);
                        }
                    }
                }
                catch
                {
                    Debug.WriteLine("Something went wrong");
                }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;


    }
}
