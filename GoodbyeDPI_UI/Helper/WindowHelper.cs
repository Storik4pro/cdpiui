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

namespace CDPI_UI.Helper
{
    public class WindowHelper
    {
        public WindowHelper()
        {

        }

        public static void SetWindowSize(Window window, int width, int height)
        {
            var hwnd = WindowNative.GetWindowHandle(window);

            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        }

    }
}
