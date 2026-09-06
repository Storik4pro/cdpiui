using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.System;

using Microsoft.UI.Xaml;
using System;
using System.IO;

namespace CDPIUI.Helper.AddOns;

public class HostsFileEditHelper
{
    public static void EditHostsFile(XamlRoot xamlRoot)
    {
        if (!SettingsManager.Instance.GetValue<bool>("FILEOPENACTIONS", "isDialogShown") || 
            !SettingsManager.Instance.GetValueOrDefault("FILEOPENACTIONS", "doNotRemindAgain", 
            defaultValue: true))
        {
            ShowEditAskDialog(Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows), "System32/drivers/etc/hosts"), xamlRoot);
        }
        else
        {
            ShellHelper.OpenFile(Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows), "System32/drivers/etc/hosts"), true, useNotepadAsDefault: true);
        }
    }

    private static async void ShowEditAskDialog(string file, XamlRoot xamlRoot)
    {
        EditSitelistAskApplicationContentDialog editSitelistAskApplicationContentDialog = new()
        {
            XamlRoot = xamlRoot,
            FilePath = file,
            UseUAC = true,
            UseNotepadAsDefault = true,
        };
        await editSitelistAskApplicationContentDialog.ShowAsync();
        if (editSitelistAskApplicationContentDialog.IsSuccess)
            SettingsManager.Instance.SetValue("FILEOPENACTIONS", "isDialogShown", true);
    }
}