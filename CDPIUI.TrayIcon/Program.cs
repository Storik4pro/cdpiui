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
using CDPIUI.Shared.Migration;
using CDPIUI.TrayIcon.ConditionalLaunch;

class Programm
{

    [STAThread]
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Instance.CreateDebugLog("Startup",
                $"Unhandled exception; terminating={e.IsTerminating}: {e.ExceptionObject}");
        using (var process = Process.GetCurrentProcess())
            Logger.Instance.CreateDebugLog("Startup",
                $"Starting PID={process.Id}, session={process.SessionId}, path='{Environment.ProcessPath}', " +
                $"autorun={args.Contains("--autorun")}.");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;

        ToastNotificationManagerCompat.History.Clear();

        PipeServer.Instance.Init();
        PipeServer.Instance.Start();

        if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated())
        {
            using var toastContext = new TrayApplicationContext();
            Application.Run(toastContext);
            return;
        }

        GoodbyeDpiMigrationActivation.TryFindArgument(args, out var migrationRequest);
        if (args.Contains("--show-ui") || args.Contains("--after-patching") ||
            args.Contains("--after-failed-update") || migrationRequest != null)
        {
            RunHelper.RunAsDesktopUser(
                Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"),
                migrationRequest?.RawArgument ?? string.Empty);
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
            if (SettingsManager.Instance.GetValue<bool>("NOTIFICATIONS", "trayHide"))
                NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("TrayHide"), "SHOW_MAIN_WINDOW");
        }

        CheckProgramUpdates();
        BeginCompatibilityCheck();

        

        using var context = new TrayApplicationContext(args.Contains("--autorun"));
        Application.Run(context);
        ToastNotificationManagerCompat.History.Clear();
    }

    public class TrayApplicationContext : ApplicationContext
    {
        private readonly EmptyForm _trayForm;
        private readonly CancellationTokenSource _startupCancellation = new();
        private bool _disposed;

        public TrayApplicationContext(bool runStartupActions = false)
        {
            _trayForm = new EmptyForm();

            _trayForm.AddIcon(notify: true);
            try
            {
                ConditionalLaunchEngine.Instance.Start(_trayForm);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(ConditionalLaunchEngine),
                    $"Conditional launch engine could not be started: {ex}");
            }

            NotifyHelper.Instance.Init();

            if (SettingsManager.Instance.HasUnrecoverableLoadError ||
                File.Exists(SettingsManager.Instance.RecoveryNoticePath))
            {
                NotifyHelper.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("SettingsRecoveryWarning"), "SHOW_MAIN_WINDOW");
            }

            if (runStartupActions)
            {
                // Start after the message loop is available; retries do not depend on the icon.
                _trayForm.BeginInvoke(new Action(() => _ = AskStartupActions(_startupCancellation.Token)));
            }
        }

        protected override void ExitThreadCore()
        {
            if (!_disposed) _startupCancellation.Cancel();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _startupCancellation.Cancel();
                _startupCancellation.Dispose();
                _trayForm.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private static Task AskStartupActions(CancellationToken cancellationToken)
    {
        return StartupActionDispatcher.DispatchAsync(
            () => PipeHelper.SendConPTYPacket(CONPTYMessageIds.GetAllStartupStrings, string.Empty, openIfNotConnected: true),
            message => Logger.Instance.CreateDebugLog("Startup", message),
            cancellationToken);
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
