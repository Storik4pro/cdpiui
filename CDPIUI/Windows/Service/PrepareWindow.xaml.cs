using CDPIUI.Default;
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using WinUIEx;
using CDPIUI.Core.Communication;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI
{
    public sealed partial class PrepareWindow : TemplateWindow
    {
        public PrepareWindow()
        {
            InitializeComponent();

            WindowMinSize = new System.Windows.Size(0, 0);
            IsAppShownInSwitchers = false;
            IconUri = @"Assets/favicon.ico";
            this.CustomTitleBarUserControl = TitleBarUserControl;

            this.OverlappedPresenter.SetBorderAndTitleBar(true, false);

            DisableResizeFeature();

            this.Closed += CriticalErrorHandlerWindow_Closed;

            PipeClientService.Instance.Connected += PipeConnected;

            string[] arguments = Environment.GetCommandLineArgs();

            if (!arguments.Contains("--create-no-window"))
                this.Hide();
        }
        private void PipeConnected()
        {
            this.Hide();
        }
        private void CriticalErrorHandlerWindow_Closed(object sender, WindowEventArgs args)
        {
            if (!PipeClientService.Instance.IsConnected)
                args.Handled = true;
        }
    }
}
