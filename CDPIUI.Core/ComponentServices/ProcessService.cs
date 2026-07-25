using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Shared.Extentions;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.ComponentsTask;
using CDPIUI.Shared.Exceptions.Database;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Proxy;
using CDPIUI.Core.ComponentServices.Helpers;

namespace CDPIUI.Core.ComponentServices
{
    public partial class ProcessService : IProcessService
    {
        public string Id { get; set; } = string.Empty;

        public event Action<string>? OutputReceived;
        public event Action<ErrorModel>? ErrorHappens;
        public event Action<string>? ProcessNameChanged;
        public event Action<Tuple<string, bool>>? ProcessStateChanged;

        public event Action<string>? ShowErrorMessageWindow;

        public bool IsProcessRunning { get; private set; } = false;
        public string ProcessName { get; private set; } = string.Empty;

        public bool IsErrorHappens { get; private set; } = false;
        public ErrorModel? LastError { get; private set; }

        private readonly StringBuilder _outputBuffer;
        private readonly StringBuilder _outputDefaultBuffer;

        public ProcessService()
        {
            _outputBuffer = new StringBuilder();
            _outputDefaultBuffer = new StringBuilder();

            ProcessName = "sampleExecutableFile";
        }

        public async Task GetReady(bool all = true)
        {
            await PipeHelper.SendConPTYPacket(Shared.Pipe.Models.CONPTYMessageIds.GetProcessIdFullOutput, Id);
            await PipeHelper.SendConPTYPacket(Shared.Pipe.Models.CONPTYMessageIds.GetProcessIdState, Id);
        }

        public async Task RunActionsIfAutorunSelected()
        {
            bool isComponentAddedToAutorun = SettingsManager.Instance.GetValue<bool>(["CONFIGS", Id], "usedForAutorun");
            if (!DatabaseHelper.Instance.IsItemInstalled(Id))
            {
                SettingsManager.Instance.SetValue(["CONFIGS", Id], "usedForAutorun", false);
                return;
            }
            if (isComponentAddedToAutorun)
            {
                await StartProcess(exitAfterActionCheck: false);
            }
            await Task.CompletedTask;
        }

        public async Task StartProcess()
        {
            await StartProcess(true);
        }
        public async Task StartProcess(bool exitAfterActionCheck)
        {
            IsErrorHappens = false;
            LastError = null;
            try
            {
                _outputBuffer.Clear();
                _outputDefaultBuffer.Clear();

                ComponentItemsLoaderHelper.Instance.Init();
                ComponentHelper componentHelper =
                    ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(Id);

                ProcessName = DatabaseHelper.Instance.GetItemById(Id).Executable?.FirstCharToUpper() ?? string.Empty;

                var exePath = componentHelper.GetExecutablePath();
                var workingDirectory = componentHelper.GetDirectory();
                string args = SetupProxy(componentHelper.GetStartupParams(), Id);

                var component = HardcodedItemIds.ComponentIds.GetKeyByValue(Id);
                if (component == Components.NoDPI) args = args.Replace("--quiet", "") + "--quiet";

                Logger.Instance.CreateDebugLog(nameof(ProcessService), $"Args is {args}");

                // onProcessStateChanged?.Invoke(Tuple.Create(Id, true));
                IsProcessRunning = true;

                await PipeHelper.SendConPTYPacket(Shared.Pipe.Models.CONPTYMessageIds.StartProcessId, Id, exePath, args);
                string[] arguments = Environment.GetCommandLineArgs();

                if (arguments.Contains("--exit-after-action") && exitAfterActionCheck) Process.GetCurrentProcess().Kill(); // FIX: Possible issue when component not setted (Pseudoconsole internal error)
            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"Unexpected error while trying to start process: {ex.Message}", _object: "console");
                SendStopMessage("Unexpected error happens while trying to stop process");
                IsProcessRunning = false;
            }
        }
        [Obsolete("Use Core.ComponentServices.ComponentTaskManager.CreateAndRunNewTask instead")]
        public async Task StartProcess(string args, string? _reserved = null) // TODO: remove "Id" from method 
        {
            IsErrorHappens = false;
            LastError = null;
            try
            {
                _outputBuffer.Clear();
                _outputDefaultBuffer.Clear();

                ComponentItemsLoaderHelper.Instance.Init();
                ComponentHelper componentHelper =
                    ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(Id);

                ProcessName = DatabaseHelper.Instance.GetItemById(Id).Executable?.FirstCharToUpper() ?? Id;

                var exePath = componentHelper.GetExecutablePath();
                var workingDirectory = componentHelper.GetDirectory();
                args = SetupProxy(args, Id);

                var component = HardcodedItemIds.ComponentIds.GetKeyByValue(Id);
                if (component == Components.NoDPI) args = args.Replace("--quiet", "") + "--quiet";

                Logger.Instance.CreateDebugLog(nameof(ProcessService), $"Args is {args}");

                // onProcessStateChanged?.Invoke(Tuple.Create(Id, true));
                IsProcessRunning = true;

                _ = PipeHelper.SendConPTYPacket(Shared.Pipe.Models.CONPTYMessageIds.StartProcessId, Id, exePath, args);

            }
            catch (Exception ex)
            {
                var errorCode = ErrorsHelper.Convertor.MapExceptionToCode(ex, out var rawHResult);
                await ShowErrorMessage($"Unexpected error while trying to start process: ({errorCode}) {ex.Message}", _object: "console");
                SendStopMessage("Unexpected error happens while trying to stop process");
                IsProcessRunning = false;
            }
        }



        
        private string SetupProxy(string args, string componentId)
        {
            if (!ProxyHelper.ProxyLikeComponents.Contains(componentId))
            {
                _ = PipeHelper.SendProxyPacket(Shared.Pipe.Models.ProxyMessageIds.Clean, Id);
                return args;
            }

            string ip = SettingsManager.Instance.GetValue<string>("PROXY", "IPAddress");
            string port = SettingsManager.Instance.GetValue<string>("PROXY", "port");

            string proxyType = SettingsManager.Instance.GetValue<string>("PROXY", "proxyType");

            if (proxyType == ProxySetupTypes.ProxiFyre.ToString())
            {
                if (!DatabaseHelper.Instance.IsItemInstalled("ASPEWK002"))
                {
                    throw new AddonNotInstalledException();
                }

                _ = PipeHelper.SendProxyPacket(Shared.Pipe.Models.ProxyMessageIds.Init, Id, proxyFirePath: GetProxiFyrePath());
                _ = PipeHelper.SendProxyPacket(
                    Shared.Pipe.Models.ProxyMessageIds.Setup,
                    Id,
                    ProxySetupTypes.ProxiFyre.ToString(),
                    ip,
                    port);

                return ProxyHelper.ReplaceArgsForProxy(args, ip, port, componentId);
            }
            else if (proxyType == ProxySetupTypes.AllSystem.ToString())
            {
                _ = PipeHelper.SendProxyPacket(
                    Shared.Pipe.Models.ProxyMessageIds.Setup,
                    Id,
                    ProxySetupTypes.AllSystem.ToString(),
                    ip,
                    port);
                return ProxyHelper.ReplaceArgsForProxy(args, ip, port, componentId);
            }
            else if (proxyType == ProxySetupTypes.NoActions.ToString())
            {
                _ = PipeHelper.SendProxyPacket(Shared.Pipe.Models.ProxyMessageIds.Clean, Id);
                return ProxyHelper.ReplaceArgsForProxy(args, ip, port, componentId);
            }
            else
            {
                _ = PipeHelper.SendProxyPacket(Shared.Pipe.Models.ProxyMessageIds.Clean, Id);
                return args;
            }
        }

