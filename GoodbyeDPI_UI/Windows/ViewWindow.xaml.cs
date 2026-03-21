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
using WindowId = Microsoft.UI.WindowId;
using Microsoft.UI.Windowing;
using CDPI_UI.Default;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>


    public sealed partial class ViewWindow : TemplateWindow
    {
        private readonly StringBuilder _outputBuffer = new StringBuilder();

        private ILocalizer localizer = Localizer.Get();

        public ViewWindow()
        {
            this.NewIdSet += SetId;
            this.InitializeComponent();

            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("PseudoconsoleWindowTitle"));
            IconUri = @"Assets/Icons/Pseudoconsole.ico";
            TitleIcon = TitleImageRectagle;
            TitleBar = AppTitleBarContent;
            this.Closed += ViewWindow_Closed;

            WindowHelper.TrySetMicaBackdrop(true, this, MainGrid);

            SetTitleBar(AppTitleBar);

            if (SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "outputMode"))
            {
                CleanOutputButton.IsChecked = true;
            }
            else
            {
                DefaultOutputButton.IsChecked = true;
            }
            
            OutputRichTextBlock.FontFamily = new FontFamily(SettingsManager.Instance.GetValue<string>("PSEUDOCONSOLE", "fontFamily"));
            OutputRichTextBlock.FontSize = SettingsManager.Instance.GetValue<double>("PSEUDOCONSOLE", "fontSize");

            SetId();
        }

        private void SetId()
        {
            DisconnectHandlers();
            TrySetCurentProcess();
            ConnectHandlers();
        }

        private async Task<ProcessManager> GetProcessManager()
        {
            ProcessManager processManager = (await TasksHelper.Instance.GetTaskFromId(Id))?.ProcessManager;
            if (processManager == null) return null;
            return processManager;
        }

        private async void ConnectHandlers()
        {
            var processManager = await GetProcessManager();
            if (processManager == null) return;
            processManager.OutputReceived += OnProcessOutputReceived;
            processManager.onProcessStateChanged += ChangeProcessStatus;
            processManager.ErrorHappens += ErrorHappens;
            processManager.ProcessNameChanged += ProcessManager_ProcessNameChanged;

            if (processManager.isErrorHappens)
            {
                if (processManager.LatestErrorMessage.Count >= 2)
                {
                    ErrorHappens(processManager.LatestErrorMessage[0], processManager.LatestErrorMessage[1]);
                }
                else
                {
                    ErrorHappens("UNKNOWN_ERROR", "console");
                }
                await processManager.GetReady(false);
            }
            else
            {
                ChangeIcon(processManager.processState);
                await processManager.GetReady(true);
            }
        }

        private async void DisconnectHandlers()
        {
            var processManager = await GetProcessManager();
            if (processManager == null) return;
            processManager.OutputReceived -= OnProcessOutputReceived;
            processManager.onProcessStateChanged -= ChangeProcessStatus;
            processManager.ErrorHappens -= ErrorHappens;
            processManager.ProcessNameChanged -= ProcessManager_ProcessNameChanged;
        }

        private async void TrySetCurentProcess(string procName = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(procName))
                {
                    var processManager = await GetProcessManager();
                    StatusMessage.Message = string.Format(
                        processManager.processState ? localizer.GetLocalizedString("ProcessStartedMessageMessage") : localizer.GetLocalizedString("ProcessStoppedMessageMessage"), 
                        GetProcessName()
                        );
                }

                var item = DatabaseHelper.Instance.GetItemById(Id);
                if (item != null)
                {
                    SelectedComponentTextBlock.Text = string.Format(localizer.GetLocalizedString("NowViewOutputFromComponent"), item.ShortName);
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
            TrySetCurentProcess(obj);
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

        private async void ChangeProcessStatus(Tuple<string, bool> tuple)
        {
            if (tuple.Item2)
            {
                ClearRichTextBlock();
                if (SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "outputMode"))
                {
                    AppendToRichTextBlock((await GetProcessManager()).GetProcessOutput());
                }
                else
                {
                    AppendToRichTextBlock((await GetProcessManager()).GetDefaultProcessOutput());
                }
                ChangeIcon(true);

            } else if (!tuple.Item2)
            {
                ChangeIcon(false);
            }
        }

        private string GetProcessName()
        {
            if (string.IsNullOrEmpty(GetProcessManager().Result.ProcessName))
            {
                var item = DatabaseHelper.Instance.GetItemById(Id);
                if (item != null)
                {
                    return item.Executable + ".exe";
                }
                return string.Empty;
            }
            return GetProcessManager().Result.ProcessName;
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
                if (string.IsNullOrEmpty(GetProcessName())) StatusMessage.Message = string.Empty;
                else StatusMessage.Message = string.Format(localizer.GetLocalizedString("ProcessStartedMessageMessage"), GetProcessName());
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
                if (string.IsNullOrEmpty(GetProcessName())) StatusMessage.Message = string.Empty;
                else StatusMessage.Message = string.Format(localizer.GetLocalizedString("ProcessStoppedMessageMessage"), GetProcessName());
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
                string.Format(localizer.GetLocalizedString("ProceesRaisedException"), GetProcessManager().Result.ProcessName) : localizer.GetLocalizedString("PseudoconsoleInternalError");
            StatusMessage.Message = $"{message}: {error}";
        }


        private void ViewWindow_Closed(object sender, WindowEventArgs args)
        {
            DisconnectHandlers();
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
                
                string text = DefaultOutputButton.IsChecked ? GetProcessManager().Result.GetDefaultProcessOutput() :
                    GetProcessManager().Result.GetProcessOutput();
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
            if (GetProcessManager().Result.processState)
            {
                await GetProcessManager().Result.StopProcess();
            } else
            {
                await GetProcessManager().Result.StartProcess();
            }
        }

        private async void ProcessRestart_Click(object sender, RoutedEventArgs e)
        {
            await GetProcessManager().Result.RestartProcess();
        }

        private void CleanOutputButton_Click(object sender, RoutedEventArgs e)
        {
            ClearRichTextBlock();
            AppendToRichTextBlock(GetProcessManager().Result.GetProcessOutput());
            SettingsManager.Instance.SetValue("PSEUDOCONSOLE", "outputMode", true);
        }

        private void DefaultOutputButton_Click(object sender, RoutedEventArgs e)
        {
            ClearRichTextBlock();
            AppendToRichTextBlock(GetProcessManager().Result.GetDefaultProcessOutput());
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

        private void SupportButton_Click(object sender, RoutedEventArgs e)
        {
            UrlOpenHelper.LaunchReportUrl();
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
                    await ProcessManager.StopService();
                } catch (Exception ex)
                {
                    ErrorContentDialog _dialog = new ErrorContentDialog { };
                    await _dialog.ShowErrorDialogAsync(content: string.Format(localizer.GetLocalizedString("ServiceStopException"), "WINDIVERT_STOP_ERROR"),
                        errorDetails: $"{ex.Message}",
                        xamlRoot: this.Content.XamlRoot);
                }
            }
        }
    }
}
