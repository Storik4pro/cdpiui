using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI_TrayIcon.Helper
{
    public static class RegeditHelper
    {
        public const string InternetSettingsKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        public static Dictionary<string, string> ReadProxySettings()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProxyServer"] = string.Empty,
                ["ProxyOverride"] = string.Empty
            };

            using (var key = Registry.CurrentUser.OpenSubKey(InternetSettingsKeyPath, writable: false))
            {
                if (key != null)
                {
                    var proxyServer = key.GetValue("ProxyServer") as string ?? string.Empty;
                    var proxyOverride = key.GetValue("ProxyOverride") as string ?? string.Empty;

                    result["ProxyServer"] = proxyServer;
                    result["ProxyOverride"] = proxyOverride;
                }
            }

            return result;
        }

        public static void SaveProxySettings(string proxyServer, string proxyOverride, int proxyEnable)
        {
            if (proxyEnable != 0 && proxyEnable != 1)
                throw new ArgumentOutOfRangeException(nameof(proxyEnable), "ERR_REGEDIT_HELPER_INTERNAL");

            using (var key = Registry.CurrentUser.CreateSubKey(InternetSettingsKeyPath))
            {
                if (key == null)
                    throw new InvalidOperationException("ERR_REGISTRY_WRITE");

                if (!string.IsNullOrEmpty(proxyServer))
                    key.SetValue("ProxyServer", proxyServer ?? string.Empty, RegistryValueKind.String);

                if (!string.IsNullOrEmpty(proxyOverride))
                    key.SetValue("ProxyOverride", proxyOverride ?? string.Empty, RegistryValueKind.String);

                key.SetValue("ProxyEnable", proxyEnable, RegistryValueKind.DWord);
            }

            if (!InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ERR_WININET_CALL");
            }

            if (!InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ERR_WININET_CALL");
            }
        }
    }
}
