using CDPIUI.Shared.PrettyErrorConvertionService;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CDPIUI.Shared.Extentions;

namespace CDPIUI.TrayIcon.Helper
{
    public sealed class ConPTYCaptureResult
    {
        public string Output { get; init; } = string.Empty;
        public int ExitCode { get; init; }
        public bool TimedOut { get; init; }
    }

    enum StopActionCallers
    {
        Unknown,
        User
    }
    public class ConPTYHelper
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private PROCESS_INFORMATION _processInfo;
        private IntPtr _pseudoConsoleHandle = IntPtr.Zero;
        private IntPtr _hInputWrite = IntPtr.Zero;
        private IntPtr _hOutputRead = IntPtr.Zero;

        private readonly StringBuilder _outputDefaultBuffer;

        public Action<bool>? ProcessStateChanged;
        public Action<Tuple<PrettyErrorCode, string>>? ErrorHappens;
        public Action<string>? ProcessExited;
        public Action<string>? OutputAdded;

        private readonly string Preffix = string.Empty;

        private StopActionCallers StopActionCaller = StopActionCallers.Unknown;

        static readonly Dictionary<string, PrettyErrorCode> errorMappings = new()
        {
            { "Error opening filter", PrettyErrorCode.FILTER_OPEN_ERROR },
            { "unknown option",  PrettyErrorCode.PARAMETER_ERROR },
            { "hostlists load failed",  PrettyErrorCode.HOSTLIST_LOAD_ERROR },
            { "must specify port filter",  PrettyErrorCode.PORT_FILTER_ERROR },
            { "must specify port or/and partial raw filter",  PrettyErrorCode.PORT_FILTER_WRONG_VALUE_ERROR },
            { "ERROR:",  PrettyErrorCode.UNKNOWN },
            { "Component not installed correctly",  PrettyErrorCode.COMPONENT_INSTALL_ERROR },
            { "error",  PrettyErrorCode.UNKNOWN },
            { "invalid value",  PrettyErrorCode.INVALID_VALUE_ERROR },
            { "nvalid value",  PrettyErrorCode.INVALID_VALUE_ERROR },
            { "option requires an argument", PrettyErrorCode.INVALID_VALUE_ERROR },
            { "--debug=0|1|syslog|@<filename>",  PrettyErrorCode.PARAMETER_ERROR },
            { "already running",  PrettyErrorCode.ALREADY_RUNNING_WARN },
            { "could not read",  PrettyErrorCode.FILE_READ_ERROR },
            { "flag provided but not defined:",  PrettyErrorCode.PARAMETER_ERROR },
            { "cannot create",  PrettyErrorCode.ACCESS_DENIED },
            { "cannot access",  PrettyErrorCode.ACCESS_DENIED }
        };

        public bool processState { get; private set; } = false;
        private bool CurrentState = false;
        private object _setStateLock = new();
        private void ChangeProcessState(bool isRunned)
        {
            lock (_setStateLock)
            {
                if (CurrentState != isRunned)
                {
                    CurrentState = isRunned;
                    processState = CurrentState;
                    ProcessStateChanged?.Invoke(isRunned);
                    Debug.WriteLine(processState);
                }
            }
        }

        public ConPTYHelper(string preffix = "") 
        {
            Preffix = preffix;
            _outputDefaultBuffer = new StringBuilder();
        }

        public bool IsReadyToRunNewProcess()
        {
            if (_processInfo.hProcess != IntPtr.Zero)
            {
                return true;
            }
            return false;
        }

        private readonly SemaphoreSlim _processLock = new SemaphoreSlim(1, 1);

