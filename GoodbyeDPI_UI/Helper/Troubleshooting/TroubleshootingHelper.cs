using CDPI_UI.Helper.Static;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Xml.Linq;
using WinUI3Localizer;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using Task = System.Threading.Tasks.Task;

namespace CDPI_UI.Helper.Troubleshooting
{
    public class TroubleshootingHelper
    {
        private static TroubleshootingHelper _instance;
        private static readonly object _lock = new object();

        public static TroubleshootingHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new TroubleshootingHelper();
                    return _instance;
                }
            }
        }

        public Action<RunBasicDialogStates> BasicDialogStateChanged;

        private TroubleshootingHelper()
        {

        }

        public enum RunBasicDialogStates
        {
            Preparing,
            CheckBFE,
            CheckProxy,
            CheckNetsh,
            CheckTimestamps,
            CheckAdGuard,
            CheckKiller,
            CheckIntelConnectivityNetwork,
            CheckCheckPointServices,
            CheckSmartByte,
            CheckVPNs,
            CheckDNS,
            CheckWinDivert,
            CheckAnotherComponents,
            CheckAnotherComponentsServices,
            Completed
        }
        public class DiagnosticStateModel
        {
            public string CorrectValueDisplayText { get; set; }
            public string InCorrectValueDisplayText { get; set; }
            public bool CorrectState { get; set; }
        }

        public static Dictionary<RunBasicDialogStates, DiagnosticStateModel> BasicDiagnosticStateModelPairs = new()
        {
            { RunBasicDialogStates.CheckBFE, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "ServiceRunned", InCorrectValueDisplayText="ServiceStopped" } },
            { RunBasicDialogStates.CheckProxy, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "Disabled", InCorrectValueDisplayText="Enabled" } },
            { RunBasicDialogStates.CheckNetsh, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Exist", InCorrectValueDisplayText="NotExist" } },
            { RunBasicDialogStates.CheckTimestamps, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Exist", InCorrectValueDisplayText="NotExist" } },
            { RunBasicDialogStates.CheckAdGuard, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunned" } },
            { RunBasicDialogStates.CheckKiller, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunned" } },
            { RunBasicDialogStates.CheckIntelConnectivityNetwork, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunned" } },
            { RunBasicDialogStates.CheckCheckPointServices, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunned" } },
            { RunBasicDialogStates.CheckSmartByte, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunned" } },
            { RunBasicDialogStates.CheckVPNs, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunned" } },
            { RunBasicDialogStates.CheckDNS, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "ServiceRunned", InCorrectValueDisplayText="ServiceStopped" } },
            { RunBasicDialogStates.CheckWinDivert, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunned" } },
            { RunBasicDialogStates.CheckAnotherComponents, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunned" } },
            { RunBasicDialogStates.CheckAnotherComponentsServices, new DiagnosticStateModel() { CorrectState = false, CorrectValueDisplayText = "ServiceStopped", InCorrectValueDisplayText="ServiceRunned" } },
        };

        public async Task<Dictionary<RunBasicDialogStates, bool>> RunBasicDiagnostic()
        {
            TasksHelper.Instance.StopAllTasks();
            _ = ProcessManager.StopService();
            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.Preparing);

            await Task.Delay(1000);
            Services = null;
            Dictionary<RunBasicDialogStates, bool> completedStates = [];

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckBFE);
            bool isRunningBFE = IsServiceRunning("BFE");

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckProxy);
            bool isProxyEnabled = RegeditHelper.IsProxyEnabled();

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckNetsh);
            bool isNetshExistInPATH = ExistsOnPath("netsh.exe");

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckTimestamps);
            bool isTimestampsChecked = CheckTimestamps();

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckAdGuard);
            bool isAdGuardRunning = IsProcessRunning("adguardvpnsvc") || IsProcessRunning("adguardvpn") || IsProcessRunning("adguardsvc");
            await Task.Delay(1000);

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckKiller);
            bool isRunningKiller = IsServiceRunning("Killer");

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckIntelConnectivityNetwork);
            bool isRunningIntelConnectivityNetwork = IsServiceRunning("Intel") || IsServiceRunning("Connectivity") || IsServiceRunning("Network");

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckCheckPointServices);
            bool isCheckPointServiceRunning = IsServiceRunning("TracSrvWrapper") || IsServiceRunning("EPWD");
            await Task.Delay(1000);

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckSmartByte);
            bool isSmartByteRunning = IsServiceRunning("SmartByte");

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckVPNs);
            bool isRunningVPN = IsServiceExist("vpn");

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckDNS);
            bool isUsedDNS = GetDnsAdress();

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckWinDivert);
            bool isWinDivertRunning = IsServiceRunning("windivert");

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckAnotherComponentsServices);
            bool isAnotherServiceComponentRunned = IsServiceExist("GoodbyeDPI") || IsServiceExist("discordfix_zapret") || IsServiceExist("winws1") || IsServiceExist("winws2");
            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.CheckAnotherComponents);
            bool isAnotherComponentRunned = IsProcessRunning("winws") || IsProcessRunning("goodbyedpi");
            await Task.Delay(1000);

            completedStates = new()
            {
                { RunBasicDialogStates.CheckBFE, isRunningBFE },
                { RunBasicDialogStates.CheckProxy, isProxyEnabled },
                { RunBasicDialogStates.CheckNetsh, isNetshExistInPATH },
                { RunBasicDialogStates.CheckTimestamps, isTimestampsChecked },
                { RunBasicDialogStates.CheckAdGuard, isAdGuardRunning },
                { RunBasicDialogStates.CheckKiller, isRunningKiller },
                { RunBasicDialogStates.CheckIntelConnectivityNetwork, isRunningIntelConnectivityNetwork },
                { RunBasicDialogStates.CheckCheckPointServices, isCheckPointServiceRunning },
                { RunBasicDialogStates.CheckSmartByte, isSmartByteRunning },
                { RunBasicDialogStates.CheckVPNs, isRunningVPN },
                { RunBasicDialogStates.CheckDNS, isUsedDNS },
                { RunBasicDialogStates.CheckWinDivert, isWinDivertRunning },
                { RunBasicDialogStates.CheckAnotherComponents, isAnotherServiceComponentRunned },
                { RunBasicDialogStates.CheckAnotherComponentsServices, isAnotherServiceComponentRunned },
            };

            await Task.CompletedTask;
            Services = null;

            BasicDialogStateChanged?.Invoke(RunBasicDialogStates.Completed);

            return completedStates;
        }

        public async Task<Dictionary<RunBasicDialogStates, FixResultModel>> FixAllBasicErrors(Dictionary<RunBasicDialogStates, bool> statesList)
        {
            Dictionary<RunBasicDialogStates, FixResultModel> result = [];
            foreach (var state in statesList)
            {
                if (state.Value != (BasicDiagnosticStateModelPairs.FirstOrDefault(x => x.Key == state.Key).Value?.CorrectState ?? false))
                {
                    result.Add(state.Key, await TryToFixBasicError(state.Key));
                }
            }
            await Task.Delay(1000);
            return result;
        }

        public class FixResultModel
        {
            public bool IsFixed { get; set; }
            public string ErrorCode { get; set; }
        }

        public async Task<FixResultModel> TryToFixBasicError(RunBasicDialogStates state)
        {
            FixResultModel fixResultModel = new() { IsFixed = false, ErrorCode = "INFO_CANNOT_FIX" };
            switch (state)
            {
                case RunBasicDialogStates.CheckTimestamps:
                    fixResultModel.IsFixed = EnableTimestamps();
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                case RunBasicDialogStates.CheckWinDivert:
                    _ = ProcessManager.StopService();
                    fixResultModel.IsFixed = true;
                    break;
                default:
                    fixResultModel.IsFixed = false;
                    fixResultModel.ErrorCode = "INFO_CANNOT_FIX";
                    break;
            }
            await Task.CompletedTask;
            return fixResultModel;
        }

        ServiceController[] Services;

        private bool IsServiceExist(string name)
        {
            try
            {
                Services ??= ServiceController.GetServices();
                return Services.FirstOrDefault(s => s.ServiceName.Contains(name, StringComparison.OrdinalIgnoreCase))?.Status != null;
            }
            catch
            {
                return false;
            }
        }

        private bool IsServiceRunning(string name)
        {
            try
            {
                Services ??= ServiceController.GetServices();
                ServiceControllerStatus serviceControllerStatus = (Services.FirstOrDefault(s => s.ServiceName.Contains(name, StringComparison.OrdinalIgnoreCase))?.Status ?? ServiceControllerStatus.Stopped);
                return serviceControllerStatus == ServiceControllerStatus.Running || serviceControllerStatus == ServiceControllerStatus.StopPending || serviceControllerStatus == ServiceControllerStatus.StartPending;
            }
            catch
            {
                return false;
            }
        }

        private static bool ExistsOnPath(string fileName)
        {
            try
            {
                return GetFullPath(fileName) != null;
            }
            catch
            {
                return false;
            }
        }

        private static string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        private bool EnableTimestamps()
        {
            try
            {
                Process p = new Process();
                ProcessStartInfo psi = new ProcessStartInfo("netsh", "interface tcp set global timestamps=enabled");
                p.StartInfo = psi;
                p.Start();
                p.WaitForExit();
                // Debug.WriteLine(p.StandardOutput);
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(TroubleshootingHelper), $"Cannot enable timestamps. {ex.Message}");
                return false;
            }
        }

        private bool CheckTimestamps()
        {
            try
            {
                Process p = new Process();
                ProcessStartInfo psi = new ProcessStartInfo("netsh", "interface tcp show global | findstr /i \"timestamps\" | findstr /i \"enabled\"");
                p.StartInfo = psi;
                p.Start();
                p.WaitForExit();
                // Debug.WriteLine(p.StandardOutput);
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(TroubleshootingHelper), $"Cannot check timestamps. {ex.Message}");
                return false;
            }
        }

        private static bool IsProcessRunning(string procName)
        {
            Process[] processes =
                Process.GetProcessesByName(procName);

            if (processes.Length > 0)
            {
                return true;
            }
            return false;
        }

        private static bool GetDnsAdress()
        {
            try
            {
                Process p = new Process();
                ProcessStartInfo psi = new ProcessStartInfo("powershell", "Get-ChildItem -Recurse -Path 'HKLM:System\\CurrentControlSet\\Services\\Dnscache\\InterfaceSpecificParameters\\' | Get-ItemProperty | Where-Object { $_.DohFlags -gt 0 } | Measure-Object | Select-Object -ExpandProperty Count");
                p.StartInfo = psi;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();
                p.WaitForExit();

                return int.TryParse(p.StandardOutput.ReadToEnd(), out int result) && result != 0;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(TroubleshootingHelper), $"Cannot check DNS. {ex.Message}");
                return false;
            }
        }
    }
}
