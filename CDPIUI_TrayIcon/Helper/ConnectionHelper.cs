using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI_TrayIcon.Helper
{
    public class PipeServer : IDisposable
    {
        private string _pipeName;
        private int _maxServerInstances;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;

        private NamedPipeServerStream? _pipeServerStream;
        private StreamString? _streamString;

        private bool IsAuthorized = false;

        public Action? Disconnected;

        private static PipeServer? _instance;
        private static readonly object _lock = new object();

        public static PipeServer Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new PipeServer();
                    return _instance;
                }
            }
        }

        public PipeServer()
        {
            _pipeName = "{C9253A32-C9BB-496F-A700-43268B370236}";
            _maxServerInstances = 1;
        }

        public void Init(string pipeName = "{C9253A32-C9BB-496F-A700-43268B370236}", int maxServerInstances = 1)
        {
            _pipeName = pipeName;
            _maxServerInstances = Math.Max(1, maxServerInstances);
        }

        public void Start()
        {
            if (_cts != null) throw new InvalidOperationException("Server already started");

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try
            {
                if (_listenerTask != null) await _listenerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _listenerTask = null;

                _pipeServerStream?.Dispose();
                _pipeServerStream = null;
                _streamString = null;
            }
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                IsAuthorized = false;
                try
                {
                    var ps = new PipeSecurity();

                    var currentUserSid = WindowsIdentity.GetCurrent().User;
                    if (currentUserSid == null)
                    {
                        continue;
                    }
                    ps.AddAccessRule(new PipeAccessRule(currentUserSid, PipeAccessRights.FullControl, AccessControlType.Allow));

                    var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                    ps.AddAccessRule(new PipeAccessRule(everyone, PipeAccessRights.ReadWrite, AccessControlType.Allow));

                    _pipeServerStream = NamedPipeServerStreamAcl.Create(
                        _pipeName,
                        PipeDirection.InOut,
                        _maxServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        inBufferSize: 0,
                        outBufferSize: 0,
                        pipeSecurity: ps);
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateErrorLog(nameof(PipeServer), $"Pipe create error: {ex.Message}");
                    Process.GetCurrentProcess().Kill();
                    return;
                    
                }

                try
                {
                    Console.WriteLine("WAIT");
                    await _pipeServerStream.WaitForConnectionAsync(token);

                    await HandleClientAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    Logger.Instance.CreateErrorLog(nameof(PipeServer), $"Pipe accept or communication error: {ex.Message}");
                }
                finally
                {
                    Disconnected?.Invoke();
                    _pipeServerStream?.Dispose();
                    _pipeServerStream = null;
                    _streamString = null;
                }
            }
        }

        private async Task HandleClientAsync(CancellationToken token)
        {
            if (_pipeServerStream == null) return;

            int threadId = Thread.CurrentThread.ManagedThreadId;
            Logger.Instance.CreateDebugLog(nameof(PipeServer), $"Client connected on thread[{threadId}].");

            _streamString = new StreamString(_pipeServerStream);
            await _streamString.WriteStringAsync("CONNECT:OK");
            try
            {
                while (_pipeServerStream.IsConnected && !token.IsCancellationRequested)
                {
                    string message;
                    try
                    {
                        message = await _streamString.ReadStringAsync(token);
                        RunMessageActions(message);
                    }
                    catch (EndOfStreamException)
                    {
                        Logger.Instance.CreateDebugLog(nameof(PipeServer), $"Client disconnected on thread[{threadId}].");
                        break;
                    }

                    if (string.IsNullOrEmpty(message))
                    {
                        continue;
                    }
                }
            }
            catch (IOException e)
            {
                Logger.Instance.CreateErrorLog(nameof(PipeServer), $"Pipe communication error (thread[{threadId}]): {e.Message}");
            }
            finally
            {
                try
                {
                    if (_pipeServerStream.IsConnected)
                    {
                        _pipeServerStream.WaitForPipeDrain();
                        _pipeServerStream.Disconnect();
                    }
                }
                catch { }

                Logger.Instance.CreateDebugLog(nameof(PipeServer), $"Client handler on thread[{threadId}] finished.");
            }
        }

        private void RunMessageActions(string message)
        {
            // MessageBox.Show(message);

            if (message.StartsWith("PIPE:"))
            {
                if (message.StartsWith("PIPE:CONNECT"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 1)
                    {
                        Console.WriteLine($"ERR, {message} => args exception");
                        return;
                    }
                    if (result[0] == Secret.AuthGuid)
                    {
                        IsAuthorized = true;
                        _ = SendMessage("PIPE:AUTH_OK");
                    }
                    else
                    {
                        IsAuthorized = false;
                        _ = SendMessage("PIPE:AUTH_ERR");
                    }
                }
            }
            else if (!IsAuthorized)
            {
                Console.WriteLine("ERR, not authorized");
                return;
            }
            else if (message.StartsWith("CONPTY:"))
            {
                if (message.StartsWith("CONPTY:START"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 2)
                    {
                        Console.WriteLine($"ERR, {message} => args exception");
                        return;
                    }
                    _ = ProcessManager.Instance.StartProcess(executable: result[0], args: result[1]);
                }
                else if (message.StartsWith("CONPTY:STOPSERVICE"))
                {
                    _ = ProcessManager.Instance.StopService();
                }
                else if (message.StartsWith("CONPTY:STOP"))
                {
                    _ = ProcessManager.Instance.StopProcess();
                }
                else if (message.StartsWith("CONPTY:RESTART"))
                {
                    _ = ProcessManager.Instance.RestartProcess();
                }
                else if (message.StartsWith("CONPTY:GETOUTPUT"))
                {
                    ProcessManager.Instance.SendDefaultProcessOutput();
                }
                else if (message.StartsWith("CONPTY:GETSTATE"))
                {
                    ProcessManager.Instance.SendState();
                    ProcessManager.Instance.SendNowSelectedComponentName();
                }
                else if (message.StartsWith("CONPTY:PROCESSCHANGED"))
                {
                    ProcessManager.Instance.IsProcessInfoChanged = true;
                }
            }
            else if (message.StartsWith("GOODCHECK:"))
            {
                if (message.StartsWith("GOODCHECK:START"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 3)
                    {
                        Console.WriteLine($"ERR, {message} => args exception");
                        return;
                    }
                    _ = GoodCheckProcessHelper.Instance.StartAsync(result[0], result[1], result[2]);
                    TrayIconHelper.Instance.ToggleStartButtonEnabled(false);

                }
                else if (message.StartsWith("GOODCHECK:STOP"))
                {
                    GoodCheckProcessHelper.Instance.Stop();
                    TrayIconHelper.Instance.ToggleStartButtonEnabled(true);
                }
            }
            else if (message.StartsWith("SETTINGS:"))
            {
                if (message.StartsWith("SETTINGS:ADD_TO_AUTORUN"))
                {
                    if (!AutoStartManager.AddToAutorun())
                    {
                        _ = SendMessage("SETTINGS:AUTORUN_FALSE");
                        TrayIconHelper.Instance.ShowMessage(LocaleHelper.GetLocaleString("Autorun"), LocaleHelper.GetLocaleString("AutorunERR"), "OPEN_AUTORUN_ERROR");
                    }
                }
                else if (message.StartsWith("SETTINGS:REMOVE_FROM_AUTORUN"))
                {
                    AutoStartManager.RemoveFromAutorun();
                }
                else if (message.StartsWith("SETTINGS:RELOAD"))
                {
                    SettingsManager.Instance.Reload();
                }
            }
            else if (message.StartsWith("UPDATE:"))
            {
                if (message.StartsWith("UPDATE:BEGIN"))
                {
                    var result = ScriptHelper.GetArgsFromString(message);
                    if (result.Length < 1)
                    {
                        Console.WriteLine($"ERR, {message} => args exception");
                        return;
                    }
                    Utils.StartUpdate(result[0]);
                }
                else if (message.StartsWith("UPDATE:AVAILABLE"))
                {
                    TrayIconHelper.Instance.ShowMessage("CDPI UI", LocaleHelper.GetLocaleString("UpdateAvailable"), "UPDATE:OPEN_DOWNLOAD_PAGE");
                }
            }

        }

        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public async Task<bool> SendMessage(string message)
        {
            if (_pipeServerStream == null || !_pipeServerStream.IsConnected || _streamString == null)
                return false;

            await _sendLock.WaitAsync();
            try
            {
                if (_pipeServerStream == null || !_pipeServerStream.IsConnected || _streamString == null)
                    return false;

                await _streamString.WriteStringAsync(message);

                return true;
            }
            catch { return false; }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
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