        public static async Task<ConPTYCaptureResult> CaptureProcessOutputAsync(
            string exePath,
            string args,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            IntPtr pseudoConsoleHandle = IntPtr.Zero;
            IntPtr hInputRead = IntPtr.Zero;
            IntPtr hInputWrite = IntPtr.Zero;
            IntPtr hOutputRead = IntPtr.Zero;
            IntPtr hOutputWrite = IntPtr.Zero;
            IntPtr attributeList = IntPtr.Zero;
            PROCESS_INFORMATION processInfo = default;
            Task? outputTask = null;
            StringBuilder output = new();
            bool timedOut = false;
            bool canceled = false;
            int exitCode = -1;

            try
            {
                CreatePipe(out hInputRead, out hInputWrite, false);
                CreatePipe(out hOutputRead, out hOutputWrite, false);

                COORD size = new() { X = 4096, Y = 50 };
                uint result = CreatePseudoConsole(
                    size,
                    hInputRead,
                    hOutputWrite,
                    0,
                    out pseudoConsoleHandle);
                if (result != 0)
                {
                    throw new InvalidOperationException(
                        $"Unable to create PseudoConsole, error: {result}");
                }

                CloseHandle(hInputRead);
                hInputRead = IntPtr.Zero;
                CloseHandle(hOutputWrite);
                hOutputWrite = IntPtr.Zero;

                STARTUPINFOEX startupInfo = new()
                {
                    StartupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf<STARTUPINFOEX>(),
                        dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW,
                        wShowWindow = SW_HIDE,
                    },
                };

                IntPtr attributeListSize = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
                attributeList = Marshal.AllocHGlobal(attributeListSize);
                if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize))
                {
                    throw new InvalidOperationException(
                        $"Cannot initialize process attributes, error: {Marshal.GetLastWin32Error()}");
                }

                if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    pseudoConsoleHandle,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new InvalidOperationException(
                        $"Cannot attach PseudoConsole, error: {Marshal.GetLastWin32Error()}");
                }

                startupInfo.lpAttributeList = attributeList;
                string resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
                    : workingDirectory;
                bool started = CreateProcess(
                    null,
                    $"\"{exePath}\" {args}",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    resolvedWorkingDirectory,
                    ref startupInfo,
                    out processInfo);
                if (!started)
                {
                    throw new InvalidOperationException(
                        $"Cannot start process, error: {Marshal.GetLastWin32Error()}");
                }

                CloseHandle(processInfo.hThread);
                processInfo.hThread = IntPtr.Zero;

                SafeFileHandle safeOutputHandle = new(hOutputRead, ownsHandle: true);
                hOutputRead = IntPtr.Zero;
                outputTask = ReadCaptureOutputAsync(safeOutputHandle, output);

                using CancellationTokenSource timeoutSource = new(timeout);
                using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutSource.Token);
                try
                {
                    await WaitForProcessExitAsync(processInfo, linkedSource.Token);
                }
                catch (OperationCanceledException)
                {
                    canceled = cancellationToken.IsCancellationRequested;
                    timedOut = !canceled && timeoutSource.IsCancellationRequested;
                    TerminateProcess(processInfo.hProcess, 1);
                    WaitForSingleObject(processInfo.hProcess, INFINITE);
                }

                if (GetExitCodeProcess(processInfo.hProcess, out uint nativeExitCode))
                {
                    exitCode = unchecked((int)nativeExitCode);
                }

                ClosePseudoConsole(pseudoConsoleHandle);
                pseudoConsoleHandle = IntPtr.Zero;
                CloseHandle(hInputWrite);
                hInputWrite = IntPtr.Zero;

                try
                {
                    await outputTask;
                }
                catch (IOException)
                {
                    // pass
                }

                if (canceled)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return new ConPTYCaptureResult
                {
                    Output = output.ToString(),
                    ExitCode = exitCode,
                    TimedOut = timedOut,
                };
            }
            finally
            {
                if (processInfo.hProcess != IntPtr.Zero)
                {
                    if (WaitForSingleObject(processInfo.hProcess, 0) == WAIT_TIMEOUT)
                    {
                        TerminateProcess(processInfo.hProcess, 1);
                        WaitForSingleObject(processInfo.hProcess, INFINITE);
                    }
                    CloseHandle(processInfo.hProcess);
                }
                if (processInfo.hThread != IntPtr.Zero)
                {
                    CloseHandle(processInfo.hThread);
                }
                if (attributeList != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeList);
                }
                if (pseudoConsoleHandle != IntPtr.Zero)
                {
                    ClosePseudoConsole(pseudoConsoleHandle);
                }
                if (hInputRead != IntPtr.Zero)
                {
                    CloseHandle(hInputRead);
                }
                if (hInputWrite != IntPtr.Zero)
                {
                    CloseHandle(hInputWrite);
                }
                if (hOutputRead != IntPtr.Zero)
                {
                    CloseHandle(hOutputRead);
                }
                if (hOutputWrite != IntPtr.Zero)
                {
                    CloseHandle(hOutputWrite);
                }

                if (outputTask != null && !outputTask.IsCompleted)
                {
                    try
                    {
                        await outputTask;
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static async Task ReadCaptureOutputAsync(
            SafeFileHandle outputHandle,
            StringBuilder output)
        {
            using FileStream stream = new(outputHandle, FileAccess.Read, 4096, isAsync: false);
            using StreamReader reader = new(stream, Encoding.UTF8, true, 4096, leaveOpen: false);
            char[] buffer = new char[4096];
            int charsRead;
            while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                output.Append(buffer, 0, charsRead);
            }
        }

        private static async Task WaitForProcessExitAsync(
            PROCESS_INFORMATION processInfo,
            CancellationToken cancellationToken)
        {
            while (WaitForSingleObject(processInfo.hProcess, 100) == WAIT_TIMEOUT)
            {
                await Task.Delay(25, cancellationToken);
            }
        }

        public async void RunProcess(string exePath, string args, string workingDirectory)
        {
            StopActionCaller = StopActionCallers.Unknown;
            await _processLock.WaitAsync();
            try
            {
                _outputDefaultBuffer.Clear();

                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                processState = true;
                ChangeProcessState(true);

                _ = Task.Run(() => RunProcessWithConPTY(exePath, args, workingDirectory ?? "", token));
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ErrorHelper.Convertor.GetPrettyErrorCode(nameof(ConPTYHelper), ex).ToEnum<PrettyErrorCode>(), _object: "console");
                SendStopMessage("Unexpected error happens while trying to stop process");

                processState = false;
                ChangeProcessState(false);
            }
            _processLock.Release();
        }

        public async void RunProcessWithConPTY(string exePath, string args, string workingDirectory, CancellationToken token, bool disableKillAfterError=false)
        {
            await _processLock.WaitAsync();

            IntPtr pseudoConsoleHandle = IntPtr.Zero;
            IntPtr hInputRead = IntPtr.Zero;
            IntPtr hInputWrite = IntPtr.Zero;
            IntPtr hOutputRead = IntPtr.Zero;
            IntPtr hOutputWrite = IntPtr.Zero;

            PrettyErrorCode lastError = PrettyErrorCode.SUCCESS;

            try
            {
                CreatePipe(out hInputRead, out hInputWrite, false);
                CreatePipe(out hOutputRead, out hOutputWrite, false);

                _hInputWrite = hInputWrite;
                _hOutputRead = hOutputRead;

                uint consoleSizeX = 80;
                uint consoleSizeY = 25;
                var size = new COORD { X = (short)consoleSizeX, Y = (short)consoleSizeY };
                var hr = CreatePseudoConsole(size, hInputRead, hOutputWrite, 0, out pseudoConsoleHandle);

                if (hr != 0)
                {
                    throw new Exception($"Unable to create PseudoConsole, error: {hr}");
                }

                _pseudoConsoleHandle = pseudoConsoleHandle;

                CloseHandle(hInputRead);
                hInputRead = IntPtr.Zero;
                CloseHandle(hOutputWrite);
                hOutputWrite = IntPtr.Zero;

                var si = new STARTUPINFOEX
                {
                    StartupInfo = new STARTUPINFO
                    {
                        cb = Marshal.SizeOf(typeof(STARTUPINFOEX)),
                        dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW,
                        wShowWindow = SW_HIDE
                    }
                };

                IntPtr lpAttrList = IntPtr.Zero;
                var lpSize = IntPtr.Zero;

                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                lpAttrList = Marshal.AllocHGlobal(lpSize);
                InitializeProcThreadAttributeList(lpAttrList, 1, 0, ref lpSize);

                UpdateProcThreadAttribute(lpAttrList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, _pseudoConsoleHandle, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);

                si.lpAttributeList = lpAttrList;

                var pi = new PROCESS_INFORMATION();

                var success = CreateProcess(
                    null,
                    $"\"{exePath}\" {args}",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    workingDirectory,
                    ref si,
                    out pi);

                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new Exception($"Cannot start process, error: {error}");
                }

                _processInfo = pi;

                CloseHandle(pi.hThread);

                var safeOutputReadHandle = new SafeFileHandle(_hOutputRead, ownsHandle: true);
                _hOutputRead = IntPtr.Zero;

                _ = Task.Run(() => CheckProcessState(pi, token));

                using (var reader = new FileStream(safeOutputReadHandle, FileAccess.Read))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;


                    while (!token.IsCancellationRequested)
                    {
                        bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: token);

                        string _output = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        foreach (var errorMapping in errorMappings)
                        {
                            if (_output.Contains(errorMapping.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                lastError = errorMapping.Value;
                            }
                        }

                        if (!string.IsNullOrEmpty(Preffix) && !string.IsNullOrEmpty(_output))
                        {
                            string newOutput = string.Empty;
                            foreach (string str in _output.Split("\n"))
                            {
                                newOutput += $"[{Preffix}] {str}\n";
                            }
                            _output = newOutput;
                        }

                        if (_outputDefaultBuffer.Length > 5000)
                        {
                            _outputDefaultBuffer.Clear();
                        }

                        _outputDefaultBuffer.Append(_output);

                        OutputAdded?.Invoke(_output);
                    }
                }

                TerminateProcess(pi.hProcess, 0);

                WaitForSingleObject(pi.hProcess, INFINITE);

                CloseHandle(pi.hProcess);
            }
            catch (OperationCanceledException ex)
            {
                
                Logger.Instance.CreateInfoLog(nameof(ConPTYHelper), $"Process will be stopped {lastError}.");
                if (lastError != PrettyErrorCode.SUCCESS && StopActionCaller == StopActionCallers.Unknown) ShowErrorMessage(lastError);
            }
            catch (Exception ex)
            {
                if (ex.Message != "External component has thrown an exception.")
                    ShowErrorMessage(ErrorHelper.Convertor.GetPrettyErrorCode(nameof(ConPTYHelper), ex).ToEnum<PrettyErrorCode>(), _object: "console");

            }
            finally
            {
                if (_pseudoConsoleHandle != IntPtr.Zero)
                {
                    ClosePseudoConsole(_pseudoConsoleHandle);
                    _pseudoConsoleHandle = IntPtr.Zero;
                }
                if (_hInputWrite != IntPtr.Zero)
                {
                    CloseHandle(_hInputWrite);
                    _hInputWrite = IntPtr.Zero;
                }

                SendStopMessage("Process will be stopped");


                processState = false;
                ChangeProcessState(processState);
                _processInfo = default;

                _processLock.Release();
            }
        }

        private async Task CheckProcessState(PROCESS_INFORMATION pi, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!ProcessAliveCheck(pi))
                {
                    _cancellationTokenSource.Cancel();
                    break;
                }
            }
            await Task.CompletedTask;
        }

        private static bool ProcessAliveCheck(PROCESS_INFORMATION pi)
        {
            switch (WaitForSingleObject(pi.hProcess, 2000))
            {
                case WAIT_OBJECT_0:
                    return false;

                case WAIT_TIMEOUT:
                    return true;
                default:
                    break;
            }
            return true;
        }

        public async Task StopProcess(bool output = true)
        {
            StopActionCaller = StopActionCallers.User;
            _cancellationTokenSource?.Cancel();

            await _processLock.WaitAsync();
            
            try
            {
                if (_processInfo.hProcess != IntPtr.Zero)
                {
                    TerminateProcess(_processInfo.hProcess, 0);

                    WaitForSingleObject(_processInfo.hProcess, INFINITE);

                    CloseHandle(_processInfo.hProcess);

                    _processInfo = default;
                }

                if (_pseudoConsoleHandle != IntPtr.Zero)
                {
                    ClosePseudoConsole(_pseudoConsoleHandle);
                    _pseudoConsoleHandle = IntPtr.Zero;
                }
                if (_hInputWrite != IntPtr.Zero)
                {
                    CloseHandle(_hInputWrite);
                    _hInputWrite = IntPtr.Zero;
                }
                processState = false;
                ChangeProcessState(processState);
            }
            catch (Exception)
            {
                processState = false;
                ChangeProcessState(processState);
            }
            _processLock.Release();
        }

        private void SendStopMessage(string output = "Process will be stopped by user")
        {
            string preffix = string.IsNullOrEmpty(Preffix) ? "" : $" [{Preffix}]";
            _outputDefaultBuffer.Append($"\n[PSEUDOCONSOLE]{preffix} {output}");

            ProcessExited?.Invoke($"\n[PSEUDOCONSOLE]{preffix} {output}");
        }

        private void ShowErrorMessage(PrettyErrorCode errorCode, string _object = "process")
        {
            Logger.Instance.CreateWarningLog(nameof(ProcessManager), $"CONPTY error: {errorCode} object: {_object}");
            ErrorHappens?.Invoke(Tuple.Create(errorCode, _object));
        }

        public string GetDefaultOutput()
        {
            return _outputDefaultBuffer.ToString();
        }

        #region WinAPI Definitions


        private const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        private const int STARTF_USESTDHANDLES = 0x00000100;
        private const int STARTF_USESHOWWINDOW = 0x00000001;
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const ushort SW_HIDE = 0;
        private const uint INFINITE = 0xFFFFFFFF;

        private const uint WAIT_OBJECT_0 = 0x000000000;
        private const uint WAIT_TIMEOUT = 0x00000102;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetExitCodeProcess(
            IntPtr hProcess,
            out System.UInt32 lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreateProcess(
            string? lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [StructLayout(LayoutKind.Sequential)]
        struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        private static void CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, bool bInheritHandle)
        {
            SECURITY_ATTRIBUTES saAttr = new SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)),
                bInheritHandle = bInheritHandle,
                lpSecurityDescriptor = IntPtr.Zero
            };

            if (!CreatePipe(out hReadPipe, out hWritePipe, ref saAttr, 0))
            {
                throw new Exception("Cannot create PseudoTerminal.");
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern int GetSystemDefaultLCID();

        #endregion
    }
}
