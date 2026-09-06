using CDPIUI.Controls.Dialogs;
using CDPIUI.Controls.Dialogs.Universal;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Default;
using CDPIUI.Helper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.Helper.WindowHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class ViewWindow : TemplateWindow
{
    private readonly ILocalizer localizer = Localizer.Get();

    public ViewWindow()
    {
        NewIdSet += SetId;
        InitializeComponent();

        WindowTitle = localizer.GetLocalizedString("PseudoconsoleWindowTitle");
        IconUri = @"Assets/Icons/Pseudoconsole.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        Closed += ViewWindow_Closed;

        WindowsPositionHelper.TrySetMicaBackdrop(true, this, MainGrid);

        CleanOutputButton.IsChecked = SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "outputMode");
        DefaultOutputButton.IsChecked = !CleanOutputButton.IsChecked;
        HidePathsButton.IsChecked = SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "prettyPathView");
        ShowPathsButton.IsChecked = !HidePathsButton.IsChecked;

        ConfigOutput.RunningStateChanged += ConfigOutput_RunningStateChanged;
        SetId();
    }

    public bool IsActive() => DispatcherQueue != null;

    private async void SetId()
    {
        if (!string.Equals(ConfigOutput.ComponentId, Id, StringComparison.Ordinal))
        {
            ConfigOutput.ComponentId = Id;
        }
        else
        {
            await ConfigOutput.RefreshComponentAsync();
        }
        UpdateProcessControls(ConfigOutput.IsProcessRunning);
    }

    private async Task<ProcessService> GetProcessManagerAsync() =>
        (await ComponentTasksManager.Instance.GetTaskFromId(Id))?.ProcessManager;

    private void ConfigOutput_RunningStateChanged(bool isRunning)
    {
        DispatcherQueue.TryEnqueue(() => UpdateProcessControls(isRunning));
    }

    private void UpdateProcessControls(bool isRunning)
    {
        ProcessControlIcon.Glyph = isRunning ? "\uE71A" : "\uE768";
        ProcessControl.Text = localizer.GetLocalizedString(isRunning ? "Stop" : "Start");
        ProcessRestart.IsEnabled = isRunning;
    }

    private void ViewWindow_Closed(object sender, WindowEventArgs args)
    {
        NewIdSet -= SetId;
        ConfigOutput.RunningStateChanged -= ConfigOutput_RunningStateChanged;
        Closed -= ViewWindow_Closed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            OverwritePrompt = true,
            FileName = "PseudoConsoleLog.txt",
            DefaultExt = ".txt",
            Filter = "TXT Files|*.txt",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, ConfigOutput.OutputText);
        }
        catch (Exception exception)
        {
            ErrorContentDialog errorDialog = new();
            await errorDialog.ShowErrorDialogAsync(
                content: string.Format(
                    localizer.GetLocalizedString("FileSaveErrorMessage"),
                    dialog.FileName,
                    "ERR_FILE_WRITE"),
                errorDetails: exception.ToString(),
                xamlRoot: Content.XamlRoot);
        }
    }

    private async void ProcessControl_Click(object sender, RoutedEventArgs e)
    {
        ProcessService processManager = await GetProcessManagerAsync();
        if (processManager == null)
        {
            return;
        }

        if (processManager.IsProcessRunning)
        {
            await processManager.StopProcess();
        }
        else
        {
            await processManager.StartProcess();
        }
    }

    private async void ProcessRestart_Click(object sender, RoutedEventArgs e)
    {
        ProcessService processManager = await GetProcessManagerAsync();
        if (processManager != null)
        {
            await processManager.RestartProcess();
        }
    }

    private void CleanOutputButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("PSEUDOCONSOLE", "outputMode", true);
        ConfigOutput.RefreshOutput();
    }

    private void DefaultOutputButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("PSEUDOCONSOLE", "outputMode", false);
        ConfigOutput.RefreshOutput();
    }

    private void ShowPathsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("PSEUDOCONSOLE", "prettyPathView", false);
        ConfigOutput.RefreshOutput();
    }

    private void HidePathsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue("PSEUDOCONSOLE", "prettyPathView", true);
        ConfigOutput.RefreshOutput();
    }

    private void ShowFontSettingsDialog()
    {
        ConsoleFontHelper.Instance.ShowFontSettingsDialogForXamlRoot(Content.XamlRoot);
    }

    private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e) => ShowFontSettingsDialog();

    private void SupportButton_Click(object sender, RoutedEventArgs e) => UrlOpenHelper.LaunchReportUrl();

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        ConfigOutput.CopyAll();
        CopyIcon.Glyph = "\uE73E";
        await Task.Delay(1000);
        CopyIcon.Glyph = "\uE8C8";
    }

    private void StopServiceButton_Click(object sender, RoutedEventArgs e) =>
        CDPIUI.Commands.CommandsHandler.HandleCommand("cdpiui://Tools/Service");
}
