using CDPI_UI.Default;
using CDPI_UI.Helper;
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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    public sealed partial class PrepareWindow : TemplateWindow
    {
        public PrepareWindow()
        {
            InitializeComponent();

            WindowMinSize = new System.Windows.Size(0, 0);
            IsAppShownInSwitchers = false;
            this.OverlappedPresenter.SetBorderAndTitleBar(true, false);

            DisableResizeFeature();

            SetTitleBar(WindowMoveAera);

            this.Closed += CriticalErrorHandlerWindow_Closed;

            PipeClient.Instance.Connected += PipeConnected;

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
            if (!PipeClient.Instance.IsConnected)
                args.Handled = true;
        }
    }
}
