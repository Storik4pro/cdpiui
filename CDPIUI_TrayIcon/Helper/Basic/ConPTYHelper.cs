using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDPIUI_TrayIcon.Helper
{
    public class ConPTYHelper
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private PROCESS_INFORMATION _processInfo;
        private IntPtr _pseudoConsoleHandle = IntPtr.Zero;
        private IntPtr _hInputRead = IntPtr.Zero;
        private IntPtr _hInputWrite = IntPtr.Zero;
        private IntPtr _hOutputRead = IntPtr.Zero;
        private IntPtr _hOutputWrite = IntPtr.Zero;

        private readonly StringBuilder _outputDefaultBuffer;

        public Action<bool>? ProcessStateChanged;
        public Action<Tuple<string, string>>? ErrorHappens;
        public Action<string>? ProcessExited;
        public Action<string>? OutputAdded;

        private string Preffix = string.Empty;

        readonly Dictionary<string, string> errorMappings = new()
        {
            { "Error opening filter", "FILTER_OPEN_ERROR" },
            { "unknown option", "PARAMETER_ERROR" },
            { "hostlists load failed", "HOSTLIST_LOAD_ERROR" },
            { "must specify port filter", "PORT_FILTER_ERROR" },
            { "ERROR:", "UNKNOWN_ERROR" },
            { "Component not installed correctly", "COMPONENT_INSTALL_ERROR" },
            { "error", "UNKNOWN_ERROR" },
            { "invalid value", "INVALID_VALUE_ERROR" },
            { "nvalid value", "INVALID_VALUE_ERROR" },
            { "option requires an argument", "INVALID_VALUE_ERROR" },
            { "--debug=0|1|syslog|@<filename>", "PARAMETER_ERROR" },
            { "already running", "ALREADY_RUNNING_WARN" },
            { "could not read", "FILE_READ_ERROR" },
            { "flag provided but not defined:", "PARAMETER_ERROR" },
            { "cannot create", "ACCESS_DENIED" },
            { "cannot access", "ACCESS_DENIED" }
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
                    ProcessStateChanged?.Invoke(isRunned);
                    CurrentState = isRunned;
                    processState = CurrentState;
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

        public async void RunProcess(string exePath, string args, string workingDirectory)
        {
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
                ShowErrorMessage($"Unexpected error while trying to start process: {ex.Message}", _object: "console");
                SendStopMessage("Unexpected error happens while trying to stop process");

                processState = false;
                ChangeProcessState(false);
            }
            _processLock.Release();
        }

        public async void RunProcessWithConPTY(string exePath, string args, string workingDirectory, CancellationToken token)
        {
            await _processLock.WaitAsync();

            IntPtr pseudoConsoleHandle = IntPtr.Zero;
            IntPtr hInputRead = IntPtr.Zero;
            IntPtr hInputWrite = IntPtr.Zero;
            IntPtr hOutputRead = IntPtr.Zero;
            IntPtr hOutputWrite = IntPtr.Zero;

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

                using (var reader = new FileStream(safeOutputReadHandle, FileAccess.Read))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;

                    while (!token.IsCancellationRequested)
                    {
                        bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken: token);
                        if (bytesRead <= 0) break;

                        string _output = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        bool flag = false;
                        foreach (var errorMapping in errorMappings)
                        {
                            if (_output.IndexOf(errorMapping.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                ShowErrorMessage(errorMapping.Value);
                                StopProcessAfterDelay();
                                flag = true;
                                break; 
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

                        _outputDefaultBuffer.Append(_output);

                        OutputAdded?.Invoke(_output);

                        if (flag) break;
                    }
                }

                TerminateProcess(pi.hProcess, 0);

                WaitForSingleObject(pi.hProcess, INFINITE);

                CloseHandle(pi.hProcess);
            }
            catch (OperationCanceledException ex)
            {
                // pass
            }
            catch (Exception ex)
            {
                if (ex.Message != "External component has thrown an exception.")
                    ShowErrorMessage($"{ex.Message}", _object: "console");

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

        public async Task StopProcess(bool output = true)
        {
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
            catch (Exception ex)
            {
                processState = false;
                ChangeProcessState(processState);
            }
            _processLock.Release();
        }

        private async void StopProcessAfterDelay()
        {
            await Task.Delay(1000);
            await StopProcess(false);
        }

        private void SendStopMessage(string output = "Process will be stopped by user")
        {
            _outputDefaultBuffer.Append($"\n[PSEUDOCONSOLE] {output}");

            ProcessExited?.Invoke($"\n[PSEUDOCONSOLE] {output}");
        }

        private void ShowErrorMessage(string message, string _object = "process")
        {
            Logger.Instance.CreateWarningLog(nameof(ProcessManager), $"CONPTY error: {message} object: {_object}");
            ErrorHappens?.Invoke(Tuple.Create(message, _object));
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

        private void CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, bool bInheritHandle)
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
