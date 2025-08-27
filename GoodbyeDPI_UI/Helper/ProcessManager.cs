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
using System.Windows.Documents;
using Microsoft.UI.Dispatching;
using Microsoft.Win32.SafeHandles;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Devices.Power;
using GoodbyeDPI_UI;
using GoodbyeDPI_UI.Helper.Static;

namespace GoodbyeDPI_UI.Helper
{
    public class ProcessManager
    {
        private static ProcessManager _instance;
        private static readonly object _lock = new object();

        public static ProcessManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ProcessManager();
                    return _instance;
                }
            }
        }

        private CancellationTokenSource _cancellationTokenSource;
        private PROCESS_INFORMATION _processInfo;
        private IntPtr _pseudoConsoleHandle = IntPtr.Zero;
        private IntPtr _hInputRead = IntPtr.Zero;
        private IntPtr _hInputWrite = IntPtr.Zero;
        private IntPtr _hOutputRead = IntPtr.Zero;
        private IntPtr _hOutputWrite = IntPtr.Zero;

        public event Action<string> OutputReceived;
        public event Action<string, string> ErrorHappens;
        public event Action<string> onProcessStateChanged;

        public bool isErrorHappens = false;
        public List<string> LatestErrorMessage = ["", ""];

        public bool processState = false;
        private string ProcessName = string.Empty;

        private readonly DispatcherQueue _dispatcherQueue;

        private readonly StringBuilder _outputBuffer;
        private readonly StringBuilder _outputDefaultBuffer;

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
            { "--debug=0|1|syslog|@<filename>", "PARAMETER_ERROR" },
            { "already running", "ALREADY_RUNNING_WARN" },
            { "could not read", "FILE_READ_ERROR" }
            
        };

        private ProcessManager()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _outputBuffer = new StringBuilder();
            _outputDefaultBuffer = new StringBuilder();
        }

        public async Task StartProcess()
        {
            isErrorHappens = false;
            LatestErrorMessage.Clear();
            try
            {
                if (_processInfo.hProcess != IntPtr.Zero)
                {
                    return;
                }

                _outputBuffer.Clear();
                _outputDefaultBuffer.Clear();

                Items.ComponentItemsLoaderHelper.Instance.Init();
                Items.ComponentHelper componentHelper = 
                    Items.ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(SettingsManager.Instance.GetValue<string>("COMPONENTS", "nowUsed"));

                ProcessName = Utils.FirstCharToUpper(DatabaseHelper.Instance.GetItemById(SettingsManager.Instance.GetValue<string>("COMPONENTS", "nowUsed")).Name);

                var exePath = componentHelper.GetExecutablePath();
                var workingDirectory = componentHelper.GetDirectory();
                string args = componentHelper.GetStartupParams();

                Logger.Instance.CreateDebugLog(nameof(ProcessManager), $"Args is {args}");

                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                onProcessStateChanged?.Invoke("started");
                processState = true;

                await Task.Run(() => RunProcessWithConPTY(exePath, args, workingDirectory, token));
                
            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"Unexpected error while trying to start process: {ex.Message}", _object: "console");
                SendStopMessage("Unexpected error happens while trying to stop process");
                processState = false;
            }
        }
        public async Task StartProcess(string componentId, string args)
        {
            isErrorHappens = false;
            LatestErrorMessage.Clear();
            try
            {
                if (_processInfo.hProcess != IntPtr.Zero)
                {
                    return;
                }

                _outputBuffer.Clear();
                _outputDefaultBuffer.Clear();

                Items.ComponentItemsLoaderHelper.Instance.Init();
                Items.ComponentHelper componentHelper =
                    Items.ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(componentId);

                ProcessName = Utils.FirstCharToUpper(DatabaseHelper.Instance.GetItemById(componentId).Name);

                var exePath = componentHelper.GetExecutablePath();
                var workingDirectory = componentHelper.GetDirectory();

                Logger.Instance.CreateDebugLog(nameof(ProcessManager), $"Args is {args}");

                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                onProcessStateChanged?.Invoke("started");
                processState = true;

                await Task.Run(() => RunProcessWithConPTY(exePath, args, workingDirectory, token));

            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"Unexpected error while trying to start process: {ex.Message}", _object: "console");
                SendStopMessage("Unexpected error happens while trying to stop process");
                processState = false;
            }
        }

        public string GetNowSelectedComponentName()
        {
            return ProcessName;
        }

        private void SendStopMessage(string output = "Process will be stopped by user")
        {
            _outputDefaultBuffer.Append($"\n[PSEUDOCONSOLE]{output}");
            _outputBuffer.Append($"\n[PSEUDOCONSOLE]{output}");

            OutputReceived?.Invoke($"\n[PSEUDOCONSOLE]{output}");

        }

        public async Task StopProcess(bool output = true)
        {
            try
            {
                _cancellationTokenSource?.Cancel();

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
                if (output) onProcessStateChanged?.Invoke("stopped");
                processState = false;
                //SendStopMessage();
            }
            catch (Exception ex)
            {
                //await ShowErrorMessage($"Unable to stop process: {ex.Message}", _object: "console");
                processState = false;
                
            }
            await Task.CompletedTask;
        }

        public async Task RestartProcess()
        {
            await StopProcess();
            await StartProcess();
        }

        public async Task StopService()
        {
            await StopProcess();
            string error = "NaN";
            try
            {
                await ExecuteCommand("sc", "stop WinDivert");
            } 
            catch (Exception ex) 
            { 
                error = ex.Message; 
            }
            try
            {
                await ExecuteCommand("sc", "delete WinDivert");
            } 
            catch (Exception ex)
            {

                if (error != "NaN") throw new Exception($"Two or more errors occurred\n" +
                    $"[1/2]\n" +
                    $"{error}" +
                    $"[2/2]\n" +
                    $"{ex.Message}");
            }
            
        }

        private async void StopProcessAfterDelay()
        {
            await Task.Delay(1000);
            await StopProcess(false);
        }

        private async Task ExecuteCommand(string fileName, string arguments)
        {
            using (Process process = new Process())
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                int lcid = GetSystemDefaultLCID();
                var ci = System.Globalization.CultureInfo.GetCultureInfo(lcid);
                var page = ci.TextInfo.OEMCodePage;

                process.StartInfo.FileName = fileName;
                process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(page);
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Unexpected error (Execute Command):\n{output}");
                }
                else
                {
                    Debug.WriteLine("Success (Execute Command)");
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern int GetSystemDefaultLCID();

        public string GetDefaultProcessOutput()
        {
            return _outputDefaultBuffer.ToString();
        }
        public string GetProcessOutput()
        {
            return _outputBuffer.ToString();
        }

        private string ReplaceSymbols(string str)
        {
            str = str.Replace("[?25l\u001b[2J\u001b[m\u001b[H", "");
            str = str.Replace("[4;1H", "\n");
            str = Regex.Replace(str, @"\u001b\]0;.*?\[\?25h", "");
            str = Regex.Replace(str, @"\[\?25l|\[1C|", "");
            str = Regex.Replace(str, @"\[\?\d{4}\w", "");
            return str;

        }

        private void RunProcessWithConPTY(string exePath, string args, string workingDirectory, CancellationToken token)
        {
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

                    while (!token.IsCancellationRequested && (bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        string _output = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        foreach (var errorMapping in errorMappings)
                        {
                            if (_output.IndexOf(errorMapping.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                _ = _dispatcherQueue.TryEnqueue(async () => await ShowErrorMessage(errorMapping.Value));
                                StopProcessAfterDelay();
                                break; 
                                
                            }
                        }
                        

                        _outputDefaultBuffer.Append(_output);

                        string output = ReplaceSymbols(_output);
                        _outputBuffer.Append(output);

                        if (!SettingsManager.Instance.GetValue<bool>("PSEUDOCONSOLE", "outputMode")) output = _output;


                        OutputReceived?.Invoke(output);
                    }
                }

                WaitForSingleObject(pi.hProcess, INFINITE);

                CloseHandle(pi.hProcess);

                SendStopMessage();
            }
            catch (Exception ex)
            {
                if (ex.Message != "External component has thrown an exception.")
                    _ = _dispatcherQueue.TryEnqueue(async () => await ShowErrorMessage($"{ex.Message}", _object:"console"));

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
                _processInfo = default;
            }
        }

        private async Task ShowErrorMessage(string message, string _object = "process")
        {
            Debug.WriteLine(message);
            isErrorHappens = true;

            await ((App)Application.Current).SafeCreateNewWindow<ViewWindow>();
            
            ErrorHappens.Invoke(message, _object);

            LatestErrorMessage.Clear();

            LatestErrorMessage.Add(message);
            LatestErrorMessage.Add(_object);

            await Task.CompletedTask;
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
            string lpApplicationName,
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

        #endregion
    }
}
