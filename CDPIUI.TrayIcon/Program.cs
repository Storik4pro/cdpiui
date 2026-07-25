using CDPIUI.TrayIcon.Helper.Basic;
using CDPIUI.TrayIcon.Forms;
using CDPIUI.TrayIcon.Helper;
using Microsoft.Toolkit.Uwp.Notifications;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Windows.Forms;
using Windows.Foundation.Collections;
using Application = System.Windows.Forms.Application;
using CDPIUI.Shared.Pipe.Models;

class Programm
{

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;

        ToastNotificationManagerCompat.History.Clear();

        PipeServer.Instance.Init();
        PipeServer.Instance.Start();

        if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated())
        {
            Application.Run(new TrayApplicationContext());
            return;
        }

        if (args.Contains("--show-ui") || args.Contains("--after-patching") || args.Contains("--after-failed-update"))
        {
            RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), string.Empty);
        }

        string updateFilePath = Path.Combine(Utils.GetDataDirectory(), "Update.exe");
        string newUpdateFilePath = Path.Combine(Utils.GetDataDirectory(), "_Update.exe");
        try
        {
            if (File.Exists(newUpdateFilePath))
            {
                File.Move(newUpdateFilePath, updateFilePath, overwrite: true);
            }
        }
        catch
        {
            if (args.Contains("--after-patching"))
            {
                Logger.Instance.CreateErrorLog("Update", "Update not finished correctly");
                NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateFailure"), "UPDATE:OPEN_LOG");
            }
        }

        
        

        if (args.Contains("--after-failed-update"))
        {
            NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateFailure"), "UPDATE:OPEN_LOG");
        }

        if (args.Contains("--autorun"))
        {
            AskStartupActions();
            if (SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "trayHide"))
                NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("TrayHide"), "SHOW_MAIN_WINDOW");
        }

        CheckProgramUpdates();
        BeginCompatibilityCheck();

        

        Application.Run(new TrayApplicationContext());
        ToastNotificationManagerCompat.History.Clear();
    }

    public class TrayApplicationContext : ApplicationContext
    {
        public TrayApplicationContext()
        {
            EmptyForm trayMenuForm = new();

            trayMenuForm.AddIcon(notify: true); // TODO: change to false

            NotifyHelper.Instance.Init();
        }
    }

    private static async void AskStartupActions()
    {
        await PipeHelper.SendConPTYPacket(CONPTYMessageIds.GetAllStartupStrings, string.Empty, openIfNotConnected: true);
    }

    private static void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat toastArgs)
    {
        try
        {
            var _toastArgs = ToastArguments.Parse(toastArgs.Argument);

            if (_toastArgs.TryGetValue("action", out string action))
            {
                NotifyHelper.HandleToastActionFromBackground(action);
            }
            else { }

            ValueSet userInput = toastArgs.UserInput;
        }
        catch { }
    }

    private static async void CheckProgramUpdates()
    {
        await Task.Delay(TimeSpan.FromMinutes(30));
        if (SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "appUpdates"))
        {
            await PipeHelper.SendCheckUpdatesPacket();
        }
    }

    private static async void BeginCompatibilityCheck()
    {
        await Task.Delay(TimeSpan.FromMinutes(60));
        if (SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "compatibilityCheck"))
        {
            await PipeHelper.SendCompatibilityCheckPacket();
        }
    }
}