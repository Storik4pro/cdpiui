using CDPIUI_TrayIcon.Helper;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Globalization;
using System.Resources;
using Windows.Foundation.Collections;
using Application = System.Windows.Forms.Application;

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

        _ = TrayIconHelper.Instance;

        if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated())
        {
            Application.Run();
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
                File.Move(newUpdateFilePath, updateFilePath);
            }
        }
        catch
        {
            if (args.Contains("--after-patching"))
            {
                Logger.Instance.CreateErrorLog("Update", "Update not finished correctly");
                TrayIconHelper.Instance.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateFailure"), "UPDATE:OPEN_LOG");
            }
        }

        
        

        if (args.Contains("--after-failed-update"))
        {
            TrayIconHelper.Instance.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateFailure"), "UPDATE:OPEN_LOG");
        }

        if (args.Contains("--autorun"))
        {
            _ = ProcessManager.Instance.StartProcess();
            if (SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "trayHide")) 
                TrayIconHelper.Instance.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("TrayHide"), "SHOW_MAIN_WINDOW");
        }

        CheckProgramUpdates();

        Application.Run();
        ToastNotificationManagerCompat.History.Clear();
    }

    private static void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat toastArgs)
    {
        try
        {
            var _toastArgs = ToastArguments.Parse(toastArgs.Argument);

            if (_toastArgs.TryGetValue("action", out string action))
            {
                TrayIconHelper.Instance.HandleToastActionFromBackground(action);
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
            if (! await PipeServer.Instance.SendMessage("UPDATE:CHECK"))
            {
                RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), "--create-no-window --check-program-updates --exit-after-action");
            }
        }
    }
}