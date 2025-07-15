using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using GoodbyeDPI_UI.Common;
//using GoodbyeDPI_UI.Data;
using GoodbyeDPI_UI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
//using Windows.ApplicationModel.Activation;
using GoodbyeDPI_UI.DesktopWap.DataModel;
using WASDK = Microsoft.WindowsAppSDK;
using System.Text;
using Windows.System;
using System.Runtime.InteropServices;
using static GoodbyeDPI_UI.Win32;
using System.Collections.Generic;
using GoodbyeDPI_UI.DesktopWap.Helper;
using System.Drawing;
using Windows.UI;
using Microsoft.UI;
using System.Threading.Tasks;

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

        public App()
        {
            this.InitializeComponent();

            UpdateThemeForAllWindows(GetThemeFromString(SettingsManager.Instance.GetValue<string>("APPEARANCE", "Theme")));

        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var mainWindow = new MainWindow();
            OpenWindows.Add(mainWindow);
            mainWindow.Activate();
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

        public async Task SafeCreateNewWindow<TWindow>() where TWindow : Window, new()
        {
            var openWindows = OpenWindows;

            var findWindows = openWindows.OfType<TWindow>().ToList();
            int findWindowCount = findWindows.Count;

            var activeFindWindow = openWindows.OfType<TWindow>().FirstOrDefault(w => w.DispatcherQueue != null);

            if (activeFindWindow != null && findWindowCount == 1)
            {
                activeFindWindow.Activate();
            }
            else
            {
                foreach (var viewWindow in openWindows.OfType<ViewWindow>().ToList())
                {
                    viewWindow.Close();
                    OpenWindows.Remove(viewWindow);
                }

                var newViewWindow = new TWindow();
                newViewWindow.Activate();
                await Task.Delay(500);
            }
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


    }
}
