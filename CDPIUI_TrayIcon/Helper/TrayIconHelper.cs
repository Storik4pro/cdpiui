using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;

namespace CDPIUI_TrayIcon.Helper
{

    public class TrayIconHelper : IDisposable
    {
        private readonly ToolStripItem? ProcessControlItem;

        private readonly ToolStripItem? PseudoconsoleItem;
        private readonly ToolStripMenuItem? UtilsItem;

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

        

        private static Icon NormalIcon = new Icon(Utils.Assembly.GetManifestResourceStream("CDPIUI_TrayIcon.Assets.trayLogoNormal.ico"));
        private static Icon ErrorIcon = new Icon(Utils.Assembly.GetManifestResourceStream("CDPIUI_TrayIcon.Assets.trayLogoError.ico"));
        private static Icon StoppedIcon = new Icon(Utils.Assembly.GetManifestResourceStream("CDPIUI_TrayIcon.Assets.trayLogoStopped.ico"));
        private static Icon RunnedIcon = new Icon(Utils.Assembly.GetManifestResourceStream("CDPIUI_TrayIcon.Assets.trayLogoStarted.ico"));
        public TrayIconHelper() 
        {
            notifyIcon.MouseClick += NotifyIcon_Click;
            notifyIcon.Icon = NormalIcon;
            notifyIcon.Visible = true;
            notifyIcon.Text = "CDPIUI";

            var contextMenu = new ContextMenuStrip();
            contextMenu.ShowImageMargin = true;
            contextMenu.BackColor = ColorTranslator.FromHtml("#2C2C2C");
            contextMenu.ForeColor = Color.White;
            contextMenu.Renderer = new ToolStripProfessionalRenderer(new LeftMenuColorTable());


            ProcessControlItem = contextMenu.Items.Add("Start", null);
            ProcessControlItem.Click += ProcessControlItem_Click;

            contextMenu.Items.Add("-");

            UtilsItem = new ToolStripMenuItem("Utils");
            PseudoconsoleItem = UtilsItem.DropDownItems.Add("Open pseudoconsole (View process output)");
            PseudoconsoleItem.ForeColor = Color.White;
            PseudoconsoleItem.Click += PseudoconsoleItem_Click;

            contextMenu.Items.Add(UtilsItem);

            contextMenu.Items.Add("-");

            ShowAppItem = contextMenu.Items.Add("Maximize app", NormalIcon.ToBitmap());
            ShowAppItem.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            ShowAppItem.Click += ShowAppItem_Click;

            ExitItem = contextMenu.Items.Add("Exit", null);
            ExitItem.Click += ExitItem_Click;
            notifyIcon.ContextMenuStrip = contextMenu;

            ProcessManager.Instance.ProcessStateChanged += ProcessManager_StateChanged;
        }

        private async void PseudoconsoleItem_Click(object? sender, EventArgs e)
        {
            if (! await PipeServer.Instance.SendMessage("WINDOW:SHOW_PSEUDOCONSOLE"))
            {
                RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), "--show-pseudoconsole");
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

        private void ProcessManager_StateChanged(bool state)
        {
            if (ProcessControlItem != null) 
                ProcessControlItem.Text = state ? "Stop" : "Start";

            if (state)
            {
                ShowMessage(ProcessManager.Instance.ProcessName, "Process is runned now.\nClick or tap here to open open pseudoconsole and view process output", "OPEN_PSEUDOCONSOLE");
                notifyIcon.Icon = RunnedIcon;
            }
            else
            {
                ShowMessage(ProcessManager.Instance.ProcessName, "Process is stopped now.\nClick or tap here to open open pseudoconsole and view process output", "OPEN_PSEUDOCONSOLE");
                notifyIcon.Icon = StoppedIcon;
            }
        }

        private void ProcessControlItem_Click(object? sender, EventArgs e)
        {
            if (ProcessManager.Instance.processState)
            {
                _ = ProcessManager.Instance.StopProcess();
            }
            else
            {
                _ = ProcessManager.Instance.StartProcess();
            }
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

        public void ShowMessage(string title, string message, string action)
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
            switch (action)
            {
                case "OPEN_PSEUDOCONSOLE":
                    if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_PSEUDOCONSOLE"))
                    {
                        RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), "--show-pseudoconsole");
                    }
                    break;
                case "SHOW_MAIN_WINDOW":
                    if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_MAIN"))
                    {
                        RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), string.Empty);
                    }
                    break;
                case "UPDATE:OPEN_LOG":
                    OpenFileInDefaultApp(Path.Combine(Utils.GetDataDirectory(), "update.log"));
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
                // TODO: Add logging
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
