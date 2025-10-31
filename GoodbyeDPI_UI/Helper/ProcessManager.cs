﻿using System;
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
using CDPI_UI;
using CDPI_UI.Helper.Static;

namespace CDPI_UI.Helper
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

        public event Action<string> OutputReceived;
        public event Action<string, string> ErrorHappens;
        public event Action<string> onProcessStateChanged;
        public event Action<string> ProcessNameChanged;

        public bool isErrorHappens = false;
        public List<string> LatestErrorMessage = ["", ""];

        public bool processState = false;
        public string ProcessName = string.Empty;

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
            { "could not read", "FILE_READ_ERROR" },
            { "flag provided but not defined:", "PARAMETER_ERROR" }
            
        };

        private ProcessManager()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _outputBuffer = new StringBuilder();
            _outputDefaultBuffer = new StringBuilder();
        }

        public void GetReady()
        {
            _ = PipeClient.Instance.SendMessage("CONPTY:GETOUTPUT");
            _ = PipeClient.Instance.SendMessage("CONPTY:GETSTATE");
        }

        public async Task StartProcess()
        {
            isErrorHappens = false;
            LatestErrorMessage.Clear();
            try
            {
                _outputBuffer.Clear();
                _outputDefaultBuffer.Clear();

                string componentId = SettingsManager.Instance.GetValue<string>("COMPONENTS", "nowUsed");

                Items.ComponentItemsLoaderHelper.Instance.Init();
                Items.ComponentHelper componentHelper = 
                    Items.ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(componentId);

                ProcessName = Utils.FirstCharToUpper(DatabaseHelper.Instance.GetItemById(componentId).Executable);

                var exePath = componentHelper.GetExecutablePath();
                var workingDirectory = componentHelper.GetDirectory();
                string args = SetupProxy(componentHelper.GetStartupParams(), componentId);

                Logger.Instance.CreateDebugLog(nameof(ProcessManager), $"Args is {args}");

                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                onProcessStateChanged?.Invoke("started");
                processState = true;

                await PipeClient.Instance.SendMessage($"CONPTY:START({exePath}$SEPARATOR{args})");
                string[] arguments = Environment.GetCommandLineArgs();

                if (arguments.Contains("--exit-after-action")) Process.GetCurrentProcess().Kill(); // FIX: Possible issue when component not setted (Pseudoconsole internal error)
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

                _ = PipeClient.Instance.SendMessage($"CONPTY:START({exePath}$SEPARATOR{args})");

            }
            catch (Exception ex)
            {
                var errorCode = ErrorsHelper.ErrorHelper.MapExceptionToCode(ex, out var rawHResult);
                await ShowErrorMessage($"Unexpected error while trying to start process: ({errorCode}) {ex.Message}", _object: "console");
                SendStopMessage("Unexpected error happens while trying to stop process");
                processState = false;
            }
        }

        

        private string ReplaceArgsForProxy(string args, string ip, string port, string componentId)
        {
            string finalArgs = Utils.ReplaseIp(args);
            if (componentId == "CSSIXC048")
                finalArgs = $"-addr={ip} -port={port} " + finalArgs;
            else
                finalArgs = $"--ip={ip} --port={port} " + finalArgs;
            return finalArgs;

        }
        private string SetupProxy(string args, string componentId)
        {
            if (!StateHelper.ProxyLikeComponents.Contains(componentId))
            {
                _ = PipeClient.Instance.SendMessage("PROXY:CLEAN");
                return args;
            }

            string ip = SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress");
            string port = SettingsManager.Instance.GetValue<string>("PROXY", "port");

            string proxyType = SettingsManager.Instance.GetValue<string>("PROXY", "proxyType");

            if (proxyType == StateHelper.ProxySetupTypes.ProxiFyre.ToString())
            {
                if (!DatabaseHelper.Instance.IsItemInstalled("ASPEWK002"))
                {
                    throw new AddonNotInstalledException();
                }

                _ = PipeClient.Instance.SendMessage($"PROXY:INIT({GetProxiFyrePath()})");
                _ = PipeClient.Instance.SendMessage($"PROXY:SETUP({StateHelper.ProxySetupTypes.ProxiFyre.ToString()}$SEPARATOR{ip}$SEPARATOR{port})");
                return ReplaceArgsForProxy(args, ip, port, componentId);
            }
            else if (proxyType == StateHelper.ProxySetupTypes.AllSystem.ToString())
            {
                _ = PipeClient.Instance.SendMessage($"PROXY:SETUP({StateHelper.ProxySetupTypes.AllSystem.ToString()}$SEPARATOR{ip}$SEPARATOR{port})");
                return ReplaceArgsForProxy(args, ip, port, componentId);
            }
            else if (proxyType == StateHelper.ProxySetupTypes.NoActions.ToString())
            {
                _ = PipeClient.Instance.SendMessage("PROXY:CLEAN");
                return ReplaceArgsForProxy(args, ip, port, SettingsManager.Instance.GetValue<string>("COMPONENTS", "nowUsed"));
            }
            else
            {
                _ = PipeClient.Instance.SendMessage("PROXY:CLEAN");
                return args;
            }
        }

        private string GetProxiFyrePath()
        {
            var item = DatabaseHelper.Instance.GetItemById("ASPEWK002");
            if (item == null) return string.Empty;

            return Path.Combine(item.Directory, item.Executable + ".exe");
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

                _ = PipeClient.Instance.SendMessage("CONPTY:STOP");
                if (output) onProcessStateChanged?.Invoke("stopped");
                processState = false;
            }
            catch (Exception ex)
            {
                processState = false;
            }
            await Task.CompletedTask;
        }

        public async Task RestartProcess()
        {
            _ = PipeClient.Instance.SendMessage("CONPTY:RESTART");
            await Task.CompletedTask;
        }

        public async Task StopService()
        {
            _ = PipeClient.Instance.SendMessage("CONPTY:STOPSERVICE");
            await Task.CompletedTask;
        }

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
            str = str.Replace("[12;1H", "\n");
            str = str.Replace("[32m", "\n");
            str = str.Replace("[90m", "\n");
            str = Regex.Replace(str, @"\u001b\]0;.*?\[\?25h", "");
            str = Regex.Replace(str, @"\[\?25l|\[1C|", "");
            str = Regex.Replace(str, @"\[\?\d{4}\w", "");
            str = Regex.Replace(str, @"\[\d[A-Z]", "");
            str = Regex.Replace(str, @"\[\d{1,2};\d{1,2}[A-Z]", "");
            str = Regex.Replace(str, @"\[\?\d{1,2}[a-z]", "");
            str = Regex.Replace(str, @"(\[\d{0,2}m)?(\[H)?", "");
            str = str.Replace("]0;", "");
            return str;

        }

        public async Task ShowErrorMessage(string message, string _object = "process")
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

        public void MarkAsStarted()
        {
            processState = true;
            onProcessStateChanged?.Invoke("started");
        }

        public void MarkAsFinished()
        {
            processState = false;
            onProcessStateChanged?.Invoke("stopped");
        }
        public void ChangeProcName(string name)
        {
            ProcessName = name;
            ProcessNameChanged?.Invoke(name);
        }
        public void AddOutput(string output)
        {
            _outputDefaultBuffer.Append(output);
            string prettyOutput = ReplaceSymbols(output);
            _outputBuffer.Append(prettyOutput);

            OutputReceived?.Invoke(prettyOutput);
        }
        public void ClearOutput()
        {
            _outputDefaultBuffer.Clear();
            _outputBuffer.Clear();
        }
    }
}
