using CDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using CDPI_UI.Helper.Static;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CDPI_UI;
using static CDPI_UI.Helper.MsiInstallerHelper;

namespace CDPI_UI.Helper
{
    public class PipeClient
    {
        private NamedPipeClientStream _pipeClient;
        private StreamString _streamString;

        private static CancellationTokenSource _cancellationTokenSource = new();
        private static CancellationToken _cancellationToken = _cancellationTokenSource.Token;

        public Action Connected;
        public bool IsConnected = false;

        private static PipeClient? _instance;
        private static readonly object _lock = new object();

        public static PipeClient Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new PipeClient();
                    return _instance;
                }
            }
        }

        public PipeClient()
        {
            SettingsManager.Instance.PropertyChanged += SettingsManager_PropertyChanged;
            SettingsManager.Instance.EnumPropertyChanged += SettingsManager_EnumPropertyChanged;
        }

        private void SettingsManager_PropertyChanged(string key)
        {
            if (key == "COMPONENTS")
                _ = SendMessage("CONPTY:PROCESSCHANGED");
        }

        private void SettingsManager_EnumPropertyChanged(IEnumerable<string> _enum)
        {
            foreach (var group in _enum)
            {
                if (group == "CONFIGS")
                {
                    _ = SendMessage("CONPTY:PROCESSCHANGED");
                    return;
                }
            }
        }

        public void Init(string pipeName = "{C9253A32-C9BB-496F-A700-43268B370236}")
        {
            _pipeClient = new NamedPipeClientStream(".", pipeName,
                            PipeDirection.InOut, PipeOptions.Asynchronous,
                            TokenImpersonationLevel.Impersonation);
        }

        public async void Start()
        {
            if (_pipeClient == null)
            {
                throw new NullReferenceException("Call PipeClient.Instanse.Init first");
            }
            _cancellationTokenSource.CancelAfter(2000);
            try
            {
                await _pipeClient.ConnectAsync(_cancellationToken);
            }
            catch { }

            if (!_pipeClient.IsConnected)
            {
                try
                {
                    string startupString = SettingsManager.Instance.GetValue<bool>("APPEARANCE", "hideToTrayOnStartup") ? "--autorun" : "--show-ui";
                    var psi = new ProcessStartInfo(Path.Combine(StateHelper.GetDataDirectory(getCurrent:true), "CDPIUI_TrayIcon.exe"), startupString)
                    {
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                }
                catch { }
                Logger.Instance.CreateErrorLog(nameof(PipeClient), "Connection timeout");


                Process.GetCurrentProcess().Kill();
                return;
            }

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new();
            _cancellationToken = _cancellationTokenSource.Token;

            

            await HandleServerAsync(_cancellationToken);
            
        }

        private async Task HandleServerAsync(CancellationToken token)
        {
            _streamString = new StreamString(_pipeClient);

            try
            {
                _ = SendMessage($"PIPE:CONNECTED({Secret.AuthGuid})");
                while (_pipeClient.IsConnected && !token.IsCancellationRequested)
                {
                    string message;
                    try
                    {
                        message = await _streamString.ReadStringAsync();
                        RunMessageActions(message);
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }

                    if (string.IsNullOrEmpty(message))
                    {

                        continue;
                    }

                    Console.WriteLine($"Received: {message}");

                }
            }
            catch (IOException e)
            {
                Console.WriteLine($"ERROR: {e.Message}");
            }
            finally
            {
                try
                {
                    if (_pipeClient.IsConnected)
                    {
                        _pipeClient.WaitForPipeDrain();
                        _pipeClient.Close();
                    }
                }
                catch { }

                _pipeClient.Dispose();
                Logger.Instance.RaiseCriticalException(
                    nameof(PipeClient), 
                    "ERR_PROCESS_DIED", 
                    "One of application process is died by unknown reason. Application can't restart this process, because ERR_ACCESS_DENIED happens. Please, restart app manually."
                );
            }
        }

        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public async Task SendMessage(string message)
        {
            Debug.WriteLine(message);
            await _sendLock.WaitAsync();
            Debug.WriteLine($"RELEASE LOCK");

            if (_pipeClient == null && !_pipeClient.IsConnected && _streamString == null)
            {
                return;
            }
            try
            {
                await _streamString.WriteStringAsync(message);
            }
            finally
            {
                _sendLock.Release();
            }

        }
        private void RunMessageActions(string message)
        {
            Logger.Instance.CreateDebugLog(nameof(PipeClient), message);

            if (message.StartsWith("PIPE:"))
            {
                if (message.StartsWith("PIPE:AUTH_OK"))
                {
                    IsConnected = true;
                    Connected?.Invoke();
                }
            }
            else if (message.StartsWith("MAIN:"))
            {
                switch (message)
                {
                    case "MAIN:EXIT_ALL":
                        Process.GetCurrentProcess().Kill();
                        break;
                    default:
                        break;
                }
            }
            else if (message.StartsWith("WINDOW:"))
            {
                switch (message)
                {
                    case "WINDOW:SHOW_MAIN":
                        _ = ((App)Microsoft.UI.Xaml.Application.Current).SafeCreateNewWindow<MainWindow>();
                        break;
                    case "WINDOW:SHOW_PSEUDOCONSOLE":
                        _ = ((App)Microsoft.UI.Xaml.Application.Current).SafeCreateNewWindow<ViewWindow>();
                        break;
                    case "WINDOW:SHOW_PROXY_SETUP":
                        _ = ((App)Microsoft.UI.Xaml.Application.Current).SafeCreateNewWindow<ProxySetupUtilWindow>();
                        break;
                    case "WINDOW:SHOW_STORE":
                        _ = ((App)Microsoft.UI.Xaml.Application.Current).SafeCreateNewWindow<StoreWindow>();
                        break;
                    case "WINDOW:SHOW_MAIN:UPDATE_PAGE":
                        _ = ((App)Microsoft.UI.Xaml.Application.Current).NavigateToUpdatesPage();
                        break;
                    default:
                        break;
                }
            }
            else if (message.StartsWith("CONPTY:"))
            {
                if (message.StartsWith("CONPTY:GET_STARTUP_STRING"))
                {
                    _ = ProcessManager.Instance.StartProcess();
                }
                else if (message.StartsWith("CONPTY:CLEAN"))
                {
                    ProcessManager.Instance.ClearOutput();
                }
                else if (message.StartsWith("CONPTY:STARTED"))
                {
                    ProcessManager.Instance.MarkAsStarted();
                }
                else if (message.StartsWith("CONPTY:STOPPED"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length == 2)
                    {
                        _ = ProcessManager.Instance.ShowErrorMessage(result[0], result[1]);
                    }
                    ProcessManager.Instance.MarkAsFinished();
                }
                else if (message.StartsWith("CONPTY:PROCNAME"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 1)
                    {
                        Logger.Instance.CreateWarningLog(nameof(PipeClient), $"ERR, {message} => args exception");
                        return;
                    }
                    ProcessManager.Instance.ChangeProcName(result[0]);
                }
                else if (message.StartsWith("CONPTY:MESSAGE"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 1)
                    {
                        Logger.Instance.CreateWarningLog(nameof(PipeClient), $"ERR, {message} => args exception");
                        return;
                    }
                    ProcessManager.Instance.AddOutput(result[0]);
                }
                else if (message.StartsWith("CONPTY:FULLOUTPUT"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 1)
                    {
                        Logger.Instance.CreateWarningLog(nameof(PipeClient), $"ERR, {message} => args exception");
                        return;
                    }
                    ProcessManager.Instance.ClearOutput();
                    ProcessManager.Instance.AddOutput(result[0]);
                }

            }
            else if (message.StartsWith("GOODCHECK:"))
            {
                if (message.StartsWith("GOODCHECK:RUNNED"))
                {

                }
                else if (message.StartsWith("GOODCHECK:DIED"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 1)
                    {
                        Logger.Instance.CreateWarningLog(nameof(PipeClient), $"ERR, {message} => args exception");
                        return;
                    }
                    if (int.TryParse(result[0], out var value))
                        GoodCheckProcessHelper.Instance.OperationWithIdDied(value);
                }
                else if (message.StartsWith("GOODCHECK:DIEDVIAERR"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 1)
                    {
                        Logger.Instance.CreateWarningLog(nameof(PipeClient), $"ERR, {message} => args exception");
                        return;
                    }
                    if (int.TryParse(result[1], out var value))
                        GoodCheckProcessHelper.Instance.HandleProcessException(result[0], value);
                }
            }
            else if (message.StartsWith("SETTINGS:"))
            {
                if (message.StartsWith("SETTINGS:AUTORUN_FALSE"))
                {
                    SettingsManager.Instance.SetValue<bool>("SYSTEM", "autorun", false);

                }
            }
            else if (message.StartsWith("UPDATE:"))
            {
                if (message.StartsWith("UPDATE:CHECK"))
                {
                    _ = ApplicationUpdateHelper.Instance.CheckForUpdates(notify: true);
                }
            }
            else if (message.StartsWith("MSI:"))
            {
                if (message.StartsWith("MSI:SETSTATUS"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 2)
                    {
                        Logger.Instance.CreateWarningLog(nameof(PipeClient), $"ERR, {message} => args exception");
                        return;
                    }
                    if (int.TryParse(result[1], out var value))
                        GetMsiInstallerMessage(result[0], (MsiState)value);
                }
                else if (message.StartsWith("MSI:REMOVED"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 1)
                    {
                        Logger.Instance.CreateWarningLog(nameof(PipeClient), $"ERR, {message} => args exception");
                        return;
                    }
                    RemoveMsiInstallerModel(result[0], notify:false);
                }
            }
        }
        private class MsiInstallerModel
        {
            public string OperationId { get; set; }
            public MsiInstallerHelper MsiInstallerHelper { get; set; }
        }

        private List<MsiInstallerModel> installerModels = [];

        public void SendMsiInstallMessage(string operationId, string filename, MsiInstallerHelper installerHelper)
        {
            installerModels.Add(new MsiInstallerModel
            {
                OperationId = operationId,
                MsiInstallerHelper = installerHelper
            });
            _ = SendMessage($"MSI:BEGIN({operationId}$SEPARATOR{filename})");
        }

        public void RemoveMsiInstallerModel(string operationId, bool notify=true)
        {
            MsiInstallerModel msiInstallerModel = installerModels.FirstOrDefault(i => i.OperationId == operationId);
            if (msiInstallerModel != null)
            {
                installerModels.Remove(msiInstallerModel);
                if (notify) _ = SendMessage($"MSI:KILL({operationId})");
            }
        }

        private void GetMsiInstallerMessage(string operationId, MsiState message)
        {
            MsiInstallerModel msiInstallerModel = installerModels.FirstOrDefault(i => i.OperationId == operationId);
            if (msiInstallerModel != null)
            {
                msiInstallerModel.MsiInstallerHelper.OnResponse(message);
            }
        }
    }

    public class StreamString
    {
        private readonly Stream ioStream;
        private readonly Encoding streamEncoding = Encoding.Unicode;

        public StreamString(Stream ioStream)
        {
            this.ioStream = ioStream ?? throw new ArgumentNullException(nameof(ioStream));
        }

        public async Task<string> ReadStringAsync(CancellationToken token = default)
        {
            byte[] lenBuffer = new byte[2];
            await ioStream.ReadAsync(lenBuffer, 0, 2, token).ConfigureAwait(false);

            int len = lenBuffer[0] * 256 + lenBuffer[1];

            byte[] inBuffer = new byte[len];
            await ioStream.ReadAsync(inBuffer, 0, len, token).ConfigureAwait(false);

            return streamEncoding.GetString(inBuffer);
        }

        public async Task<int> WriteStringAsync(string outString, CancellationToken token = default)
        {
            byte[] outBuffer = streamEncoding.GetBytes(outString ?? string.Empty);
            int len = Math.Min(outBuffer.Length, UInt16.MaxValue);

            byte[] header = new byte[2] { (byte)(len / 256), (byte)(len & 255) };
            await ioStream.WriteAsync(header, 0, 2, token).ConfigureAwait(false);
            await ioStream.WriteAsync(outBuffer, 0, len, token).ConfigureAwait(false);
            await ioStream.FlushAsync(token).ConfigureAwait(false);

            return len + 2;
        }
    }

    public class ScriptHelper
    {
        public static string[] GetArgsFromString(string scriptString)
        {
            Match match = Regex.Match(scriptString, @"\((.*?)\)$", RegexOptions.Singleline);
            if (match.Success)
            {
                return match.Groups[1].Value.Split("$SEPARATOR");
            }
            return [scriptString];
        }
    }
}
