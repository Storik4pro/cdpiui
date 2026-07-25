using Microsoft.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace CDPIUI.Helper.Native
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

        private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

        private static readonly ConcurrentDictionary<nint, nint> _originalProcs = new();
        private static readonly ConcurrentDictionary<nint, WndProcDelegate> _wndProcDelegates = new();

        public static void ForceDisableMaximize(Window window)
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            if (hwnd == nint.Zero) return;

            if (_originalProcs.ContainsKey(hwnd)) return;

            nint original = GetWindowLongPtr(hwnd, GWLP_WNDPROC);
            if (original == nint.Zero)
            {
                Debug.WriteLine("Failed to get original WndProc.");
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
                    Debug.WriteLine("Double-click maximize suppressed.");
                    return nint.Zero;
                }

                if (msg == WM_SYSCOMMAND && wParam.ToInt32() == SC_MAXIMIZE)
                {
                    Debug.WriteLine("Maximize via system command suppressed.");
                    return nint.Zero;
                }

                return CallWindowProc(original, wndHwnd, msg, wParam, lParam);
            };

            _originalProcs[hwnd] = original;
            _wndProcDelegates[hwnd] = del;

            nint newPtr = Marshal.GetFunctionPointerForDelegate(del);
            nint prev = SetWindowLongPtr(hwnd, GWLP_WNDPROC, newPtr);
            if (prev == nint.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Debug.WriteLine($"SetWindowLongPtr failed: {err}");
                _originalProcs.TryRemove(hwnd, out _);
                _wndProcDelegates.TryRemove(hwnd, out _);
            }
            else
            {
                Debug.WriteLine("WndProc hooked successfully.");
            }
        }

        private static void RemoveHook(nint hwnd, nint originalProc)
        {
            try
            {
                nint prev = SetWindowLongPtr(hwnd, GWLP_WNDPROC, originalProc);
                if (prev == nint.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"Restore SetWindowLongPtr failed: {err}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restoring WndProc: {ex.Message}");
            }
            finally
            {
                _originalProcs.TryRemove(hwnd, out _);
                _wndProcDelegates.TryRemove(hwnd, out _);
            }
        }

        // Win32 API declarations
        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint Msg, nint wParam, nint lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern nint GetWindowLong32(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern nint SetWindowLong32(nint hWnd, int nIndex, nint dwNewLong);

        private static nint GetWindowLongPtr(nint hWnd, int nIndex)
        {
            return nint.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
        }

        private static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong)
        {
            return nint.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);
        }
    }
}
