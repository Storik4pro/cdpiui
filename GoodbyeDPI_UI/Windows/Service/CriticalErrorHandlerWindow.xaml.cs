using CDPI_UI.Default;
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
    public sealed partial class CriticalErrorHandlerWindow : TemplateWindow
    {
        private const string WhereTemplate = "UNKNOWN";
        private const string InfoNotProvided = "This information is not provided.";
        private const string FlashlightError = "ERR_FLASHLIGHT_BUSY";


        public CriticalErrorHandlerWindow(
            string where = WhereTemplate,
            string why = InfoNotProvided,
            string errorCode = FlashlightError
            )
        {
            InitializeComponent();
            ((App)Application.Current).ShowWindowModalAsync(this);

            IconUri = @"Assets/Icons/Error.ico";
            TitleIcon = TitleImageRectagle;
            TitleBar = WindowMoveAera;

            DisableResizeFeature();

            SetTitleBar(WindowMoveAera);

            this.Closed += CriticalErrorHandlerWindow_Closed;

            WhereTextBlock.Text = where;
            WhyTextBlock.Text = why;
            ErrorCodeTextBlock.Text = errorCode;

            string additionalInfo =
                $"Application: CDPI UI\n" +
                $"Version: {StateHelper.Instance.Version}\n" +
                $"System: {Environment.OSVersion.ToString()}\n" +
                $"Architecture: {RuntimeInformation.OSArchitecture.ToString()}";
            AdditionalTextBlock.Text = additionalInfo;

            WindowHelper.SetCustomWindowSizeAndPositionFromSettings(this);
        }

        private void CriticalErrorHandlerWindow_Closed(object sender, WindowEventArgs args)
        {
            this.Closed -= CriticalErrorHandlerWindow_Closed;
            Process.GetCurrentProcess().Kill();
        }

        ~CriticalErrorHandlerWindow()
        {
            Process.GetCurrentProcess().Kill();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchReportUrl();
        }

        private CancellationTokenSource _copyTimerCts;

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string readyToCopyText =
                $"Critical Exception Info\n" +
                $"```\n" +
                $"Where: {WhereTextBlock.Text}\n" +
                $"Why: {WhyTextBlock.Text}\n" +
                $"ErrCode: {ErrorCodeTextBlock.Text}\n" +
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
            Utils.OpenFolderInExplorer(Path.Combine(StateHelper.GetDataDirectory(), "Logs"));
        }
    }
}
