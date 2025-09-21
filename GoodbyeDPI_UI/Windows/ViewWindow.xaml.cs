using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI;
using System.Text;
using Microsoft.UI.System;
using Windows.UI;
using CDPI_UI.Helper;
using System.Runtime.InteropServices;
using WinRT.Interop;
using CommunityToolkit.WinUI.Behaviors;
using CommunityToolkit.WinUI.Controls;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.Win32;
using CDPI_UI.DataModel;
using static CommunityToolkit.WinUI.Animations.Expressions.ExpressionValues;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using CDPI_UI.Controls.Dialogs;
using WinUIEx;
using WinUI3Localizer;
using CDPI_UI.Helper.Static;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>


    public sealed partial class ViewWindow : WindowEx
    {
        private readonly StringBuilder _outputBuffer = new StringBuilder();

        private const int WM_GETMINMAXINFO = 0x0024;
        private IntPtr _hwnd;
        private WindowProc _newWndProc;
        private IntPtr _oldWndProc;

        private ILocalizer localizer = Localizer.Get();

        public ViewWindow()
        {
            this.InitializeComponent();
            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("PseudoconsoleWindowTitle"));

            InitializeWindow();
            WindowHelper.SetWindowSize(this, 800, 600);
            this.Closed += ViewWindow_Closed;
            TrySetMicaBackdrop(true);

            ((App)Application.Current).OpenWindows.Add(this);

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ((App)Application.Current).CurrentTheme;
            }

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            if (SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "outputMode"))
            {
                AppendToRichTextBlock(ProcessManager.Instance.GetProcessOutput());
                CleanOutputButton.IsChecked = true;
            }
            else
            {
                AppendToRichTextBlock(ProcessManager.Instance.GetDefaultProcessOutput());
                DefaultOutputButton.IsChecked = true;
            }
            ProcessManager.Instance.OutputReceived += OnProcessOutputReceived;

            ProcessManager.Instance.onProcessStateChanged += ChangeProcessStatus;

            ProcessManager.Instance.ErrorHappens += ErrorHappens;

            ProcessManager.Instance.ProcessNameChanged += ProcessManager_ProcessNameChanged;

            TrySetCurentProcess();

            if (ProcessManager.Instance.isErrorHappens)
            {
                if (ProcessManager.Instance.LatestErrorMessage.Count >= 2)
                {
                    ErrorHappens(ProcessManager.Instance.LatestErrorMessage[0], ProcessManager.Instance.LatestErrorMessage[1]);
                }
                else
                {
                    ErrorHappens("UNKNOWN_ERROR", "console");
                }
            } else
            {
                ChangeIcon(ProcessManager.Instance.processState);
            }
            OutputRichTextBlock.FontFamily = new FontFamily(SettingsManager.Instance.GetValue<string>("PSEUDOCONSOLE", "fontFamily"));
            OutputRichTextBlock.FontSize = SettingsManager.Instance.GetValue<double>("PSEUDOCONSOLE", "fontSize");
        }

        private void TrySetCurentProcess()
        {
            try
            {
                var item = DatabaseHelper.Instance.GetItemById(SettingsManager.Instance.GetValue<string>("COMPONENTS", "nowUsed"));
                if (item != null)
                {
                    SelectedComponentTextBlock.Text = string.Format(localizer.GetLocalizedString("NowSelectedComponent"), item.ShortName);
                }
                else
                {
                    SelectedComponentTextBlock.Text = localizer.GetLocalizedString("NoComponent");
                }
            }
            catch 
            {
                SelectedComponentTextBlock.Text = localizer.GetLocalizedString("NoComponent");
            }
        }

        private void ProcessManager_ProcessNameChanged(string obj)
        {
            TrySetCurentProcess();
        }

        public bool IsActive()
        {
            return this.DispatcherQueue != null;
        }

        private void OnProcessOutputReceived(string output)
        {
            AppendToRichTextBlock(output);
        }
        private void AppendToRichTextBlock(string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _outputBuffer.Append(text);

                Run run = new Run
                {
                    Text = text,
                    Foreground = new SolidColorBrush(Colors.LightGray) 
                };

                OutputParagraph.Inlines.Add(run);

                
            });
        }


        private void ClearRichTextBlock()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _outputBuffer.Clear();

                OutputParagraph.Inlines.Clear();
            });
        }

        private void ChangeProcessStatus(string state)
        {
            if (state == "started")
            {
                ClearRichTextBlock();
                ChangeIcon(true);

            } else if (state == "stopped")
            {
                ChangeIcon(false);
            }
        }

        private void ChangeIcon(bool type)
        {
            if (type)
            {
                StatusIcon.Glyph = "\uEC61";
                ProcessControlIcon.Glyph = "\uE71A";
                ProcessControl.Text = localizer.GetLocalizedString("Stop");
                ProcessRestart.IsEnabled = true;
                var successColor = (Color)Application.Current.Resources["SystemFillColorSuccess"];
                var successBrush = new SolidColorBrush(successColor);
                StatusIcon.Foreground = successBrush;
                StatusText.Text = localizer.GetLocalizedString("ProcessStarted");
                StatusMessage.Title = localizer.GetLocalizedString("ProcessStartedMessageTitle");
                StatusMessage.Message = string.Format(localizer.GetLocalizedString("ProcessStartedMessageMessage"), ProcessManager.Instance.ProcessName);
                StatusMessage.Severity = InfoBarSeverity.Success;
            }
            else
            {
                StatusIcon.Glyph = "\uEB90";
                ProcessControlIcon.Glyph = "\uE768";
                ProcessControl.Text = localizer.GetLocalizedString("Start");
                ProcessRestart.IsEnabled = false;
                var criticalColor = (Color)Application.Current.Resources["SystemFillColorCritical"];
                var criticalBrush = new SolidColorBrush(criticalColor);
                StatusIcon.Foreground = criticalBrush;
                StatusText.Text = localizer.GetLocalizedString("ProcessStopped");
                StatusMessage.Title = localizer.GetLocalizedString("ProcessStoppedMessageTitle");
                StatusMessage.Message = string.Format(localizer.GetLocalizedString("ProcessStoppedMessageMessage"), ProcessManager.Instance.ProcessName);
                StatusMessage.Severity = InfoBarSeverity.Informational;
            }
            StatusHeader.Visibility = Visibility.Visible;
            StatusMessage.IsOpen = true;
            
        }

        private void ErrorHappens(string error, string _object = "process")
        {
            ChangeIcon(false);
            StatusMessage.Severity = InfoBarSeverity.Error;
            StatusHeader.Visibility = Visibility.Visible;
            string message = _object == "process" ? 
                string.Format(localizer.GetLocalizedString("ProceesRaisedException"), ProcessManager.Instance.ProcessName) : localizer.GetLocalizedString("PseudoconsoleInternalError");
            StatusMessage.Message = $"{message}: {error}";
        }


        private void ViewWindow_Closed(object sender, WindowEventArgs args)
        {
            ProcessManager.Instance.OutputReceived -= OnProcessOutputReceived;
            ProcessManager.Instance.onProcessStateChanged -= ChangeProcessStatus;
            ((App)Application.Current).OpenWindows.Remove(this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void StatusMessage_CloseButtonClick(InfoBar sender, object args)
        {
            StatusHeader.Visibility = Visibility.Collapsed;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var _dialog = new Microsoft.Win32.SaveFileDialog
            {
                OverwritePrompt = true,
                FileName = "PseudoConsoleLog.txt",
                DefaultExt = ".txt",
                Filter = "TXT Files|*.txt"
            };
            var result = _dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                string filename = _dialog.FileName;
                
                string text = DefaultOutputButton.IsChecked ? ProcessManager.Instance.GetDefaultProcessOutput() :
                    ProcessManager.Instance.GetProcessOutput();
                try
                {
                    File.WriteAllText(filename, text);
                    
                } catch (Exception ex)
                {
                    ErrorContentDialog dialog = new ErrorContentDialog { };
                    await dialog.ShowErrorDialogAsync(content: string.Format(localizer.GetLocalizedString("FileSaveErrorMessage"), _dialog.FileName, "ERR_FILE_WRITE"),
                        errorDetails: $"{ex}",
                        xamlRoot: this.Content.XamlRoot);
                }
                
            }
        }

        private async void ProcessControl_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessManager.Instance.processState)
            {
                await ProcessManager.Instance.StopProcess();
            } else
            {
                await ProcessManager.Instance.StartProcess();
            }
        }

        private async void ProcessRestart_Click(object sender, RoutedEventArgs e)
        {
            await ProcessManager.Instance.RestartProcess();
        }

        private void CleanOutputButton_Click(object sender, RoutedEventArgs e)
        {
            ClearRichTextBlock();
            AppendToRichTextBlock(ProcessManager.Instance.GetProcessOutput());
            SettingsManager.Instance.SetValue("PSEUDOCONSOLE", "outputMode", true);
        }

        private void DefaultOutputButton_Click(object sender, RoutedEventArgs e)
        {
            ClearRichTextBlock();
            AppendToRichTextBlock(ProcessManager.Instance.GetDefaultProcessOutput());
            SettingsManager.Instance.SetValue("PSEUDOCONSOLE", "outputMode", false);
        }
        private async void ShowFontSettingsDialog()
        {
            FontSettingsContentDialog dialog = new()
            {
                XamlRoot = this.Content.XamlRoot,
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string selectedFontFamily = dialog.FontName as string;
                ApplyFontSettings(selectedFontFamily, dialog.FontSize);
            }
        }

        private void ApplyFontSettings(string fontFamily, double fontSize)
        {
            OutputRichTextBlock.FontFamily = new FontFamily(fontFamily);
            OutputRichTextBlock.FontSize = fontSize;
            SettingsManager.Instance.SetValue("PSEUDOCONSOLE", "fontFamily", fontFamily);
            SettingsManager.Instance.SetValue("PSEUDOCONSOLE", "fontSize", fontSize);
        }


        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            ShowFontSettingsDialog();
        }

        private async void SupportButton_Click(object sender, RoutedEventArgs e)
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Storik4pro/goodbyeDPI-UI/issues/"));
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            OutputRichTextBlock.SelectAll();
            OutputRichTextBlock.CopySelectionToClipboard();
            CopyIcon.Glyph = "\uE73E";
            await Task.Delay(1000);
            CopyIcon.Glyph = "\uE8C8";

        }

        private async void StopServiceButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = localizer.GetLocalizedString("ConfirmationRequired"),

                PrimaryButtonText = localizer.GetLocalizedString("YesStopService"),
                CloseButtonText = localizer.GetLocalizedString("Cancel"),
                DefaultButton = ContentDialogButton.Close,
                Content = localizer.GetLocalizedString("ServiceAskToStopMessage")
            };
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await ProcessManager.Instance.StopService();
                } catch (Exception ex)
                {
                    ErrorContentDialog _dialog = new ErrorContentDialog { };
                    await _dialog.ShowErrorDialogAsync(content: string.Format(localizer.GetLocalizedString("ServiceStopException"), "WINDIVERT_STOP_ERROR"),
                        errorDetails: $"{ex.Message}",
                        xamlRoot: this.Content.XamlRoot);
                }
            }
        }

        #region WINAPI
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
        bool TrySetMicaBackdrop(bool useMicaAlt)
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                micaBackdrop.Kind = useMicaAlt ? Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt : Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base;
                this.SystemBackdrop = micaBackdrop;

                return true;
            }

            return false;
        }
        #endregion
    }
}
