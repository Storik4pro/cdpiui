using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static CDPI_UI.App;

namespace CDPI_UI
{
    internal static class Win32
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(IntPtr moduleName);

        [DllImport("User32.dll")]
        internal static extern int GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        internal static extern int SetWindowLong32(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        internal static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, WindowMessage Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern int CallNextHookEx(int idHook, int nCode, IntPtr wParam, IntPtr lParam);


        public static int SetWindowKeyHook(HookProc hookProc)
        {
            return SetWindowsHookEx(WH_KEYBOARD, hookProc, GetModuleHandle(IntPtr.Zero), (int)GetCurrentThreadId());
        }

        public static bool IsKeyDownHook(IntPtr lWord)
        {
            // The 30th bit tells what the previous key state is with 0 being the "UP" state
            // For more info see https://learn.microsoft.com/windows/win32/winmsg/keyboardproc#lparam-in
            return (lWord >> 30 & 1) == 0;
        }

        public const int WM_ACTIVATE = 0x0006;
        public const int WA_ACTIVE = 0x01;
        public const int WA_INACTIVE = 0x00;
        public const int WH_KEYBOARD = 2;
        public const int WM_KEYDOWN = 0x0104;

        public const int WM_SETICON = 0x0080;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        internal delegate IntPtr WinProc(IntPtr hWnd, WindowMessage Msg, IntPtr wParam, IntPtr lParam);
        public delegate int HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [Flags]
        internal enum WindowLongIndexFlags : int
        {
            GWL_WNDPROC = -4,
        }

        internal enum WindowMessage : int
        {
            WM_GETMINMAXINFO = 0x0024,
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

        public static ulong GetDiskFreeSpace(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            ulong dummy = 0;

            if (!GetDiskFreeSpaceEx(path, out ulong freeSpace, out dummy, out dummy))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return freeSpace;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi")]
        public static extern IntPtr DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
    }
}
