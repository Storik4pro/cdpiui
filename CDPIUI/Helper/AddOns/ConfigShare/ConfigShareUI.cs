using CDPIUI.AddOns.ConfigShare;
using CDPIUI.Controls.ComponentSettings;
using CDPIUI.Controls.Dialogs.ConfigShare;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Data;
using CDPIUI.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.Helper.AddOns.ConfigShare;

internal static class ConfigShareUI
{
    internal static string Text(string key) => Localizer.Get().GetLocalizedString(key);
    internal static string ErrorText(Exception exception)
    {
        string code = (exception as ConfigShareException)?.Code ?? "SHARE_OPERATION_FAILED";
        string message = Text("ConfigShareError_" + code);
        if (string.IsNullOrWhiteSpace(message) || message == "ConfigShareError_" + code)
            message = Text("ConfigShareError");
        return $"{message}\n{code}: {exception.Message}";
    }

    internal static async Task ShowExportAsync(XamlRoot root, ConfigSelectorItem selected)
    {
        await Task.Run(ConfigShareService.CleanupPreviousSystemShares);
        var dialog = new ConfigShareExportContentDialog(selected) { XamlRoot = root };
        await dialog.ShowAsync();
    }

    internal static async Task OfferComponentInstallAsync(XamlRoot root, ConfigItem config)
    {
        string componentId = ConfigShareService.GetMissingComponentId(config);
        if (string.IsNullOrWhiteSpace(componentId)) return;
        var known = HardcodedItemIds.ComponentIds.FirstOrDefault(item => item.Value == componentId);
        string componentName = known.Value == null ? componentId
            : known.Key == Components.Zapret ? "Zapret Legacy" : known.Key.ToString();
        var dialog = new ConfigShareMessageContentDialog
        {
            XamlRoot = root,
            Title = Text("ConfigShareMissingComponentTitle"),
            Message = string.Format(Text("ConfigShareMissingComponentMessage"), config.name, componentName),
            PrimaryButtonText = Text("ConfigShareInstallComponent"),
            CloseButtonText = Text("ConfigShareLater")
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (ConfigShareService.GetMissingComponentId(config) == null) return;
        Logger.Instance.CreateInfoLog(nameof(ConfigShareUI), $"Opening required component installer: {componentId}");
        var app = (App)Application.Current;
        var installer = await app.UnsafeCreateNewWindow<StoreSmallDownloadDialog>(activate: false, id: componentId);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnClosed(object sender, WindowEventArgs args) => closed.TrySetResult();
        installer.Closed += OnClosed;
        try
        {
            App.ActivateWindow(installer);
            await closed.Task;
        }
        finally { installer.Closed -= OnClosed; }
    }
}
