using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI_TrayIcon.Helper
{
    public class ProcessManager
    {
        private static ProcessManager? _instance;
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

        private bool useProxy = false;
        private string proxyType = string.Empty;
        private string proxiFyreDir = string.Empty;
        private string _ip = string.Empty;
        private string _port = string.Empty;

        private string? Executable;
        private string? Args;

        public Action<bool>? ProcessStateChanged;
        public string ProcessName = string.Empty;

        
        private ConPTYHelper conPTYHelper;
        private ConPTYHelper proxiFyreHelper;
        

        public ProcessManager()
        {
            conPTYHelper = new ConPTYHelper();
            conPTYHelper.ProcessExited += SendStopMessage;
            conPTYHelper.OutputAdded += SendOutput;
            conPTYHelper.ErrorHappens += ShowErrorMessage;
            conPTYHelper.ProcessStateChanged += ChangeProcessState;

            proxiFyreHelper = new ConPTYHelper("PROXIFYRE");
            proxiFyreHelper.ProcessExited += PFSendStopMessage;
            proxiFyreHelper.OutputAdded += PFSendOutput;
            proxiFyreHelper.ErrorHappens += PFShowErrorMessage;
            proxiFyreHelper.ProcessStateChanged += PFChangeProcessState;
        }       

        public bool IsProcessInfoChanged = false;



        public void InitProxy(string path)
        {
            proxiFyreDir = path;
        }

        public void CleanProxy()
        {
            proxyType = string.Empty;
            proxiFyreDir = string.Empty;
            useProxy = false;   
        }

        public async void StartProxy(string _proxyType, string ip, string port)
        {
            _ip = ip;
            _port = port;
            proxyType = _proxyType;
            Debug.WriteLine($"Proxy setup for {proxyType}, {ip}, {port}");
            if (!string.IsNullOrEmpty(proxyType))
            {
                if (proxyType == "AllSystem")
                {
                    StartSystemProxy(ip, port);
                }
                else if (proxyType == "ProxiFyre")
                {
                    proxiFyreHelper.RunProcess(proxiFyreDir, string.Empty, Path.GetDirectoryName(proxiFyreDir)!);
                }
            }
            await Task.CompletedTask;
        }

        public void StopProxy()
        {
            if (proxyType == "AllSystem")
                StopSystemProxy();

            if (proxiFyreHelper.processState)
                _ = proxiFyreHelper.StopProcess();
        }

        public async Task StartProcess()
        {
            if (useProxy)
                StartProxy(proxyType, _ip, _port);

            if (Executable != null && Args != null && !IsProcessInfoChanged)
            {
                await StartProcess(Executable, Args);
            }
            else
            {
                if (!await PipeServer.Instance.SendMessage("CONPTY:GET_STARTUP_STRING"))
                {
                    RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), "--create-no-window --get-startup-params --exit-after-action");
                }
            }
        }

        public async Task StartProcess(string executable, string args)
        {
            IsProcessInfoChanged = false;
            Executable = executable;
            Args = args;
            
            var exePath = executable;
            var workingDirectory = Path.GetDirectoryName(exePath);

            ProcessName = Path.GetFileName(exePath);
            await PipeServer.Instance.SendMessage("CONPTY:CLEAN");
            await PipeServer.Instance.SendMessage("CONPTY:STARTED");

            SendNowSelectedComponentName();

            conPTYHelper.RunProcess(exePath, args, workingDirectory?? "");
        }

        public void SendNowSelectedComponentName()
        {
            _ = PipeServer.Instance.SendMessage($"CONPTY:PROCNAME({ProcessName})");
        }

       

        public async Task StopProcess(bool output = true)
        {
            StopProxy();
            await conPTYHelper.StopProcess(output);

            await Task.CompletedTask;
        }

        public async Task RestartProcess()
        {
            await StopProcess();
            await Task.Delay(1000);
            await StartProcess();
        }

        public async Task StopService()
        {
            Console.WriteLine("STOP SERVICE");
            await StopProcess();
            Console.WriteLine("STOP SERVICE2");
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

                if (error != "NaN") Logger.Instance.CreateErrorLog(nameof(ProcessManager), $"Two or more errors occurred\n" +
                    $"[1/2]\n" +
                    $"{error}" +
                    $"[2/2]\n" +
                    $"{ex.Message}");
            }

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
                    Console.WriteLine($"Unexpected error (Execute Command):\n{output}");
                }
                else
                {
                    Console.WriteLine("Success (Execute Command)");
                }
            }
        }

        public void SendDefaultProcessOutput()
        {
            _ = PipeServer.Instance.SendMessage($"CONPTY:FULLOUTPUT({conPTYHelper.GetDefaultOutput()})");
        }
        public bool GetState()
        {
            return conPTYHelper.processState;
        }
        public void SendState()
        {
            if (conPTYHelper.processState)
            {
                _ = PipeServer.Instance.SendMessage("CONPTY:STARTED");
            }
            else
            {
                _ = PipeServer.Instance.SendMessage("CONPTY:STOPPED");
            }
        }

        private void StartSystemProxy(string ip, string port)
        {
            string proxyServer = string.Empty;
            IPAddress address;
            if (IPAddress.TryParse(ip, out address))
            {
                proxyServer = address.AddressFamily switch
                {
                    System.Net.Sockets.AddressFamily.InterNetwork => $"socks={ip}:{port}",
                    System.Net.Sockets.AddressFamily.InterNetworkV6 => $"socks=[{ip}]:{port}",
                    _ => "",
                };
            }

            if (string.IsNullOrEmpty(proxyServer))
            {
                ShowErrorMessage(Tuple.Create($"Internal error -> IP_INCORRECT", nameof(StartSystemProxy)));
                return;
            }
            try
            {
                RegeditHelper.SaveProxySettings(proxyServer, string.Empty, 1);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(Tuple.Create($"Internal error -> {ex.Message}", nameof(RegeditHelper)));
                Logger.Instance.CreateErrorLog(nameof(ProcessManager), $"{ex.Message}");
            }
        }

        private void StopSystemProxy()
        {
            try
            {
                RegeditHelper.SaveProxySettings(string.Empty, string.Empty, 0);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(Tuple.Create($"Internal error -> {ex.Message}", nameof(RegeditHelper)));
                Logger.Instance.CreateErrorLog(nameof(ProcessManager), $"{ex.Message}");
            }
        }

        #region MessageHandler
        private void SendStopMessage(string output = "Process will be stopped by user")
        {
            _ = PipeServer.Instance.SendMessage($"CONPTY:MESSAGE({output})");
            StopProxy();
        }
        private void SendOutput(string output)
        {
            _ = PipeServer.Instance.SendMessage($"CONPTY:MESSAGE({output})");
        }
        private void ChangeProcessState(bool isRunned)
        {
            ProcessStateChanged?.Invoke(isRunned);
            Debug.WriteLine($"Process state is {isRunned}");
            if (isRunned == false) StopProxy();
        }
        private void ShowErrorMessage(Tuple<string, string> tuple)
        {
            string message = tuple.Item1;
            string _object = tuple.Item2;
            _ = PipeServer.Instance.SendMessage($"CONPTY:STOPPED({message}$SEPARATOR{_object})");
            StopProxy();
        }

        private void PFSendStopMessage(string output = "Process will be stopped by user")
        {
            _ = PipeServer.Instance.SendMessage($"CONPTY:MESSAGE({output})");
            _ = conPTYHelper.StopProcess();
        }
        private void PFSendOutput(string output)
        {
            _ = PipeServer.Instance.SendMessage($"CONPTY:MESSAGE({output})");
        }
        private void PFChangeProcessState(bool isRunned)
        {
            ProcessStateChanged?.Invoke(isRunned);
            if (isRunned == false) _ = conPTYHelper.StopProcess();
        }
        private void PFShowErrorMessage(Tuple<string, string> tuple)
        {
            string message = tuple.Item1;
            string _object = tuple.Item2;
            _ = PipeServer.Instance.SendMessage($"CONPTY:STOPPED({message}$SEPARATOR{_object})");
            _ = conPTYHelper.StopProcess();
        }

        #endregion

        #region WINAPI

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern int GetSystemDefaultLCID();

        #endregion
    }
}
