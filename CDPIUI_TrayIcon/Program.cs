using CDPIUI_TrayIcon.Helper;
using Microsoft.Toolkit.Uwp.Notifications;
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

        if (args.Contains("--show-ui"))
        {
            RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), string.Empty);
        }

        if (args.Contains("--autorun"))
        {
            _ = ProcessManager.Instance.StartProcess();
            TrayIconHelper.Instance.ShowMessage("CDPI UI", "Application is runned and minimized to tray now.\nClick or tap here to open main window", "SHOW_MAIN_WINDOW");
        }

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
}