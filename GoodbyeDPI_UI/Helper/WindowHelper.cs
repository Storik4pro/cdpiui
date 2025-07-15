using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;

namespace GoodbyeDPI_UI.Helper
{
    public class WindowHelper
    {
        private static WindowHelper _instance;
        private static readonly object _lock = new object();
        private WindowHelper()
        {

        }

        
        public static WindowHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new WindowHelper();
                    return _instance;
                }
            }
        }

        public void SetWindowSize(Window window, int width, int height)
        {
            var hwnd = WindowNative.GetWindowHandle(window);

            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        }

    }
}
