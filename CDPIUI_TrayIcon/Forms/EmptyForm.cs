using CDPIUI_TrayIcon.Helper;
using CDPIUI_TrayIcon.Helper.Basic;
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

namespace CDPIUI_TrayIcon.Forms
{
    // Used for creating tray menu handler only.

    public partial class EmptyForm : Form
    {
        private static readonly Guid IconDisplayGuid = GetGuid();
        private static StringBuilder ErrorMessage = new(
            "Application can't create icon in system notification aera during unexpected exception. " +
            "You can find solution of this problem on https://storik4pro.github.io/en-US/cdpiui/wiki/Other/TrayIconNotShown/ or application internal help. \n" +
            "\tHere some additional information about this problem: \n" +
            "\t\tApplication version: {0}\n" +
            "\t\tOperation: {1}\n" +
            "\t\tOperation result: {2}\n" +
            "\t\tException code: {3}\n" +
            "\tContact official support if this error persist.");

        private TrayMenuForm? TrayMenuForm;

        public EmptyForm()
        {
            InitializeComponent();

            HideWindow();

            Application.ApplicationExit += Application_ApplicationExit;
            this.Disposed += EmptyForm_Disposed;
            this.Load += EmptyForm_Load;

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

        private void EmptyForm_Load(object? sender, EventArgs e)
        {
            AddIcon();
        }

        private void ConnectHandlers()
        {
            TasksHelper.Instance.TaskStateUpdated += HandleTaskStateUpdate;
            TasksHelper.Instance.TaskListUpdated += HandleTasksListUpdate;
            
        }

        private void DisconnectHandlers()
        {
            TasksHelper.Instance.TaskStateUpdated -= HandleTaskStateUpdate;
            TasksHelper.Instance.TaskListUpdated -= HandleTasksListUpdate;
        }

        private string GetCurrentIcon()
        {
            int rt = 0;

            foreach (var task in TasksHelper.Instance.Tasks)
            {
                if (task.ProcessManager.GetState())
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
                if (task.ProcessManager.GetState())
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
            var icon = Utils.GetBitmapFromResourses($"CDPIUI_TrayIcon.Assets.{name}.ico");
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

        private static void UpdateIcon(string iconName, string toolTip)
        {
            if (string.IsNullOrEmpty(iconName)) return;

            NOTIFYICONDATA data = new();

            data.cbSize = Marshal.SizeOf(data);
            data.guidItem = IconDisplayGuid;
            data.uCallbackMessage = WM_MYMESSAGE;
            data.hIcon = LoadIcon(iconName);
            data.uFlags = NotifyFlags.NIF_ICON | NotifyFlags.NIF_GUID | NotifyFlags.NIF_MESSAGE | NotifyFlags.NIF_TIP |
                          NotifyFlags.NIF_SHOWTIP;
            data.szTip = GetNormalToolTip(toolTip);

            var result = Shell_NotifyIcon(NotifyCommand.NIM_MODIFY, ref data);

        }

        public void AddIcon(bool notify=false, string? iconName=null, string? toolTip = null)
        {
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

            var result = Shell_NotifyIcon(NotifyCommand.NIM_ADD, ref data);

            if (result == 0 && notify)
            {
                string error = $"0x{(uint)Marshal.GetLastWin32Error():X8}";
                Logger.Instance.CreateErrorLog(
                    nameof(EmptyForm), string.Format(ErrorMessage.ToString(), "NaN", nameof(AddIcon), result, error)
                    );
                NotifyHelper.Instance.ShowTrayErrorMessage(error);
                error = null;
            }

            data.uVersion = NOTIFYICON_VERSION_4;
            Shell_NotifyIcon(NotifyCommand.NIM_SETVERSION, ref data);

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
            if (!await PipeServer.Instance.SendMessage("WINDOW:SHOW_MAIN"))
            {
                RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), string.Empty);
            }
        }

        private void EmptyForm_Disposed(object? sender, EventArgs e)
        {
            DeleteIcon();
            DisconnectHandlers();
            this.Disposed -= EmptyForm_Disposed;
        }


        #region MessageHandler
        static uint s_uTaskbarRestart;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CREATE)
            {
                s_uTaskbarRestart = RegisterWindowMessage("TaskbarCreated");
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
                if (m.Msg == s_uTaskbarRestart)
                    AddIcon(notify: true, iconName: GetCurrentIcon(), toolTip: GetNowRunnedComponentsString());
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
        [StructLayout(LayoutKind.Sequential)]
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

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern System.Int32 Shell_NotifyIcon(NotifyCommand cmd, ref NOTIFYICONDATA data);

        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint RegisterWindowMessage(String lpString);


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
