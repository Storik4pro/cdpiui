using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Forms;
using static System.Windows.Forms.AxHost;

namespace CDPIUI_TrayIcon.Helper
{

    public class TrayIconHelper : IDisposable
    {
        private readonly ContextMenuStrip? ContextMenu;

        private readonly ToolStripItem? ProcessControlItem;

        private readonly ToolStripMenuItem? StoreMenuItem;

        private readonly Dictionary<string, Tuple<ToolStripMenuItem, ToolStripItem>> ComponentControlElements = new();
        private readonly Dictionary<string, string> SupportedComponents = new()
        {
            { "CSZTBN012", "Zapret" },
            { "CSBIHA024", "ByeDPI" },
            { "CSGIVS036", "GoodbyeDPI" },
            { "CSSIXC048", "SpoofDPI" },
            { "CSNIG9025", "NoDPI" },
        };

        private readonly ToolStripItem? ShowAppItem;
        private readonly ToolStripItem? ExitItem;

        private static TrayIconHelper? _instance;
        private static readonly object _lock = new object();

        public static TrayIconHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new TrayIconHelper();
                    return _instance;
                }
            }
        }

        private NotifyIcon notifyIcon = new NotifyIcon();

        

        private static readonly Icon NormalIcon = new(Utils.Assembly.GetManifestResourceStream("CDPIUI_TrayIcon.Assets.trayLogoNormal.ico")!);
        private static readonly Icon StoppedIcon = new(Utils.Assembly.GetManifestResourceStream("CDPIUI_TrayIcon.Assets.trayLogoStopped.ico")!);
        private static readonly Icon RunnedIcon = new(Utils.Assembly.GetManifestResourceStream("CDPIUI_TrayIcon.Assets.trayLogoStarted.ico")!);
        private static readonly Icon RunnedNotAllIcon = new(Utils.Assembly.GetManifestResourceStream("CDPIUI_TrayIcon.Assets.trayLogoStartedNotAll.ico")!);
        public TrayIconHelper()
        {
            notifyIcon.MouseClick += NotifyIcon_Click;
            notifyIcon.Icon = NormalIcon;
            notifyIcon.Visible = true;
            notifyIcon.Text = "CDPI UI";

            ContextMenu = new ContextMenuStrip();
            ContextMenu.ShowImageMargin = true;
            ContextMenu.BackColor = ColorTranslator.FromHtml("#2C2C2C");
            ContextMenu.ForeColor = Color.White;
            ContextMenu.Renderer = new ToolStripProfessionalRenderer(new LeftMenuColorTable());

            foreach (var pair in SupportedComponents)
            {
                CreateComponentItem(pair.Key, pair.Value);
            }

            StoreMenuItem = new(LocaleHelper.GetLocaleString("Store"));
            StoreMenuItem.Click += StoreMenuItem_Click;

            ContextMenu.Items.Add(StoreMenuItem);

            ContextMenu.Items.Add("-");

            ShowAppItem = ContextMenu.Items.Add(LocaleHelper.GetLocaleString("ShowMainWindow"), NormalIcon.ToBitmap());
            ShowAppItem.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            ShowAppItem.Click += ShowAppItem_Click;

            ExitItem = ContextMenu.Items.Add(LocaleHelper.GetLocaleString("Exit"), null);
            ExitItem.Click += ExitItem_Click;
            notifyIcon.ContextMenuStrip = ContextMenu;

            ContextMenu.HandleCreated += ContextMenu_HandleCreated;

            TasksHelper.Instance.TaskStateUpdated += HandleTaskStateUpdate;
            TasksHelper.Instance.TaskListUpdated += HandleTasksListUpdate;
        }

        public void ToggleComponentAvailability(string id, bool state)
        {
            ComponentControlElements.TryGetValue(id, out var item);
            if (item != null)
            {
                item.Item1.Enabled = state;
            }
        }

        private async void StoreMenuItem_Click(object? sender, EventArgs e)
        {
            if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_STORE"))
            {
                RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), "--show-store");
            }
        }

        private void CreateComponentItem(string id, string displayName)
        {
            ToolStripMenuItem menuItem = new(displayName);
            ToolStripItem runItem;

            runItem = menuItem.DropDownItems.Add(LocaleHelper.GetLocaleString("Start"), null);
            runItem.ForeColor = Color.White;
            runItem.Click += (s, e) => { RunProcessById(id); };
            var viewOutputButton = menuItem.DropDownItems.Add(LocaleHelper.GetLocaleString("Pseudoconsole"), null);
            viewOutputButton.ForeColor = Color.White;
            viewOutputButton.Click += (s, e) => { OpenPseudoconsole(id); };

            menuItem.Visible = false;

            ContextMenu?.Items.Add(menuItem);

            ComponentControlElements.Add(id, Tuple.Create(menuItem, runItem));
        }

        private async void ContextMenu_HandleCreated(object? sender, EventArgs e)
        {
            SafeHandleListUpdate();
            foreach (var task in TasksHelper.Instance.Tasks)
            {
                ChangeRunItemText(task.Id, await TasksHelper.Instance.IsTaskRunned(task.Id));
            }
            
        }

        private void ChangeRunItemText(string id, bool state)
        {
            string text = state ? LocaleHelper.GetLocaleString("Stop") : LocaleHelper.GetLocaleString("Start");

            var runControlItem = ComponentControlElements.FirstOrDefault((x) => x.Key == id).Value?.Item2;
            if (runControlItem != null)
            {
                runControlItem.Text = text;
            }
            
        }

        private void HandleTasksListUpdate()
        {
            if (ContextMenu.IsHandleCreated)
            {
                ContextMenu.Invoke(new Action(() =>
                {
                    SafeHandleListUpdate();
                }));
            }
        }

        private void SafeHandleListUpdate()
        {
            Debug.WriteLine(TasksHelper.Instance.Tasks.Count);
            if (TasksHelper.Instance.Tasks.Count == 0)
            {
                StoreMenuItem.Visible = true;
            }
            else
            {
                StoreMenuItem.Visible = false;
            }
            foreach (var pair in ComponentControlElements)
            {
                pair.Value.Item1.Visible = false;
            }
            foreach (var task in TasksHelper.Instance.Tasks)
            {
                ComponentControlElements.TryGetValue(task.Id, out var item);
                if (item != null)
                {
                    item.Item1.Visible = true;
                }
            }
        }

        private async void HandleTaskStateUpdate(Tuple<string, bool> taskStateUpdate)
        {
            if (ContextMenu.IsHandleCreated)
            {
                ContextMenu.Invoke(new Action(() =>
                {
                    ChangeRunItemText(taskStateUpdate.Item1, taskStateUpdate.Item2);
                }));
            }
            
            if (taskStateUpdate.Item2)
            {
                if (IsAnyTaskHasState(!taskStateUpdate.Item2))
                {
                    notifyIcon.Icon = RunnedNotAllIcon;
                }
                else
                {
                    notifyIcon.Icon = RunnedIcon;
                }
            }
            else
            {
                if (IsAnyTaskHasState(!taskStateUpdate.Item2))
                {
                    notifyIcon.Icon = RunnedNotAllIcon;
                }
                else
                {
                    notifyIcon.Icon = StoppedIcon;
                }
            }

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

        private static bool IsAnyTaskHasState(bool state)
        {
            foreach (var task in TasksHelper.Instance.Tasks)
            {
                if (task.ProcessManager != null)
                {
                    if (task.ProcessManager.GetState() == state) return true;
                }
            }
            return false;
        }

        private async void OpenPseudoconsole(string id)
        {
            if (!await PipeServer.Instance.SendMessage($"WINDOW:SHOW_PSEUDOCONSOLE({id})"))
            {
                RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), $"--show-pseudoconsole={id}");
            }
        }

        private async void ShowAppItem_Click(object? sender, EventArgs e)
        {
            if (! await PipeServer.Instance.SendMessage("WINDOW:SHOW_MAIN"))
            {
                RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), string.Empty);
            }
        }

        private void ExitItem_Click(object? sender, EventArgs e)
        {
            _ = PipeServer.Instance.SendMessage("MAIN:EXIT_ALL");
            Application.Exit();
        }

        public void ToggleStartButtonEnabled(bool enabled)
        {
            if (ProcessControlItem != null) ProcessControlItem.Enabled = enabled;
        }

        private async void NotifyIcon_Click(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (! await PipeServer.Instance.SendMessage("WINDOW:SHOW_MAIN"))
                {
                    RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), string.Empty);
                }
            }
        }

        public void ShowMessage(string title, string? message, string action)
        {
            new ToastContentBuilder()
                .AddArgument("action", action)
                .AddText(title)
                .AddText(message)
                .Show();
            /*
            var assembly = typeof(Utils).Assembly;
            using (var stream = assembly.GetManifestResourceStream("CDPIUI_TrayIcon.Assets.trayLogoError.ico"))
            {

                string tempFile = Path.Combine(Path.GetTempPath(), "trayLogoError_" + Guid.NewGuid().ToString() + ".ico");

                using (var fs = File.Create(tempFile))
                {
                    stream.CopyTo(fs);
                }

                

                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(2));
                    try { File.Delete(tempFile); } catch { }
                });
            }
            */
        }

        public async void HandleToastActionFromBackground(string action)
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
                Logger.Instance.CreateErrorLog(nameof(TrayIconHelper), $"ERR_UNABLE_OPEN_FILE details => {ex.Message}");
            }
        }

        private async void RunProcessById(string id)
        {
            if (!await TasksHelper.Instance.IsTaskRunned(id))
            {
                TasksHelper.Instance.CreateAndRunNewTask(id);
            }
            else
            {
                await TasksHelper.Instance.StopTask(id);
            }
        }

        public void Dispose() 
        {
            notifyIcon.Dispose();
        }
    }

    public class LeftMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuItemBorder
        {
            get { return ColorTranslator.FromHtml("#353535"); }
        }

        public override Color MenuBorder 
        {
            get { return ColorTranslator.FromHtml("#3E3E3E"); }
        }

        public override Color MenuItemPressedGradientBegin
        {
            get { return ColorTranslator.FromHtml("#4C4A48"); }
        }
        public override Color MenuItemPressedGradientEnd
        {
            get { return ColorTranslator.FromHtml("#5F5D5B"); }
        }

        public override Color ToolStripBorder
        {
            get { return ColorTranslator.FromHtml("#3E3E3E"); }
        }

        public override Color MenuItemSelectedGradientBegin
        {
            get { return ColorTranslator.FromHtml("#353535"); }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get { return ColorTranslator.FromHtml("#353535"); }
        }

        public override Color ToolStripDropDownBackground
        {
            get { return ColorTranslator.FromHtml("#2C2C2C"); }
        }

        public override Color ToolStripGradientBegin
        {
            get { return ColorTranslator.FromHtml("#2C2C2C"); }
        }

        public override Color ToolStripGradientEnd
        {
            get { return ColorTranslator.FromHtml("#2C2C2C"); }
        }

        public override Color ToolStripGradientMiddle
        {
            get { return ColorTranslator.FromHtml("#2C2C2C"); }
        }

        public override Color ImageMarginGradientMiddle
        {
            get { return ColorTranslator.FromHtml("#2C2C2C"); }
        }
        public override Color ImageMarginGradientBegin
        {
            get { return ColorTranslator.FromHtml("#2C2C2C"); }
        }
        public override Color ImageMarginGradientEnd 
        {
            get { return ColorTranslator.FromHtml("#2C2C2C"); }
        }
    }
}
