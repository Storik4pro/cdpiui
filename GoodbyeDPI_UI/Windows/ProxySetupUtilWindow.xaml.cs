using CDPI_UI.Default;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using Microsoft.UI;
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
    public sealed partial class ProxySetupUtilWindow : TemplateWindow
    {
        private ILocalizer localizer = Localizer.Get();
        public ProxySetupUtilWindow()
        {
            InitializeComponent();

            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("ProxyWindowTitle"));
            IconUri = @"Assets/Icons/Proxy.ico";
            TitleIcon = TitleImageRectagle;
            TitleBar = WindowMoveAera;
            DisableResizeFeature();

            NativeWindowHelper.ForceDisableMaximize(this);

            ContentFrame.Navigate(typeof(Views.SetupProxy.MainPage));
            SetTitleBar(WindowMoveAera);
        }
    }
}
