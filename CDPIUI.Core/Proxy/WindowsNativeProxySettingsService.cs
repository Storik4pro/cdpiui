using CDPIUI.Core.Basic;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CDPIUI.Core.Proxy
{
    public static class WindowsNativeProxySettingsService
    {
        public const string InternetSettingsKeyPath = 
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(
            nint hInternet, 
            int dwOption, 
            nint lpBuffer, 
            int dwBufferLength);

        public static Dictionary<string, string> ReadProxySettings()
        {
            if (!OperatingSystem.IsWindows()) throw new NotSupportedException();

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProxyServer"] = string.Empty,
                ["ProxyOverride"] = string.Empty
            };

            using (var key = 
                Registry.CurrentUser.OpenSubKey(InternetSettingsKeyPath, writable: false))
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

        public static bool IsProxyEnabled()
        {
            try
            {
                if (!OperatingSystem.IsWindows()) throw new NotSupportedException();

                int? isProxyEnable = new();
                using (var key = 
                    Registry.CurrentUser.OpenSubKey(InternetSettingsKeyPath, writable: false))
                {
                    if (key != null)
                    {
                        isProxyEnable = (int?)key.GetValue("ProxyEnable", new int?());
                    }
                }
                return isProxyEnable.HasValue && Convert.ToBoolean(isProxyEnable.Value);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(WindowsNativeProxySettingsService),
                    $"Cannot get reg value. {ex.Message}");
                return false;
            }
        }

        public static void SaveProxySettings(
            string proxyServer, 
            string proxyOverride, 
            int proxyEnable)
        {
            if (!OperatingSystem.IsWindows()) throw new NotSupportedException();

            if (proxyEnable != 0 && proxyEnable != 1)
                throw new ArgumentOutOfRangeException(nameof(proxyEnable), 
                    "ERR_REGEDIT_HELPER_INTERNAL");

            using (var key = Registry.CurrentUser.CreateSubKey(InternetSettingsKeyPath))
            {
                if (key == null)
                    throw new InvalidOperationException("ERR_REGISTRY_WRITE");

                key.SetValue("ProxyServer", proxyServer ?? string.Empty, RegistryValueKind.String);
                key.SetValue("ProxyOverride", proxyOverride ?? string.Empty, RegistryValueKind.String);
                key.SetValue("ProxyEnable", proxyEnable, RegistryValueKind.DWord);
            }

            if (!InternetSetOption(nint.Zero, INTERNET_OPTION_SETTINGS_CHANGED, nint.Zero, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ERR_WININET_CALL");
            }

            if (!InternetSetOption(nint.Zero, INTERNET_OPTION_REFRESH, nint.Zero, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ERR_WININET_CALL");
            }
        }
    }
}
