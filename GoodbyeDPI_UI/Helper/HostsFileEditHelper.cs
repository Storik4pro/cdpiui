using CDPI_UI.Controls.Dialogs.ComponentSettings;
using CDPI_UI.Helper.Static;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper
{
    internal class HostsFileEditHelper
    {
        private enum Flags
        {
            add,
            remove,
            recover,
        }
        private static Process RunEditorWithFlag(Flags flag)
        {
            string startupString = $"/{flag}";
            string path = Path.Combine(StateHelper.GetDataDirectory(getCurrent: true), "EditHostFile.exe");

            if (!Path.Exists(path)) throw new ApplicationFilesDamagedException("File not found");

            var psi = new ProcessStartInfo(path, startupString)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            var process = Process.Start(psi);
            return process;
        }

        public static async Task<int> AddDomains()
        {
            Process process = RunEditorWithFlag(Flags.add);
            await process.WaitForExitAsync();
            
            return process.ExitCode;
        }

        public static async Task<int> RemoveDomains()
        {
            Process process = RunEditorWithFlag(Flags.remove);
            await process.WaitForExitAsync();
            
            return process.ExitCode;
        }

        public static async Task<int> RestoreDomains()
        {
            Process process = RunEditorWithFlag(Flags.recover);
            await process.WaitForExitAsync();
            
            return process.ExitCode;
        }

        public static void EditHostsFile(XamlRoot xamlRoot)
        {
            if (!SettingsManager.Instance.GetValue<bool>("FILEOPENACTIONS", "isDialogShown") || !SettingsManager.Instance.GetValueOrDefault<bool>("FILEOPENACTIONS", "doNotRemindAgain", defaultValue: true))
            {
                ShowEditAskDialog(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/drivers/etc/hosts"), xamlRoot);
            }
            else
            {
                Utils.OpenFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/drivers/etc/hosts"), true, useNotepadAsDefault:true);
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
}
