using CDPIUI.Core;
using CDPIUI.Core.Basic;
using Microsoft.Win32;
using Microsoft.Windows.AppLifecycle;
using System;
using System.IO;
using System.Runtime.InteropServices;
using WinUI3Localizer;

namespace CDPIUI.Helper.Basic
{
    internal static class FileAssociationService
    {
        private const string AutomaticRegistrationMarkerPath =
            @"Software\CDPIUI\Registration\{B0DC091F-8A91-4EA4-AC76-ECA28C7ED986}\ConfigShare-v2";

        private static readonly Association[] Associations =
        [
            new(".cdpitask", "CDPIUI.ConditionalTask", "ConditionalTaskFileTypeDisplayName"),
            new(".cdpiconfigpack", "CDPIUI.ConfigPack", "ConfigPackFileTypeDisplayName"),
            new(".cdpiconfig", "CDPIUI.SharedConfig", "ConfigShareFileType"),
            new(".cdpisignedpack", "CDPIUI.SignedPack", "SignedPackFileTypeDisplayName"),
            new(".cdpipatch", "CDPIUI.Patch", "PatchFileTypeDisplayName")
        ];

        internal static void EnsureRegistered()
        {
            if (WasAutomaticRegistrationAttempted() || !TryCreateAutomaticRegistrationMarker())
                return;

            _ = RegisterAssociations();
        }

        internal static bool RegisterManually()
        {
            _ = TryCreateAutomaticRegistrationMarker();
            return RegisterAssociations();
        }

        private static bool RegisterAssociations()
        {
            var executablePath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(executablePath))
                return false;

            var localizer = Localizer.Get();
            var applicationDisplayName = string.Format(
                localizer.GetLocalizedString("FileAssociationApplicationNameFormat"),
                ApplicationInfo.Version);
            var executableIcon = $"{executablePath},0";

            var protocolRegistered = RegisterProtocolActivation(
                executablePath,
                executableIcon,
                applicationDisplayName);
            var fileActivationRegistered = RegisterFileActivationHandlers(
                executablePath,
                executableIcon,
                localizer);
            var classicAssociationsRegistered = RegisterClassicAssociations(
                executablePath,
                applicationDisplayName,
                localizer);

            return protocolRegistered &&
                fileActivationRegistered &&
                classicAssociationsRegistered;
        }

        private static bool RegisterProtocolActivation(
            string executablePath,
            string executableIcon,
            string applicationDisplayName)
        {
            try
            {
                ActivationRegistrationManager.RegisterForProtocolActivation(
                    "cdpiui",
                    executableIcon,
                    applicationDisplayName,
                    executablePath);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(FileAssociationService),
                    $"Cannot register protocol activation: {ex.Message}");
                return false;
            }
        }

        private static bool RegisterFileActivationHandlers(
            string executablePath,
            string executableIcon,
            ILocalizer localizer)
        {
            try
            {
                foreach (var association in Associations)
                {
                    ActivationRegistrationManager.RegisterForFileTypeActivation(
                        [association.Extension],
                        executableIcon,
                        localizer.GetLocalizedString(association.DisplayNameResource),
                        [],
                        executablePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(FileAssociationService),
                    $"Cannot register file activation: {ex.Message}");
                return false;
            }
        }

        private static bool RegisterClassicAssociations(
            string executablePath,
            string applicationDisplayName,
            ILocalizer localizer)
        {
            try
            {
                var executableName = Path.GetFileName(executablePath);
                var command = $"\"{executablePath}\" \"%1\"";
                var icon = $"\"{executablePath}\",0";

                SetValue($@"Software\Classes\Applications\{executableName}", "FriendlyAppName", applicationDisplayName);
                SetValue($@"Software\Classes\Applications\{executableName}", "ApplicationIcon", icon);
                SetValue($@"Software\Classes\Applications\{executableName}\DefaultIcon", null, icon);
                SetValue($@"Software\Classes\Applications\{executableName}\shell\open\command", null, command);

                using var supportedTypesKey = Registry.CurrentUser.CreateSubKey(
                    $@"Software\Classes\Applications\{executableName}\SupportedTypes");
                using var capabilitiesKey = Registry.CurrentUser.CreateSubKey(
                    @"Software\CDPIUI\Capabilities");
                capabilitiesKey?.SetValue("ApplicationName", applicationDisplayName);
                capabilitiesKey?.SetValue("ApplicationDescription", "CDPI UI");
                capabilitiesKey?.SetValue("ApplicationIcon", icon);
                using var capabilityAssociationsKey = Registry.CurrentUser.CreateSubKey(
                    @"Software\CDPIUI\Capabilities\FileAssociations");

                foreach (var association in Associations)
                {
                    var displayName = localizer.GetLocalizedString(association.DisplayNameResource);
                    SetValue($@"Software\Classes\{association.Extension}", null, association.ProgId);
                    SetValue($@"Software\Classes\{association.ProgId}", null, displayName);
                    SetValue($@"Software\Classes\{association.ProgId}", "FriendlyTypeName", displayName);
                    SetValue($@"Software\Classes\{association.ProgId}\DefaultIcon", null, icon);
                    SetValue($@"Software\Classes\{association.ProgId}\shell\open\command", null, command);

                    using (var openWithKey = Registry.CurrentUser.CreateSubKey(
                        $@"Software\Classes\{association.Extension}\OpenWithProgids"))
                    {
                        openWithKey?.SetValue(
                            association.ProgId,
                            Array.Empty<byte>(),
                            RegistryValueKind.None);
                    }

                    supportedTypesKey?.SetValue(
                        association.Extension,
                        Array.Empty<byte>(),
                        RegistryValueKind.None);
                    capabilityAssociationsKey?.SetValue(
                        association.Extension,
                        association.ProgId);
                }

                SetValue(
                    @"Software\RegisteredApplications",
                    "CDPI UI",
                    @"Software\CDPIUI\Capabilities");
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(FileAssociationService),
                    $"Cannot register classic file associations: {ex.Message}");
                return false;
            }
        }

        private static bool WasAutomaticRegistrationAttempted()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(AutomaticRegistrationMarkerPath);
                return key is not null;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(FileAssociationService),
                    $"Cannot read the automatic association registration marker: {ex.Message}");
                return false;
            }
        }

        private static bool TryCreateAutomaticRegistrationMarker()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(AutomaticRegistrationMarkerPath);
                return key is not null;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(FileAssociationService),
                    $"Cannot create the automatic association registration marker: {ex.Message}");
                return false;
            }
        }

        private static void SetValue(string keyPath, string? valueName, object value)
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            key?.SetValue(valueName, value);
        }

        private sealed record Association(
            string Extension,
            string ProgId,
            string DisplayNameResource);

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(
            uint wEventId,
            uint uFlags,
            IntPtr dwItem1,
            IntPtr dwItem2);
    }
}
