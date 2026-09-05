using CDPIUI.Shared;
using CDPIUI.Shared.Pipe;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.Shared.Secrets;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace CDPIUI.TrayIcon.Helper.Basic
{
    public class PipeServer : PipeServiceBase
    {
        private int _maxServerInstances = 1;
        private Task? _listenerTask;

        private bool IsAuthorized = false;

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
            Logger = Basic.Logger.Instance;
        }

        public void Init() { }

        public void Start()
        {
            if (CancellationToken != null) throw new InvalidOperationException("Server already started");
            CreateCancellationToken();

            _listenerTask = Task.Run(() => ListenLoopAsync());
        }

        

        private async Task ListenLoopAsync()
        {
            while (!(CancellationToken?.IsCancellationRequested ?? true))
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

                    PipeStream = NamedPipeServerStreamAcl.Create(
                        SharedConstants.PipeName,
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
                    Logger?.CreateErrorLog(nameof(PipeServer), $"Pipe create error: {ex.Message}");
                    Process.GetCurrentProcess().Kill();
                    return;
                }

                try
                {
                    Console.WriteLine("WAIT");
                    await ((NamedPipeServerStream)PipeStream).WaitForConnectionAsync(CancellationToken ?? default);

                    int threadId = Thread.CurrentThread.ManagedThreadId;
                    Logger?.CreateDebugLog(nameof(PipeServer), $"Client connected on thread[{threadId}].");
                    await HandleConnectionAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    Logger?.CreateErrorLog(nameof(PipeServer), $"Pipe accept or communication error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Logger?.CreateErrorLog(nameof(PipeServer), $"Pipe failure: {ex.Message}");
                }
                finally
                {
                    if (PipeStream.IsConnected)
                    {
                        ((NamedPipeServerStream)PipeStream).Disconnect();
                    }

                    NotifyDisconnected();
                    PipeStream?.Dispose();
                    PipeStream = null;
                    StreamString = null;

                    Logger?.CreateDebugLog(nameof(PipeServer), $"Client handler finished.");
                }
            }
        }

        protected override async Task RunMessageActions(IPipeMessage message)
        {
            if (message is ServiceMessageModel messageModel)
            {
                if (messageModel.MessageType == ServiceMessageIds.RequestAuth)
                {
                    Debug.WriteLine(messageModel.MessageData["GUID"]);
                    if (messageModel.MessageData != null && messageModel.MessageData["GUID"] == Secret.AuthGuid)
                    {
                        IsAuthorized = true;
                        _ = SendMessageAsync(ServiceMessageModel.AuthSuccessful().ToString());
                    }
                    else
                    {
                        IsAuthorized = false;
                        _ = SendMessageAsync(ServiceMessageModel.AuthFailure().ToString());
                    }
                }
            }

            if (!IsAuthorized)
            {
                Logger?.CreateWarningLog(nameof(PipeServer), "Authorization failed.");
                return;
            }

            await CommandsHandler.HandleCommandAsync(message);
        }

        public override async void Dispose()
        {
            try
            {
                if (_listenerTask != null) await _listenerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            finally
            {
                base.Dispose();
            }
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
