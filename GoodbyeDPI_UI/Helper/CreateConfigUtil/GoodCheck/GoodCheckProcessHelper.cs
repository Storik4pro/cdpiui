using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GoodbyeDPI_UI.Helper.CreateConfigUtil.GoodCheck
{
    public enum GoodCheckProcessMode
    {
        AllAsOne, // All site lists -> One strategy list
        AnyAsAny, // One site list -> One strategy list
    }
    public class GoodCheckSiteListModel
    {
        public string SiteListPath { get; set; }
        public string StrategyListPath { get; set; }
    }

    public class GoodCheckOperationModel
    {
        public int OperationId { get; set; }
        public string SiteListName { get; set; } 
        public string Output { get; set; } = "";
        public GoodCheckOperationType OperationType { get; set; } = GoodCheckOperationType.Wait;
        public string ErrorCode { get; set; } = "";
    }
    public enum GoodCheckOperationType
    {
        Wait,
        ErrorHappens,
        SuccessFinish,
        UserInterrupt,
        WorkInProgress,
    }
    public class GoodCheckProcessHelper
    {
        public Action<string> ErrorHappens;
        public Action<GoodCheckSiteListModel> ProcessCompleted;
        public Action<string> CurrentSiteListChanged;
        public Action<Tuple<string, string, string>> ProgressChanged;
        public Action<Tuple<int, int>> StrategyResultAdded;
        public Action<int> CorrectCountChanged;
        public Action<int> IncorrectCountChanged;

        public Action<Tuple<int, string>> OperationOutputAdded;
        public Action<Tuple<int, GoodCheckOperationType>> OperationTypeChanged;
        public Action OperationsListChanged;

        public int CorrectCount { get; private set; }
        public int IncorrectCount { get; private set; }
        public string CurrentSiteList {  get; private set; }
        public Tuple<int, int> CurrentSiteListIndex { get; private set; }

        private const string AddonId = "ASGKOI001";
        private const string CheckLists = "CheckLists";

        private string ExeFileName = "";
        private string DirName = "";
        private string ComponentName = "";

        private const string GetProgressRegex = @"Launching '[a-zA-Z]{1,}', strategy (\d{1,})/(\d{1,}): \[(.*?)\]";
        private const string GetStrategyCountRegex = @"worst result for this strategy: (\d{1,})/(\d{1,})\n{1,}Terminating program\.\.\.";

        private CancellationTokenSource CancellationTokenSource;
        private CancellationToken CancellationToken;

        private GoodCheckProcessMode _mode;
        private List<GoodCheckSiteListModel> _siteList = [];
        
        private static GoodCheckProcessHelper _instance;
        private static readonly object _lock = new object();

        private int CurrentOperationId = 0;
        private List<GoodCheckOperationModel> Operations = new List<GoodCheckOperationModel>();

        public static GoodCheckProcessHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new GoodCheckProcessHelper();
                    return _instance;
                }
            }
        }

        private GoodCheckProcessHelper()
        {

        }

        private bool IsGoodCheckInstalled()
        {
            bool isInstalled = DatabaseHelper.Instance.IsItemInstalled(AddonId);

            if (!isInstalled)
                return false;

            DatabaseStoreItem item = DatabaseHelper.Instance.GetItemById(AddonId);

            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            DirName = Path.Combine(
                localAppData,
                StateHelper.StoreDirName,
                StateHelper.StoreItemsDirName,
                AddonId);

            ExeFileName = Path.Combine(
                DirName,
                item.Executable + ".exe");

            return File.Exists(ExeFileName);
        }

        public static void MergeUniquePreserveOrder(
            List<GoodCheckSiteListModel> siteLists, 
            string outputFile,
            Encoding encoding = null, 
            bool trimLines = true, 
            bool ignoreEmpty = true,
            StringComparer comparer = null)
        {
            encoding ??= Encoding.UTF8;
            comparer ??= StringComparer.Ordinal; 

            var seen = new HashSet<string>(comparer);

            using var writer = new StreamWriter(outputFile, false, encoding);
            foreach (var siteList in siteLists)
            {
                if (!File.Exists(siteList.SiteListPath)) continue; 
                using var reader = new StreamReader(siteList.SiteListPath, encoding);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (trimLines) line = line.Trim();
                    if (ignoreEmpty && string.IsNullOrEmpty(line)) continue;

                    if (seen.Add(line))
                    {
                        writer.WriteLine(line);
                    }
                }
            }
        }

        public void InitGoodCheck(
            string componentId, 
            GoodCheckProcessMode checkMode, 
            List<GoodCheckSiteListModel> siteListsToCheck
            )
        {
            _siteList.Clear();
            Operations.Clear();
            
            CurrentOperationId = 0;

            ComponentName = StateHelper.Instance.ComponentIdPairs[componentId].ToLower();
            _mode = checkMode;

            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string siteListFolder = Path.Combine(
                localAppData,
                StateHelper.StoreDirName,
                StateHelper.StoreItemsDirName,
                AddonId,
                CheckLists);

            if (_mode == GoodCheckProcessMode.AllAsOne)
            {
                TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                int secondsSinceEpoch = (int)t.TotalSeconds;

                string siteList = Path.Combine(siteListFolder, $"{secondsSinceEpoch}_sitelist.txt");

                MergeUniquePreserveOrder(siteListsToCheck, siteList);

                GoodCheckSiteListModel model = new()
                {
                    SiteListPath = Path.GetFileName(siteList),
                    StrategyListPath = Path.GetFileName(siteListsToCheck[0].StrategyListPath),
                };
                _siteList.Add(model);
                Operations.Add(new()
                {
                    OperationId = 0,
                    SiteListName = Path.GetFileName(siteList),
                });
            }
            else
            {
                for (int i = 0; i < siteListsToCheck.Count; i++)
                {
                    var model = siteListsToCheck[i];
                    File.Copy(model.SiteListPath, Path.Combine(siteListFolder, Path.GetFileName(model.SiteListPath)), true);

                    model.SiteListPath = Path.GetFileName(model.SiteListPath);
                    model.StrategyListPath = Path.GetFileName(model.StrategyListPath);

                    Operations.Add(new()
                    {
                        OperationId = i,
                        SiteListName = model.SiteListPath,
                    });
                }
                _siteList = siteListsToCheck;
            }

            OperationsListChanged?.Invoke();

        }

        public void Start()
        {
            CancellationTokenSource = new();
            CancellationToken = CancellationTokenSource.Token;
            _ = StartCheck(CancellationToken);
        }

        public void Stop()
        {
            CancellationTokenSource.Dispose();
        }


        public async Task StartCheck(CancellationToken cancellationToken)
        {
            if (!IsGoodCheckInstalled())
            {
                ErrorHappens?.Invoke("ERR_ADDON_NOT_INSTALLED");
                return;
            }

            try
            {
                if (_siteList != null)
                {
                    for (int i = 0; i < _siteList.Count; i++)
                    {
                        CurrentOperationId = i;
                        Operations[CurrentOperationId].OperationType = GoodCheckOperationType.WorkInProgress;
                        OperationTypeChanged?.Invoke(Tuple.Create(CurrentOperationId, GoodCheckOperationType.WorkInProgress));

                        var site = _siteList[i];
                        CurrentSiteList = $"[{i + 1}/{_siteList.Count}] {site.SiteListPath}";
                        CurrentSiteListChanged?.Invoke(CurrentSiteList);

                        CurrentSiteListIndex = Tuple.Create(i+1, _siteList.Count);

                        if (cancellationToken.IsCancellationRequested) break;

                        bool ok = await StartProcessAsync(site, cancellationToken).ConfigureAwait(false);
                        if (ok)
                        {
                            ProcessCompleted?.Invoke(site);
                            Operations[CurrentOperationId].OperationType = GoodCheckOperationType.SuccessFinish;
                            OperationTypeChanged?.Invoke(Tuple.Create(CurrentOperationId, GoodCheckOperationType.SuccessFinish));
                        }

                        if (cancellationToken.IsCancellationRequested) break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // pass
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessHelper), $"ERR_INTERNAL happens: {ex}");
                ErrorHappens?.Invoke("ERR_INTERNAL");
            }
        }

        private async Task<bool> StartProcessAsync(GoodCheckSiteListModel siteList, CancellationToken cancellationToken)
        {
            IncorrectCount = 0;
            CorrectCount = 0;
            var prevWorking = Environment.CurrentDirectory;
            Environment.CurrentDirectory = DirName;

            if (string.IsNullOrEmpty(ExeFileName) || !File.Exists(ExeFileName))
            {
                HandleProcessException("ERR_EXE_NOT_FOUND", CurrentOperationId);
                return false;
            }
            string resolver = SettingsManager.Instance.GetValue<bool>(["ADDONS", AddonId], "UseCurl") ? "curl" : "native";
            string args =
                $"-q "+
                $"-p {SettingsManager.Instance.GetValue<string>(["ADDONS", AddonId], "passesValue")} " +
                $"-f {ComponentName} " + 
                $"-m {resolver} " +
                $"-c \"{siteList.SiteListPath}\" " +
                $"-s \"{siteList.StrategyListPath}\" ";

            var startInfo = new ProcessStartInfo
            {
                FileName = ExeFileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = DirName,
            };

            var job = CreateKillOnCloseJob();

            using var proc = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            try
            {
                if (!proc.Start())
                {
                    HandleProcessException("ERR_UNABLE_START_PROCESS", CurrentOperationId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                HandleProcessException("ERR_UNABLE_START_PROCESS", CurrentOperationId);
                return false;
            }

            if (!AssignProcessToJobObject(job, proc.Handle))
            {
                HandleProcessException("ERR_WIN32_EXCEPTION", CurrentOperationId);
                Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessHelper), $"ERR_WIN32_EXCEPTION details => {Marshal.GetLastWin32Error()}");
                return false;
            }

            string logsDirectory = Path.Combine(DirName, "Logs");
            var monitorTask = MonitorProcessAndLogsAsync(proc, logsDirectory, cancellationToken);

            try
            {
                var completedTask = await Task.WhenAny(monitorTask, Task.Run(() =>
                {
                    proc.WaitForExit();
                }, cancellationToken)).ConfigureAwait(false);

                if (completedTask == monitorTask)
                {
                    bool result = await monitorTask.ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested && !proc.HasExited)
                    {
                        TryKillProcess(proc);
                        return false;
                    }

                    return result;
                }
                else
                {
                    bool result = await monitorTask.ConfigureAwait(false);
                    return result;
                }
            }
            finally
            {
                if (!proc.HasExited)
                {
                    TryKillProcess(proc);
                }
                Environment.CurrentDirectory = prevWorking;
            }

        }

        private void TryKillProcess(Process proc)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // pass
            }
        }

        string goodStrategyCountString;

        private void AddOutput(string output)
        {
            Match progressMatch = Regex.Match(output, GetProgressRegex);
            if (progressMatch.Success)
            {
                string current = progressMatch.Groups[1].Value;
                string all = progressMatch.Groups[2].Value;
                string strategy = progressMatch.Groups[3].Value;

                ProgressChanged?.Invoke(Tuple.Create(current, all, strategy));
            }
            if (output.Contains("This strategy has no successes"))
            {
                IncorrectCount++;
                IncorrectCountChanged?.Invoke(IncorrectCount);
            }

            if (output.Contains("worst result for this strategy:"))
            {
                goodStrategyCountString = output;
            }

            if (!string.IsNullOrEmpty(goodStrategyCountString))
            {
                goodStrategyCountString += $"\n{output}";
                Match resultMatch = Regex.Match(goodStrategyCountString, GetStrategyCountRegex);
                if (resultMatch.Success)
                {
                    string current = resultMatch.Groups[1].Value;
                    string all = resultMatch.Groups[2].Value;

                    int currentInt = 0;
                    int allInt = 1;

                    int.TryParse(current, out currentInt);
                    int.TryParse(all, out allInt);

                    StrategyResultAdded?.Invoke(Tuple.Create(currentInt, allInt));

                    if (allInt != 0 && currentInt / allInt * 100 >= 65)
                    {
                        CorrectCount++;
                        CorrectCountChanged?.Invoke(CorrectCount);
                    }
                    else if (currentInt != 0)
                    {
                        IncorrectCount++;
                        IncorrectCountChanged?.Invoke(IncorrectCount);
                    }
                    goodStrategyCountString = null;
                }
            }

            OperationOutputAdded?.Invoke(Tuple.Create(CurrentOperationId, $"{output}"));
            Operations[CurrentOperationId].Output += $"{output}\n";
        }

        private async Task<bool> MonitorProcessAndLogsAsync(Process proc, string logsDirectory, CancellationToken cancellationToken)
        {
            const string logPattern = "logfile_GoodCheckGoGo_*.log";
            const string errorFlag = "Exiting with an error...";
            const string successFlag = "All Done";

            string currentLogFile = null;
            long currentPosition = 0;
            StreamReader reader = null;
            FileStream fs = null;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string newest = GetNewestLogFile(logsDirectory, logPattern);

                    if (string.IsNullOrEmpty(newest))
                    {
                        if (proc.HasExited)
                        {
                            HandleProcessException("ERR_PROCESS_DIED_UNKNOWN", CurrentOperationId);
                            return false;
                        }

                        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (reader == null || !string.Equals(currentLogFile, newest, StringComparison.OrdinalIgnoreCase))
                    {
                        reader?.Dispose();
                        fs?.Dispose();

                        currentLogFile = newest;
                        fs = new FileStream(currentLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        reader = new StreamReader(fs);

                        currentPosition = fs.Length;
                        fs.Seek(currentPosition, SeekOrigin.Begin);
                    }

                    string newText = await reader.ReadToEndAsync().ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(newText))
                    {
                        currentPosition = fs.Position;
                        string[] lines = newText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            AddOutput(line);

                            if (line.IndexOf(errorFlag, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                HandleProcessException("ERR_GOODCHECK_EXCEPTION", CurrentOperationId);
                                return false;
                            }
                            if (line.IndexOf(successFlag, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return true;
                            }
                        }
                    }

                    if (proc.HasExited)
                    {
                        string remaining = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(remaining))
                        {
                            string[] lines = remaining.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                AddOutput(line);

                                if (line.IndexOf(errorFlag, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    HandleProcessException("ERR_GOODCHECK_EXCEPTION", CurrentOperationId);
                                    return false;
                                }
                                if (line.IndexOf(successFlag, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    return true;
                                }
                            }
                        }

                        HandleProcessException("ERR_PROCESS_DIED", CurrentOperationId);
                        return false;
                    }

                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                Operations[CurrentOperationId].OperationType = GoodCheckOperationType.UserInterrupt;
                OperationTypeChanged?.Invoke(Tuple.Create(CurrentOperationId, GoodCheckOperationType.UserInterrupt));
                return false;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessHelper), $"ERR_MONITOR_EXCEPTION happens: {ex}");
                HandleProcessException("ERR_MONITOR_EXCEPTION", CurrentOperationId);
                return false;
            }
            finally
            {
                reader?.Dispose();
                fs?.Dispose();
            }
        }

        private string GetNewestLogFile(string directory, string pattern)
        {
            try
            {
                if (!Directory.Exists(directory)) return null;
                var files = Directory.GetFiles(directory, pattern);
                if (files == null || files.Length == 0) return null;

                return files.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private void HandleProcessException(string errorCode, int operationId)
        {
            Operations[operationId].OperationType = GoodCheckOperationType.ErrorHappens;
            Operations[operationId].ErrorCode = errorCode;
            OperationTypeChanged?.Invoke(Tuple.Create(operationId, GoodCheckOperationType.ErrorHappens));
            ErrorHappens?.Invoke(errorCode);
        }

        public List<GoodCheckOperationModel> GetCurrentOperations()
        {
            return Operations;
        }
        public GoodCheckOperationModel GetOperationById(int id)
        {
            try
            {
                return Operations[id];
            }
            catch { return null; }
        }
        #region WinAPI

        const int JobObjectExtendedLimitInformation = 9;
        const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

        [StructLayout(LayoutKind.Sequential)]
        struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        static IntPtr CreateKillOnCloseJob()
        {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr p = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, p, false);
                if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, p, (uint)length))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }

            return job;
        }
        #endregion
    }
}
