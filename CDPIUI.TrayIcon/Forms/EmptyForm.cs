using CDPIUI.TrayIcon.Helper.Basic;
using CDPIUI.TrayIcon.Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CDPIUI.TrayIcon.ConditionalLaunch;

namespace CDPIUI.TrayIcon.Forms
{
    // Used for creating tray menu handler only.

    public partial class EmptyForm : Form
    {
        private static readonly Guid IconDisplayGuid = GetGuid();
        private readonly System.Windows.Forms.Timer _iconRetryTimer = new() { Interval = 5000 };
        private bool _iconAdded;
        private int _iconAttempts;

        private TrayMenuForm? TrayMenuForm;

        public EmptyForm()
        {
            InitializeComponent();

            HideWindow();
            _iconRetryTimer.Tick += (_, _) => AddIcon(iconName: GetCurrentIcon(), toolTip: GetNowRunnedComponentsString());

            Application.ApplicationExit += Application_ApplicationExit;
            this.Disposed += EmptyForm_Disposed;

            ConnectHandlers();
        }

        private static Guid GetGuid()
        {
            string savedGUID = SettingsManager.Instance.GetValue<string>("TRAY", "iconGUID");
            if (savedGUID == "NaN")
            {
                Guid guid = Guid.NewGuid();
                SettingsManager.Instance.SetValue("TRAY", "iconGUID", guid.ToString());
                return guid;
            }
            else
            {
                return new(savedGUID);
            }
        }

        private void Application_ApplicationExit(object? sender, EventArgs e)
        {
            Application.ApplicationExit -= Application_ApplicationExit;
            this.Close();
            this.Dispose();
        }

        private void ConnectHandlers()
        {
            TasksHelper.Instance.TaskStateUpdated += HandleTaskStateUpdate;
            TasksHelper.Instance.TaskListUpdated += HandleTasksListUpdate;
            SystemEvents.PowerModeChanged += OnPowerChange;
        }

        private void DisconnectHandlers()
        {
            TasksHelper.Instance.TaskStateUpdated -= HandleTaskStateUpdate;
            TasksHelper.Instance.TaskListUpdated -= HandleTasksListUpdate;
            SystemEvents.PowerModeChanged -= OnPowerChange;
        }

