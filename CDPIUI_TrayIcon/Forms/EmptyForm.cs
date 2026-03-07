using CDPIUI_TrayIcon.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static CDPIUI_TrayIcon.Forms.TrayMenuForm;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace CDPIUI_TrayIcon.Forms
{
    // Used for creating tray menu handler only.

    public partial class EmptyForm : Form
    {
        private static readonly Guid IconDisplayGuid = new("D6AF8980-885B-453C-908C-DD79AC1F2AB2");

        private TrayMenuForm? TrayMenuForm;

        private readonly CancellationTokenSource? CancellationTokenSource;

        public EmptyForm()
        {
            InitializeComponent();

            CancellationTokenSource = new();

            HideWindow();

            this.Disposed += EmptyForm_Disposed;
            this.Load += EmptyForm_Load;

            ConnectHandlers();
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

        private void HandleTaskStateUpdate(Tuple<string, bool> taskStateUpdate)
        {
            if (taskStateUpdate.Item2)
            {
                if (IsAnyTaskHasState(!taskStateUpdate.Item2))
                {
                    UpdateIcon("trayLogoStartedNotAll", GetNowRunnedComponentsString());
                }
                else
                {
                    UpdateIcon("trayLogoStarted", LocaleHelper.GetLocaleString("StartedAll"));
                }
            }
            else
            {
                if (IsAnyTaskHasState(!taskStateUpdate.Item2))
                {
                    UpdateIcon("trayLogoStartedNotAll", GetNowRunnedComponentsString());
                }
                else
                {
                    UpdateIcon("trayLogoStopped", LocaleHelper.GetLocaleString("AllStopped"));
                }
            }
        }

        private void HandleTasksListUpdate()
        {

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
            result = result[..^2];
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

        private void ShowContextMenuAt(Point location)
        {
            TrayMenuForm = new();
            TrayMenuForm.ShowWindow(location);
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

        public async void BeginIconVisibilityCheck()
        {
            while (CancellationTokenSource!= null && !CancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                if (!TryShowIcon())
                {
                    BeginIconVisibilityCheck();
                }
                else
                {
                    UpdateIcon("trayLogoNormal", string.Empty);
                    CancellationTokenSource.Cancel();
                }
            }
            
        }

        public bool TryShowIcon()
        {
            RECT rect = GetRectIcon();
            if (rect.top == 0 && rect.left == 0 && rect.right == 0 && rect.bottom == 0)
            {
                DeleteIcon();
                AddIcon(showIcon:false);
                return false;
            }
            else
            {
                return true;
            }
        }

        private static void UpdateIcon(string iconName, string toolTip)
        {
            if (string.IsNullOrEmpty(toolTip)) toolTip = "CDPI UI";
            else toolTip = $"CDPI UI\n{toolTip}";
            if (string.IsNullOrEmpty(iconName)) return;

            NOTIFYICONDATA data = new();

            data.cbSize = Marshal.SizeOf(data);
            data.guidItem = IconDisplayGuid;
            data.uCallbackMessage = WM_MYMESSAGE;
            data.hIcon = LoadIcon(iconName);
            data.uFlags = NotifyFlags.NIF_ICON | NotifyFlags.NIF_GUID | NotifyFlags.NIF_MESSAGE | NotifyFlags.NIF_TIP |
                          NotifyFlags.NIF_SHOWTIP;
            data.szTip = toolTip;

            var result = Shell_NotifyIcon(NotifyCommand.NIM_MODIFY, ref data);

            Debug.WriteLine($"RESULT {result}");
        }

        public void AddIcon(bool showIcon=true)
        {
            NOTIFYICONDATA data = new();

            data.cbSize = Marshal.SizeOf(data);
            data.hWnd = this.Handle;
            data.guidItem = IconDisplayGuid;
            data.uCallbackMessage = WM_MYMESSAGE;
            if (showIcon) data.hIcon = LoadIcon("trayLogoNormal");
            data.szTip = "CDPI UI";

            data.uFlags = NotifyFlags.NIF_ICON | NotifyFlags.NIF_GUID | NotifyFlags.NIF_MESSAGE | NotifyFlags.NIF_TIP |
                          NotifyFlags.NIF_SHOWTIP;

            Shell_NotifyIcon(NotifyCommand.NIM_ADD, ref data);

            data.uVersion = NOTIFYICON_VERSION_4;
            Shell_NotifyIcon(NotifyCommand.NIM_SETVERSION, ref data);
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
            CancellationTokenSource?.Cancel();
            DeleteIcon();
            DisconnectHandlers();
            this.Disposed -= EmptyForm_Disposed;
        }


        #region MessageHandler

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MYMESSAGE)
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
                        ShowContextMenuAt(new Point(rect.left, rect.top));
                        break;

                    //get what mouse messages you want
                    case WM_LBUTTONDOWN:
                        MaximizeApp();
                        break;
                    default:

                        break;
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

        [DllImport("shell32.dll")]
        public static extern System.Int32 Shell_NotifyIcon(NotifyCommand cmd, ref NOTIFYICONDATA data);


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