        private string GetProxiFyrePath()
        {
            var item = DatabaseHelper.Instance.GetItemById("ASPEWK002");
            if (item == null) return string.Empty;

            return Path.Combine(item.Directory, item.Executable + ".exe");
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
                _ = PipeHelper.SendConPTYPacket(Shared.Pipe.Models.CONPTYMessageIds.StopProcessId, Id);
                if (output) ProcessStateChanged?.Invoke(Tuple.Create(Id, false));
                IsProcessRunning = false;
            }
            catch (Exception ex)
            {
                IsProcessRunning = false;
            }
            await Task.CompletedTask;
        }

        public async Task RestartProcess()
        {
            _ = PipeHelper.SendConPTYPacket(Shared.Pipe.Models.CONPTYMessageIds.RestartProcessId, Id);
            await Task.CompletedTask;
        }

        public static async Task StopService()
        {
            _ = PipeHelper.SendConPTYPacket(Shared.Pipe.Models.CONPTYMessageIds.StopService);
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

        public static string ReplacePath(string str)
        {
            str = DirectoryReplaceRegex().Replace(str, "cdpi-ui://");
            return str;
        }



        public async Task ShowErrorMessage(string message, string _object = "process")
        {
            Debug.WriteLine(message);
            IsErrorHappens = true;

            LastError = null;

            LastError = new()
            {
                ErrorCode = message,
                Object = _object
            };

            ShowErrorMessageWindow?.Invoke(Id);

            IsProcessRunning = false;
            ProcessStateChanged?.Invoke(Tuple.Create(Id, false));

            ErrorHappens?.Invoke(LastError);

            await Task.CompletedTask;
        }

        public void MarkAsStarted()
        {
            IsProcessRunning = true;
            ProcessStateChanged?.Invoke(Tuple.Create(Id, true));
        }

        public void MarkAsFinished()
        {
            IsProcessRunning = false;
            if (!IsErrorHappens)
            {
                ProcessStateChanged?.Invoke(Tuple.Create(Id, false));
            }
            else
            {
                if (LastError != null)
                    ErrorHappens?.Invoke(LastError);
            }
        }
        public void ChangeProcName(string name)
        {
            ProcessName = name;
            ProcessNameChanged?.Invoke(name);
        }
        public void AddOutput(string output)
        {
            _outputDefaultBuffer.Append(output);
            string prettyOutput = OutputCleanupHelper.ReplaceSymbols(output);
            _outputBuffer.Append(prettyOutput);

            OutputReceived?.Invoke(prettyOutput);
        }
        public void ClearOutput()
        {
            _outputDefaultBuffer.Clear();
            _outputBuffer.Clear();
        }

        [GeneratedRegex(@"(?:[a-zA-Z]):\\.*?/", RegexOptions.Singleline)]
        private static partial Regex DirectoryReplaceRegex();
    }
}
