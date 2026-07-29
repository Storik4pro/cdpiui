#nullable enable

using CDPIUI.Core;
using CDPIUI.Core.Basic;
using Microsoft.Win32;
using Microsoft.Windows.AppLifecycle;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WinUI3Localizer;

namespace CDPIUI.Helper.Basic
{
    internal static class FileAssociationService
    {
        private const string RegistrationStatePath = @"Software\CDPIUI";
        private const string RegistrationSignatureValue = "ActivationRegistrationSignature";

        private static readonly Association[] Associations =
        [
            new(".cdpitask", "CDPIUI.ConditionalTask", "ConditionalTaskFileTypeDisplayName"),
            new(".cdpiconfigpack", "CDPIUI.ConfigPack", "ConfigPackFileTypeDisplayName"),
            new(".cdpisignedpack", "CDPIUI.SignedPack", "SignedPackFileTypeDisplayName"),
            new(".cdpipatch", "CDPIUI.Patch", "PatchFileTypeDisplayName")
        ];

        internal static void EnsureRegistered()
        {
            var executablePath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(executablePath))
                return;

            var localizer = Localizer.Get();
            var applicationDisplayName = string.Format(
                localizer.GetLocalizedString("FileAssociationApplicationNameFormat"),
                ApplicationInfo.Version);
            var registrationSignature = CreateRegistrationSignature(
                executablePath,
                applicationDisplayName,
                localizer);

            if (IsRegistrationCurrent(
                executablePath,
                applicationDisplayName,
                registrationSignature,
                localizer))
            {
                return;
            }

            var executableIcon = $"{executablePath},0";
            var activationRegistered = RegisterActivationHandlers(
                executablePath,
                executableIcon,
                applicationDisplayName,
                localizer);
            var classicAssociationsRegistered = RegisterClassicAssociations(
                executablePath,
                applicationDisplayName,
                localizer);

            if (activationRegistered && classicAssociationsRegistered)
            {
                try
                {
                    SetValue(
                        RegistrationStatePath,
                        RegistrationSignatureValue,
                        registrationSignature);
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateWarningLog(
                        nameof(FileAssociationService),
                        $"Cannot save activation registration state: {ex.Message}");
                }
            }
        }

        private static bool RegisterActivationHandlers(
            string executablePath,
            string executableIcon,
            string applicationDisplayName,
            ILocalizer localizer)
        {
            try
            {
                ActivationRegistrationManager.RegisterForProtocolActivation(
                    "cdpiui",
                    executableIcon,
                    applicationDisplayName,
                    executablePath);

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
                    $"Cannot register application activation: {ex.Message}");
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

        private static bool IsRegistrationCurrent(
            string executablePath,
            string applicationDisplayName,
            string registrationSignature,
            ILocalizer localizer)
        {
            try
            {
                var executableName = Path.GetFileName(executablePath);
                var command = $"\"{executablePath}\" \"%1\"";
                var icon = $"\"{executablePath}\",0";
                var applicationPath = $@"Software\Classes\Applications\{executableName}";

                if (!ValueEquals(
                        RegistrationStatePath,
                        RegistrationSignatureValue,
                        registrationSignature) ||
                    !ValueEquals(applicationPath, "FriendlyAppName", applicationDisplayName) ||
                    !ValueEquals(applicationPath, "ApplicationIcon", icon) ||
                    !ValueEquals($@"{applicationPath}\DefaultIcon", null, icon) ||
                    !ValueEquals($@"{applicationPath}\shell\open\command", null, command) ||
                    !IsProtocolRegistrationPresent(executablePath) ||
                    !ValueEquals(
                        @"Software\CDPIUI\Capabilities",
                        "ApplicationName",
                        applicationDisplayName) ||
                    !ValueEquals(
                        @"Software\CDPIUI\Capabilities",
                        "ApplicationDescription",
                        "CDPI UI") ||
                    !ValueEquals(
                        @"Software\CDPIUI\Capabilities",
                        "ApplicationIcon",
                        icon) ||
                    !ValueEquals(
                        @"Software\RegisteredApplications",
                        "CDPI UI",
                        @"Software\CDPIUI\Capabilities"))
                {
                    return false;
                }

                foreach (var association in Associations)
                {
                    var displayName = localizer.GetLocalizedString(association.DisplayNameResource);
                    if (!ValueEquals(
                            $@"Software\Classes\{association.Extension}",
                            null,
                            association.ProgId) ||
                        !ValueEquals(
                            $@"Software\Classes\{association.ProgId}",
                            null,
                            displayName) ||
                        !ValueEquals(
                            $@"Software\Classes\{association.ProgId}",
                            "FriendlyTypeName",
                            displayName) ||
                        !ValueEquals(
                            $@"Software\Classes\{association.ProgId}\DefaultIcon",
                            null,
                            icon) ||
                        !ValueEquals(
                            $@"Software\Classes\{association.ProgId}\shell\open\command",
                            null,
                            command) ||
                        !ValueEquals(
                            @"Software\CDPIUI\Capabilities\FileAssociations",
                            association.Extension,
                            association.ProgId) ||
                        !ValueExists(
                            $@"Software\Classes\{association.Extension}\OpenWithProgids",
                            association.ProgId) ||
                        !ValueExists(
                            $@"{applicationPath}\SupportedTypes",
                            association.Extension))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ValueEquals(string keyPath, string? valueName, string expected)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return string.Equals(
                key?.GetValue(
                    valueName,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString(),
                expected,
                StringComparison.Ordinal);
        }

        private static bool ValueExists(string keyPath, string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase) == true;
        }

        private static bool IsProtocolRegistrationPresent(string executablePath)
        {
            using var protocolKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\cdpiui");
            using var commandKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\cdpiui\shell\open\command");
            var command = commandKey?.GetValue(null)?.ToString();

            return protocolKey?.GetValueNames().Contains(
                    "URL Protocol",
                    StringComparer.OrdinalIgnoreCase) == true &&
                command?.Contains(executablePath, StringComparison.OrdinalIgnoreCase) == true;
        }

        private static string CreateRegistrationSignature(
            string executablePath,
            string applicationDisplayName,
            ILocalizer localizer) =>
            string.Join(
                "|",
                new[] { "1", executablePath, applicationDisplayName }
                    .Concat(Associations.Select(association =>
                        localizer.GetLocalizedString(association.DisplayNameResource))));

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
