using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.JSON;
using CDPIUI.Helper.AddOns.ConfigShare;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Controls.Dialogs.ConfigShare;

public sealed partial class ConfigShareExportContentDialog : ContentDialog
{
    private readonly ConfigSelectorItem selected;
    private bool initialized;
    private bool busy;
    private CancellationTokenSource shareCancellation;

    public ConfigShareExportContentDialog(ConfigSelectorItem selected)
    {
        this.selected = selected;
        InitializeComponent();
        PresetNameBox.Header = ConfigShareUI.Text("ConfigSharePresetName");
        DeveloperNameBox.Header = ConfigShareUI.Text("ConfigShareDeveloper");
        PresetNameBox.Text = selected.DisplayName;
        DeveloperNameBox.Text = SettingsManager.Instance.GetValueOrDefault("CONFIGKIT", "lastUsedDevName", defaultValue: Environment.UserName);
        ExportStatus.Text = ConfigShareUI.Text("ConfigShareCollecting");
        initialized = true;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool valid = !busy && !string.IsNullOrWhiteSpace(PresetNameBox.Text) &&
            !string.IsNullOrWhiteSpace(DeveloperNameBox.Text);
        SaveButton.IsEnabled = ShareButton.IsEnabled = valid;
    }

    private void PresetFields_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (initialized) UpdateButtons();
    }

    private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args) => args.Cancel = busy;

    private async Task ExportAsync(bool share)
    {
        if (busy) return;
        busy = true;
        UpdateButtons();
        PresetNameBox.IsEnabled = DeveloperNameBox.IsEnabled = false;
        ExportProgress.Visibility = ExportStatus.Visibility = Visibility.Visible;
        ExportError.IsOpen = false;

        if (!string.IsNullOrEmpty(DeveloperNameBox.Text)) SettingsManager.Instance.SetValue("CONFIGKIT", "lastUsedDevName", DeveloperNameBox.Text);

        try
        {
            ConfigItem config = await Task.Run(() => JSONConvertor.LoadJson<ConfigItem>(Path.Combine(
                ConfigurationService.GetItemFolderFromPackId(selected.PackId), selected.FileName)))
                ?? throw new ConfigShareException("SHARE_CONFIG_INVALID", selected.FileName);
            config.packId = selected.PackId;
            using var package = await new ConfigShareService().ExportAsync(config, PresetNameBox.Text, DeveloperNameBox.Text);
            if (share)
            {
                package.RetainForSystemShare();
                ExportStatus.Text = ConfigShareUI.Text("ConfigShareSharing");
                var owner = ((App)Application.Current).OpenWindows.FirstOrDefault(window => window.Content?.XamlRoot == XamlRoot)
                    ?? throw new InvalidOperationException("The sharing window is no longer available.");
                using var cancellation = new CancellationTokenSource();
                shareCancellation = cancellation;
                await WindowsPresetShare.ShareAsync(owner, package, cancellation.Token);
            }
            else
            {
                var picker = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = ConfigShareService.SafeFileName(PresetNameBox.Text) + ConfigShareService.Extension,
                    DefaultExt = ConfigShareService.Extension,
                    Filter = $"{ConfigShareUI.Text("ConfigShareFileType")}|*{ConfigShareService.Extension}", OverwritePrompt = true
                };
                if (picker.ShowDialog() != true) return;
                await Task.Run(() =>
                {
                    string destination = Path.GetFullPath(picker.FileName);
                    string staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.Copy(package.ArchivePath, staging, false);
                        File.Move(staging, destination, true);
                    }
                    finally { if (File.Exists(staging)) File.Delete(staging); }
                });
                Logger.Instance.CreateInfoLog(nameof(ConfigShareExportContentDialog), $"Preset exported to {picker.FileName}");
            }
            busy = false;
            Hide();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(ConfigShareExportContentDialog), exception.ToString());
            ExportError.Message = ConfigShareUI.ErrorText(exception);
            ExportError.IsOpen = true;
        }
        finally
        {
            busy = false;
            shareCancellation = null;
            PresetNameBox.IsEnabled = DeveloperNameBox.IsEnabled = true;
            ExportProgress.Visibility = ExportStatus.Visibility = Visibility.Collapsed;
            ExportStatus.Text = ConfigShareUI.Text("ConfigShareCollecting");
            UpdateButtons();
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await ExportAsync(share: false);
    }

    private async void ShareButton_Click(object sender, RoutedEventArgs e)
    {
        await ExportAsync(share: true);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
