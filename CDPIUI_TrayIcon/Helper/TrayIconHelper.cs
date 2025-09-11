using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI_TrayIcon.Helper
{
    public class TrayIconHelper : IDisposable
    {
        private ToolStripItem? ExitItem;
        private ToolStripItem? ProcessControlItem;
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
        public TrayIconHelper() 
        {
            notifyIcon.Click += NotifyIcon_Click;
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            notifyIcon.Visible = true;
            notifyIcon.Text = "CDPIUI";

            var contextMenu = new ContextMenuStrip();

            ProcessControlItem = contextMenu.Items.Add("Start", null);
            ProcessControlItem.Click += ProcessControlItem_Click;

            ExitItem = contextMenu.Items.Add("Exit", null, (s, e) => { PipeServer.Instance.SendMessage("MAIN:EXIT_ALL"); });
            notifyIcon.ContextMenuStrip = contextMenu;

            ProcessManager.Instance.ProcessStateChanged += ProcessManager_StateChanged;
        }

        public void ToggleStartButtonEnabled(bool enabled)
        {
            if (ProcessControlItem != null) ProcessControlItem.Enabled = enabled;
        }

        private void ProcessManager_StateChanged(bool state)
        {
            if (ProcessControlItem != null) ProcessControlItem.Text = state ? "Stop" : "Start";
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

        private void NotifyIcon_Click(object? sender, EventArgs e)
        {

        }

        public void Dispose() 
        {
            notifyIcon.Dispose();
        }
    }
}