        private async void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    foreach (var task in TasksHelper.Instance.Tasks)
                    {
                        if (task.ProcessManager.IsProcessRunning)
                        {
                            await TasksHelper.Instance.RestartTask(task.Id);
                        }
                    }
                    break;
                case PowerModes.Suspend:
                    break;
            }
        }

        private string GetCurrentIcon()
        {
            int rt = 0;

            foreach (var task in TasksHelper.Instance.Tasks)
            {
                if (task.ProcessManager.IsProcessRunning)
                {
                    rt++;
                }
            }

            if (rt == 0)
            {
                return "trayLogoStopped";
            }
            else if (rt == TasksHelper.Instance.Tasks.Count)
            {
                return "trayLogoStarted";
            }
            else
            {
                return "trayLogoStartedNotAll";
            }

        }

        private void HandleTaskStateUpdate(Tuple<string, bool> taskStateUpdate)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => HandleTaskStateUpdate(taskStateUpdate))); }
                catch (InvalidOperationException) { }
                return;
            }
            UpdateIcon(GetCurrentIcon(), GetNowRunnedComponentsString());
        }

        private void HandleTasksListUpdate()
        {

        }

        private static string GetNowRunnedComponentsString()
        {
            string result = string.Empty;
            int cnt = 0;
            foreach (var task in TasksHelper.Instance.Tasks)
            {
                if (task.ProcessManager.IsProcessRunning)
                {
                    result += $"{LocaleHelper.GetLocalizedComponentName(task.Id)}, ";
                    cnt++;
                }
            }
            result = result.Length > 2 ? result[..^2] : result;
            if (cnt == 0) return LocaleHelper.GetLocaleString("AllStopped");
            return string.Format(cnt > 1 ? LocaleHelper.GetLocaleString("StartedNowS") : LocaleHelper.GetLocaleString("StartedNow"), result);
        }

        private void HideWindow()
        {
            this.ShowInTaskbar = false;
            this.Visible = false;

            StartPosition = FormStartPosition.Manual;

            Location = new Point(-2000, -2000);
            Size = new Size(1, 1);
        }

        private void ShowContextMenuAt(Point location, int vertOffset, int horOffset)
        {
            TrayMenuForm = new();
            TrayMenuForm.ShowWindow(location, vertOffset, horOffset);
            TrayMenuForm.Hided += HideContextMenu;
        }

        private void HideContextMenu()
        {
            if (TrayMenuForm != null)
            {
                TrayMenuForm.Hided -= HideContextMenu;
                TrayMenuForm.Close();
                TrayMenuForm.Dispose();

                TrayMenuForm = null;
            }
        }

        private static nint LoadIcon(string name)
        {
            var icon = Utils.GetBitmapFromResourses($"CDPIUI.TrayIcon.Assets.{name}.ico");
            if (icon != null)
            {
                nint result = icon.GetHicon();
                icon.Dispose();

                return result;
            }
            return nint.Zero;
        }

        private static string GetNormalToolTip(string? toolTip)
        {
            return "CDPI UI" + (string.IsNullOrEmpty(toolTip) ? string.Empty : $"\n{toolTip}");
        }

        private void UpdateIcon(string iconName, string toolTip)
        {
            if (string.IsNullOrEmpty(iconName) || !_iconAdded) return;

            NOTIFYICONDATA data = new();

            data.cbSize = Marshal.SizeOf(data);
            data.hWnd = Handle;
            data.guidItem = IconDisplayGuid;
            data.uCallbackMessage = WM_MYMESSAGE;
            data.hIcon = LoadIcon(iconName);
            data.uFlags = NotifyFlags.NIF_ICON | NotifyFlags.NIF_GUID | NotifyFlags.NIF_MESSAGE | NotifyFlags.NIF_TIP |
                          NotifyFlags.NIF_SHOWTIP;
            data.szTip = GetNormalToolTip(toolTip);

            try
            {
                if (Shell_NotifyIcon(NotifyCommand.NIM_MODIFY, ref data) == 0)
                {
                    _iconAdded = false;
                    _iconRetryTimer.Start();
                }
            }
            finally
            {
                if (data.hIcon != IntPtr.Zero) DestroyIcon(data.hIcon);
            }

        }

        public void AddIcon(bool notify=false, string? iconName=null, string? toolTip = null)
        {
            if (IsDisposed || Disposing || _iconAdded) return;
            if (string.IsNullOrEmpty(iconName)) iconName = "trayLogoNormal";

            NOTIFYICONDATA data = new();

            data.cbSize = Marshal.SizeOf(data);
            data.hWnd = this.Handle;
            data.guidItem = IconDisplayGuid;
            data.uCallbackMessage = WM_MYMESSAGE;
            data.hIcon = LoadIcon(iconName);
            data.szTip = GetNormalToolTip(toolTip);

            data.uFlags = NotifyFlags.NIF_ICON | NotifyFlags.NIF_GUID | NotifyFlags.NIF_MESSAGE | NotifyFlags.NIF_TIP |
                          NotifyFlags.NIF_SHOWTIP;

            try
            {
                _iconAttempts++;
                var result = Shell_NotifyIcon(NotifyCommand.NIM_ADD, ref data);
                int lastError = Marshal.GetLastWin32Error();
                // TaskbarCreated also occurs on DPI changes, when the icon may still exist.
                _iconAdded = result != 0 || Shell_NotifyIcon(NotifyCommand.NIM_MODIFY, ref data) != 0;
                if (!_iconAdded)
                {
                    _iconRetryTimer.Start();
                    if (_iconAttempts == 1 || _iconAttempts % 12 == 0)
                        Logger.Instance.CreateDebugLog(nameof(EmptyForm),
                            $"Tray registration pending: attempt={_iconAttempts}, NIM_ADD={result}, " +
                            $"last-error snapshot=0x{lastError:X8} (Shell_NotifyIcon does not document GetLastError), " +
                            $"taskbarPresent={FindWindow("Shell_TrayWnd", null) != IntPtr.Zero}, hwnd=0x{Handle:X}. Will retry.");

                    if (notify && _iconAttempts == 1 && FindWindow("Shell_TrayWnd", null) != IntPtr.Zero)
                    {
                        try { NotifyHelper.Instance.ShowTrayErrorMessage($"0x{lastError:X8}"); }
                        catch (Exception ex)
                        {
                            Logger.Instance.CreateDebugLog(nameof(EmptyForm), $"Tray error notification failed: {ex}");
                        }
                    }
                    return;
                }

                _iconRetryTimer.Stop();
                Logger.Instance.CreateDebugLog(nameof(EmptyForm), $"Tray icon registered after {_iconAttempts} attempt(s).");
                _iconAttempts = 0;
                data.uVersion = NOTIFYICON_VERSION_4;
                if (Shell_NotifyIcon(NotifyCommand.NIM_SETVERSION, ref data) == 0)
                    Logger.Instance.CreateDebugLog(nameof(EmptyForm), "NIM_SETVERSION returned FALSE.");
            }
            finally
            {
                if (data.hIcon != IntPtr.Zero) DestroyIcon(data.hIcon);
            }

            data = default;
        }

        private static void DeleteIcon()
        {
            NOTIFYICONDATA data = new NOTIFYICONDATA();
            data.cbSize = Marshal.SizeOf(data);
            data.uFlags = NotifyFlags.NIF_GUID;
            data.guidItem = IconDisplayGuid;

            Shell_NotifyIcon(NotifyCommand.NIM_DELETE, ref data);
        }

        private static RECT GetRectIcon()
        {
            NOTIFYICONIDENTIFIER notifyIcon = new NOTIFYICONIDENTIFIER();

            notifyIcon.cbSize = Marshal.SizeOf(notifyIcon);

            notifyIcon.guidItem = IconDisplayGuid;
            int hresult = Shell_NotifyIconGetRect(ref notifyIcon, out RECT rect);

            return rect;
        }

        private static async void MaximizeApp()
        {
            await PipeHelper.SendOpenWindowPacket("MainWindow", true);
        }

        private void EmptyForm_Disposed(object? sender, EventArgs e)
        {
            _iconRetryTimer.Dispose();
            ConditionalLaunchEngine.Instance.Dispose();
            DeleteIcon();
            DisconnectHandlers();
            this.Disposed -= EmptyForm_Disposed;
        }


        #region MessageHandler
        static uint s_uTaskbarRestart;
        protected override void WndProc(ref Message m)
        {
            if (ConditionalLaunchEngine.Instance.HandleWindowMessage(ref m))
                return;

            if (m.Msg == WM_CREATE)
            {
                s_uTaskbarRestart = RegisterWindowMessage("TaskbarCreated");
                // Explorer runs at medium integrity; this process requires administrator rights.
                if (s_uTaskbarRestart == 0 ||
                    !ChangeWindowMessageFilterEx(m.HWnd, s_uTaskbarRestart, 1, IntPtr.Zero))
                    Logger.Instance.CreateDebugLog(nameof(EmptyForm),
                        $"Cannot allow TaskbarCreated through UIPI: Win32={Marshal.GetLastWin32Error()}. Timer retry remains available.");
            }
            else if (m.Msg == WM_MYMESSAGE)
            {
                //(Int32)m.LParam & 0x0000FFFF get the low 2 bytes of LParam, we dont need the high ones. 
                //(Int32)m.WParam & 0x0000FFFF is the X coordinate and 
                //((Int32)m.WParam & 0xFFFF0000) >> 16 the Y
                switch ((Int32)m.LParam & 0x0000FFFF)
                {
                    case NIN_BALLOONHIDE:

                        break;
                    case NIN_BALLOONSHOW:

                        break;
                    case NIN_BALLOONTIMEOUT:

                        break;
                    case NIN_BALLOONUSERCLICK:
                        //user clicked on balloon

                        break;
                    case NIN_SELECT:

                        break;
                    case WM_CONTEXTMENU:
                        var rect = GetRectIcon();
                        ShowContextMenuAt(new Point(rect.left, rect.top), rect.top - rect.bottom, rect.right - rect.left);
                        break;

                    //get what mouse messages you want
                    case WM_LBUTTONDOWN:
                        MaximizeApp();
                        break;
                    default:

                        break;
                }
            }
            else
            {
                if (s_uTaskbarRestart != 0 && m.Msg == s_uTaskbarRestart)
                {
                    Logger.Instance.CreateDebugLog(nameof(EmptyForm), "TaskbarCreated received; restoring tray icon.");
                    _iconAdded = false;
                    AddIcon(iconName: GetCurrentIcon(), toolTip: GetNowRunnedComponentsString());
                }
            }

            base.WndProc(ref m);
        }

        #endregion

        #region WinAPI

        public const Int32 WM_MYMESSAGE = 0x8000; //WM_APP
        public const Int32 NOTIFYICON_VERSION_4 = 0x4;

        //messages
        public const Int32 WM_CONTEXTMENU = 0x7B;
        public const Int32 NIN_BALLOONHIDE = 0x403;
        public const Int32 NIN_BALLOONSHOW = 0x402;
        public const Int32 NIN_BALLOONTIMEOUT = 0x404;
        public const Int32 NIN_BALLOONUSERCLICK = 0x405;
        public const Int32 NIN_KEYSELECT = 0x403;
        public const Int32 NIN_SELECT = 0x400;
        public const Int32 NIN_POPUPOPEN = 0x406;
        public const Int32 NIN_POPUPCLOSE = 0x407;
        public const Int32 WM_LBUTTONDOWN = 0x0201;
        public const Int32 WM_CREATE = 0x0001;
        public const Int32 TASKBAR_INIT_COMPLETE = 0xC0AA;

        public const Int32 NIIF_USER = 0x4;
        public const Int32 NIIF_NONE = 0x0;
        public const Int32 NIIF_INFO = 0x1;
        public const Int32 NIIF_WARNING = 0x2;
        public const Int32 NIIF_ERROR = 0x3;
        public const Int32 NIIF_LARGE_ICON = 0x20;



        public enum NotifyFlags
        {
            NIF_MESSAGE = 0x01,
            NIF_ICON = 0x02,
            NIF_TIP = 0x04,
            NIF_INFO = 0x10,
            NIF_STATE = 0x08,
            NIF_GUID = 0x20,
            NIF_SHOWTIP = 0x80
        }

        public enum NotifyCommand { NIM_ADD = 0x0, NIM_DELETE = 0x2, NIM_MODIFY = 0x1, NIM_SETVERSION = 0x4 }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public Int32 cbSize;
            public IntPtr hWnd;
            public Int32 uID;
            public NotifyFlags uFlags;
            public Int32 uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public String szTip;
            public Int32 dwState;
            public Int32 dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String szInfo;
            public Int32 uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public String szInfoTitle;
            public Int32 dwInfoFlags;
            public Guid guidItem; //> IE 6
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern System.Int32 Shell_NotifyIcon(NotifyCommand cmd, ref NOTIFYICONDATA data);

        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint RegisterWindowMessage(String lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint message, uint action, IntPtr changeInfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string? windowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);


        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public Int32 left;
            public Int32 top;
            public Int32 right;
            public Int32 bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NOTIFYICONIDENTIFIER
        {
            public Int32 cbSize;
            public IntPtr hWnd;
            public Int32 uID;
            public Guid guidItem;
        }

        //Works with Shell32.dll (version 6.1 or later)
        [DllImport("shell32.dll", SetLastError = true)]
        public static extern int Shell_NotifyIconGetRect([In] ref NOTIFYICONIDENTIFIER identifier, [Out] out RECT iconLocation);


        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,     // x-coordinate of upper-left corner
            int nTopRect,      // y-coordinate of upper-left corner
            int nRightRect,    // x-coordinate of lower-right corner
            int nBottomRect,   // y-coordinate of lower-right corner
            int nWidthEllipse, // width of ellipse
            int nHeightEllipse // height of ellipse
        );

        #endregion
    }
}
