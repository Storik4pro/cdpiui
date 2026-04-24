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
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Xml.Linq;
using WinUI3Localizer;
using static CDPI_UI.Helper.MsiInstallerHelper;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using static System.Windows.Forms.AxHost;
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

        public Action<Enum> CurrentStateChanged;

        private TroubleshootingHelper()
        {

        }

        public class DiagnosticStateModel
        {
            public string CorrectValueDisplayText { get; set; }
            public string InCorrectValueDisplayText { get; set; }
            public bool CorrectState { get; set; }
        }

        public enum StoreCheckStates
        {
            Preparing,
            CheckGitHubRepo,
            CheckGitLabRepo,
            CheckWriteAccess,
            CheckWriteAccessIntoItemsFolder,
            CheckWriteAccessIntoRepoFolder,
            CheckWriteAccessIntoDatabaseFolder,
            Completed
        }

        public static Dictionary<StoreCheckStates, DiagnosticStateModel> StoreDiagnosticStateModelPairs = new()
        {
            { StoreCheckStates.CheckGitHubRepo, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckGitLabRepo, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckWriteAccess, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckWriteAccessIntoItemsFolder, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckWriteAccessIntoRepoFolder, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
            { StoreCheckStates.CheckWriteAccessIntoDatabaseFolder, new DiagnosticStateModel() { CorrectState = true, CorrectValueDisplayText = "Available", InCorrectValueDisplayText="NotAvailable" } },
        };

        public async Task<Dictionary<StoreCheckStates, bool>> RunStoreDiagnostic()
        {
            TasksHelper.Instance.StopAllTasks();
            _ = ProcessManager.StopService();
            CurrentStateChanged?.Invoke(StoreCheckStates.Preparing);

            await Task.Delay(1000);
            Services = null;
            Dictionary<StoreCheckStates, bool> completedStates = [];

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckGitHubRepo);
            bool isGitHubRepoAvailable = await StoreHelper.TryLoadDatabaseForVersionControl(SupportedVersionControls.GitHub);

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckGitLabRepo);
            bool isGitLabRepoAvailable = await StoreHelper.TryLoadDatabaseForVersionControl(SupportedVersionControls.GitLab);

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckWriteAccess);
            string localAppData = StateHelper.GetDataDirectory();
            string storeFolder = Path.Combine(localAppData, StateHelper.StoreDirName);

            bool hasWriteAccess = IsDirectoryWritable(Path.Combine(storeFolder));

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckWriteAccessIntoItemsFolder);
            bool hasWriteAccessIntoItemsFolder = IsDirectoryWritable(Path.Combine(storeFolder, StateHelper.StoreItemsDirName));

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckWriteAccessIntoRepoFolder);
            bool hasWriteAccessIntoRepoFolder = IsDirectoryWritable(Path.Combine(storeFolder, StateHelper.StoreRepoCache, StateHelper.StoreRepoDirName));

            CurrentStateChanged?.Invoke(StoreCheckStates.CheckWriteAccessIntoDatabaseFolder);
            bool hasWriteAccessIntoDatabaseFolder = IsDirectoryWritable(Path.Combine(storeFolder, StateHelper.StoreRepoCache, StateHelper.StoreLocalDirName));

            await Task.Delay(1000);

            completedStates = new()
            {
                { StoreCheckStates.CheckGitHubRepo, isGitHubRepoAvailable },
                { StoreCheckStates.CheckGitLabRepo, isGitLabRepoAvailable },
                { StoreCheckStates.CheckWriteAccess, hasWriteAccess },
                { StoreCheckStates.CheckWriteAccessIntoItemsFolder, hasWriteAccessIntoItemsFolder },
                { StoreCheckStates.CheckWriteAccessIntoRepoFolder, hasWriteAccessIntoRepoFolder },
                { StoreCheckStates.CheckWriteAccessIntoDatabaseFolder, hasWriteAccessIntoDatabaseFolder },
            };

            await Task.CompletedTask;
            Services = null;

            CurrentStateChanged?.Invoke(StoreCheckStates.Completed);

            return completedStates;
        }

        public async Task<Dictionary<StoreCheckStates, FixResultModel>> FixAllStoreErrors(Dictionary<StoreCheckStates, bool> statesList)
        {
            Dictionary<StoreCheckStates, FixResultModel> result = [];
            foreach (var state in statesList)
            {
                if (state.Value != (StoreDiagnosticStateModelPairs.FirstOrDefault(x => x.Key == state.Key).Value?.CorrectState ?? false))
                {
                    result.Add(state.Key, await TryToFixStoreError(state.Key));
                }
            }
            await Task.Delay(1000);
            return result;
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
            CurrentStateChanged?.Invoke(RunBasicDialogStates.Preparing);

            await Task.Delay(1000);
            Services = null;
            Dictionary<RunBasicDialogStates, bool> completedStates = [];

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckBFE);
            bool isRunningBFE = IsServiceRunning("BFE");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckProxy);
            bool isProxyEnabled = RegeditHelper.IsProxyEnabled();

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckNetsh);
            bool isNetshExistInPATH = ExistsOnPath("netsh.exe");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckTimestamps);
            bool isTimestampsChecked = CheckTimestamps();

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckAdGuard);
            bool isAdGuardRunning = IsProcessRunning("adguardvpnsvc") || IsProcessRunning("adguardvpn") || IsProcessRunning("adguardsvc");
            await Task.Delay(1000);

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckKiller);
            bool isRunningKiller = IsServiceRunning("Killer");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckIntelConnectivityNetwork);
            bool isRunningIntelConnectivityNetwork = IsServiceRunning("Intel") || IsServiceRunning("Connectivity") || IsServiceRunning("Network");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckCheckPointServices);
            bool isCheckPointServiceRunning = IsServiceRunning("TracSrvWrapper") || IsServiceRunning("EPWD");
            await Task.Delay(1000);

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckSmartByte);
            bool isSmartByteRunning = IsServiceRunning("SmartByte");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckVPNs);
            bool isRunningVPN = IsServiceExist("vpn");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckDNS);
            bool isUsedDNS = GetDnsAdress();

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckWinDivert);
            bool isWinDivertRunning = IsServiceRunning("windivert");

            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckAnotherComponentsServices);
            bool isAnotherServiceComponentRunned = IsServiceExist("GoodbyeDPI") || IsServiceExist("discordfix_zapret") || IsServiceExist("winws1") || IsServiceExist("winws2");
            CurrentStateChanged?.Invoke(RunBasicDialogStates.CheckAnotherComponents);
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

            CurrentStateChanged?.Invoke(RunBasicDialogStates.Completed);

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

        public async Task<FixResultModel> TryToFixStoreError(StoreCheckStates state)
        {
            FixResultModel fixResultModel = new() { IsFixed = false, ErrorCode = "INFO_CANNOT_FIX" };

            string localAppData = StateHelper.GetDataDirectory();
            string storeFolder = Path.Combine(localAppData, StateHelper.StoreDirName);

            switch (state)
            {
                case StoreCheckStates.CheckWriteAccess:
                    fixResultModel.IsFixed = await GrantAccess(storeFolder);
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                case StoreCheckStates.CheckWriteAccessIntoDatabaseFolder:
                    fixResultModel.IsFixed = await GrantAccess(Path.Combine(storeFolder, StateHelper.StoreRepoCache, StateHelper.StoreLocalDirName));
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                case StoreCheckStates.CheckWriteAccessIntoItemsFolder:
                    fixResultModel.IsFixed = await GrantAccess(Path.Combine(storeFolder, StateHelper.StoreItemsDirName));
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                case StoreCheckStates.CheckWriteAccessIntoRepoFolder:
                    fixResultModel.IsFixed = await GrantAccess(Path.Combine(storeFolder, StateHelper.StoreRepoCache, StateHelper.StoreRepoDirName));
                    fixResultModel.ErrorCode = fixResultModel.IsFixed ? string.Empty : "ERR_WIN32_UNKNOWN";
                    break;
                default:
                    fixResultModel.IsFixed = false;
                    fixResultModel.ErrorCode = "INFO_CANNOT_FIX";
                    break;
            }
            await Task.CompletedTask;
            return fixResultModel;
        }

        #region BasicFixServices

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

        #endregion

        #region StoreFixServices
        private TaskCompletionSource<bool> _tcs;
        private bool opResult;
        private async Task<bool> GrantAccess(string file, CancellationToken ct = default)
        {
            _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using (cts.Token.Register(() => _tcs.TrySetCanceled()))
            {
                await PipeClient.Instance.SendMessage($"UTILS:GRANT_ACCESS({file})");
                await _tcs.Task.ConfigureAwait(false);
                return _tcs.Task.Result;
            }
        }

        public void OnGrantAccessCompleted(bool state)
        {
            opResult = state;
            _tcs.TrySetResult(state);
        }

        private static bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
        {
            try
            {
                using FileStream fs = File.Create(
                    Path.Combine(
                        dirPath,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose);
                return true;
            }
            catch
            {
                if (throwIfFails)
                    throw;
                else
                    return false;
            }
        }

        #endregion
    }
}
