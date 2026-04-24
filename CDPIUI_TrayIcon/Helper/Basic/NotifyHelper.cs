using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using WinRT.Interop;
using static System.Windows.Forms.AxHost;

namespace CDPIUI_TrayIcon.Helper.Basic
{

    public class NotifyHelper : IDisposable
    {
        private static NotifyHelper? _instance;
        private static readonly object _lock = new object();

        public static NotifyHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new NotifyHelper();
                    return _instance;
                }
            }
        }

        public NotifyHelper()
        {
            
        }

        public void Init()
        {
            TasksHelper.Instance.TaskStateUpdated += HandleTaskStateUpdate;
        }

        private async void HandleTaskStateUpdate(Tuple<string, bool> taskStateUpdate)
        {
            string procName = (await TasksHelper.Instance.GetTaskFromId(taskStateUpdate.Item1))?.ProcessManager.ProcessName ?? $"{taskStateUpdate.Item1}";

            if (taskStateUpdate.Item2)
            {
                if (!SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "procState")) return;
                ShowMessage(procName, LocaleHelper.GetLocaleString("ProcRun"), $"OPEN_PSEUDOCONSOLE({taskStateUpdate.Item1})");
            }
            else
            {
                if (!SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "procState")) return;
                ShowMessage(procName, LocaleHelper.GetLocaleString("ProcStop"), $"OPEN_PSEUDOCONSOLE({taskStateUpdate.Item1})");
            }
        }

        public static void ShowMessage(string title, string? message, string action)
        {
            new ToastContentBuilder()
                .AddArgument("action", action)
                .AddText(title)
                .AddText(message)
                .Show();
        }

        public static async void HandleToastActionFromBackground(string action)
        {
            if (action.StartsWith("OPEN_PSEUDOCONSOLE"))
            {
                var result = ScriptHelper.GetArgsFromString(action);
                if (result.Length < 1)
                {
                    Console.WriteLine($"ERR, {action} => args exception");
                    return;
                }

                if (!await PipeServer.Instance.SendMessage($"WINDOW:SHOW_PSEUDOCONSOLE({result[0]})"))
                {
                    RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), $"--show-pseudoconsole={result[0]}");
                }
                return;
            }
            switch (action)
            {
                case "SHOW_MAIN_WINDOW":
                    if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_MAIN"))
                    {
                        RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), string.Empty);
                    }
                    break;
                case "OPEN_PROXY_SETUP":
                    if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_PROXY_SETUP"))
                    {
                        RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), "--show-proxy-setup");
                    }
                    break;
                case "OPEN_BEGIN_STORE_UPDATE_CHECK":
                    if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_BEGIN_STORE_UPDATE_CHECK"))
                    {
                        RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), "--show-begin-store-update-check");
                    }
                    break;
                case "LOGGER:OPEN_TRAY_LOG":
                    OpenFileInDefaultApp(Path.Combine(Utils.GetDataDirectory(), "Logs", "EmptyForm.log"));
                    break;
                case "LOGGER:OPEN_MSI_LOG":
                    OpenFileInDefaultApp(Path.Combine(Utils.GetDataDirectory(), "Logs", "MsiInstallerHelper.log"));
                    break;
                case "UPDATE:OPEN_LOG":
                    OpenFileInDefaultApp(Path.Combine(Utils.GetDataDirectory(), "update.log"));
                    break;
                case "UPDATE:OPEN_DOWNLOAD_PAGE":
                    if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_MAIN:UPDATE_PAGE"))
                    {
                        RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), "--show-update-page");
                    }
                    break;
                

            }
        }

        public void ShowTrayErrorMessage(string errorCode)
        {
            ShowMessage(LocaleHelper.GetLocaleString("TrayErrorTitle"), string.Format(LocaleHelper.GetLocaleString("TrayErrorMessage"), errorCode), $"LOGGER:OPEN_TRAY_LOG");
        }

        public static void OpenFileInDefaultApp(string filePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Path.GetFullPath(filePath),
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(NotifyHelper), $"ERR_UNABLE_OPEN_FILE details => {ex.Message}");
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
