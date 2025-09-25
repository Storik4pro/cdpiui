using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;

namespace CDPIUI_TrayIcon.Helper
{
    public class GoodCheckProcessHelper
    {
        private static GoodCheckProcessHelper? _instance;
        private static readonly object _lock = new object();

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

        private static CancellationTokenSource _cancellationTokenSource = new();
        private static CancellationToken _cancellationToken = _cancellationTokenSource.Token;

        private Process? Process;
        private nint Job;

        public GoodCheckProcessHelper() { }

        public async Task<bool> StartAsync(string executable, string args, string operationId)
        {
            await ProcessManager.Instance.StopProcess();

            _cancellationTokenSource = new();
            _cancellationToken = _cancellationTokenSource.Token;
            await PipeServer.Instance.SendMessage($"GOODCHECK:RUNNED({operationId})");
            TrayIconHelper.Instance.ToggleStartButtonEnabled(true);


            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(executable),
            };

            Job = CreateKillOnCloseJob();
            

            Process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            try
            {
                if (!Process.Start())
                {
                    HandleProcessException("ERR_UNABLE_START_PROCESS", operationId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                // TODO: add logging
                HandleProcessException("ERR_UNABLE_START_PROCESS", operationId);
                return false;
            }

            if (!AssignProcessToJobObject(Job, Process.Handle))
            {
                HandleProcessException("ERR_WIN32_EXCEPTION", operationId);
                // Logger.Instance.CreateErrorLog(nameof(GoodCheckProcessHelper), $"ERR_WIN32_EXCEPTION details => {Marshal.GetLastWin32Error()}");
                return false;
            }

            var completedTask = await Task.WhenAny(Task.Run(() =>
            {
                Process.WaitForExit();
            }, _cancellationToken)).ConfigureAwait(false);

            TryKillProcess(Process);
            await PipeServer.Instance.SendMessage($"GOODCHECK:DIED({operationId})");
            TrayIconHelper.Instance.ToggleStartButtonEnabled(true);
            return true;
        }

        public void Stop()
        {
            if (Process != null) TryKillProcess(Process);
            try
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            } 
            catch
            {
                // pass
            }
        }
        

        private void TryKillProcess(Process proc)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    proc.Close();
                    proc.Dispose();
                }
            }
            catch
            {
                // pass
            }
        }

        private void HandleProcessException(string error, string operationId)
        {
            _ = PipeServer.Instance.SendMessage($"GOODCHECK:DIEDVIAERR({operationId}$SEPARATOR{error})");
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
        static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

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
