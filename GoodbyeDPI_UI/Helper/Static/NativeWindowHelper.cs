using Microsoft.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;

namespace CDPI_UI.Helper.Static
{
    /// <summary>
    /// This code copied from https://github.com/microsoft/microsoft-ui-xaml/issues/9427#issuecomment-2504707196.
    /// Author is Yui Sayou
    /// </summary>
    public static class NativeWindowHelper
    {
        private const int WM_NCLBUTTONDBLCLK = 0x00A3; // Non-client left button double-click
        private const int WM_SYSCOMMAND = 0x0112; // System command message
        private const int SC_MAXIMIZE = 0xF030; // Maximize command
        private const int WM_SIZE = 0x0005; // Resize message
        private const int SIZE_MAXIMIZED = 2; // Maximized size
        private const int WM_NCDESTROY = 0x0082;
        private const int GWLP_WNDPROC = -4;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        // Static field to hold the delegate, preventing it from being garbage-collected
        private static WndProcDelegate _currentWndProcDelegate;


        // Delegate for the new window procedure
        private static readonly ConcurrentDictionary<IntPtr, IntPtr> _originalProcs = new();
        private static readonly ConcurrentDictionary<IntPtr, WndProcDelegate> _wndProcDelegates = new();

        public static void ForceDisableMaximize(Window window)
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            if (hwnd == IntPtr.Zero) return;

            if (_originalProcs.ContainsKey(hwnd)) return;

            IntPtr original = GetWindowLongPtr(hwnd, GWLP_WNDPROC);
            if (original == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Failed to get original WndProc.");
                return;
            }

            WndProcDelegate del = null!;
            del = (wndHwnd, msg, wParam, lParam) =>
            {
                if (msg == WM_NCDESTROY)
                {
                    RemoveHook(wndHwnd, original);
                    return CallWindowProc(original, wndHwnd, msg, wParam, lParam);
                }

                if (msg == WM_NCLBUTTONDBLCLK)
                {
                    System.Diagnostics.Debug.WriteLine("Double-click maximize suppressed.");
                    return IntPtr.Zero;
                }

                if (msg == WM_SYSCOMMAND && wParam.ToInt32() == SC_MAXIMIZE)
                {
                    System.Diagnostics.Debug.WriteLine("Maximize via system command suppressed.");
                    return IntPtr.Zero;
                }

                return CallWindowProc(original, wndHwnd, msg, wParam, lParam);
            };

            _originalProcs[hwnd] = original;
            _wndProcDelegates[hwnd] = del;

            IntPtr newPtr = Marshal.GetFunctionPointerForDelegate(del);
            IntPtr prev = SetWindowLongPtr(hwnd, GWLP_WNDPROC, newPtr);
            if (prev == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"SetWindowLongPtr failed: {err}");
                _originalProcs.TryRemove(hwnd, out _);
                _wndProcDelegates.TryRemove(hwnd, out _);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WndProc hooked successfully.");
            }
        }

        private static void RemoveHook(IntPtr hwnd, IntPtr originalProc)
        {
            try
            {
                IntPtr prev = SetWindowLongPtr(hwnd, GWLP_WNDPROC, originalProc);
                if (prev == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    System.Diagnostics.Debug.WriteLine($"Restore SetWindowLongPtr failed: {err}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring WndProc: {ex.Message}");
            }
            finally
            {
                _originalProcs.TryRemove(hwnd, out _);
                _wndProcDelegates.TryRemove(hwnd, out _);
            }
        }

        // Win32 API declarations
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);
        }
    }
}
