using CDPIUI.Core.ComponentServices;
using CDPIUI.Shared;
using CDPIUI.Shared.Pipe;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.Shared.PrettyErrorConvertionService;
using CDPIUI.Shared.Migration;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;

namespace CDPIUI.Core.Communication
{
    public class PipeClientService : PipeServiceBase
    {
        private static PipeClientService? _instance;
        private static readonly object _lock = new object();

        public static PipeClientService Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new PipeClientService();
                    return _instance;
                }
            }
        }

        public event Action<IPipeMessage>? MessageReceived;

        private PipeClientService()
        {
            SettingsManager.Instance.EnumPropertyChanged += SettingsManager_EnumPropertyChanged;

            Logger = Basic.Logger.Instance;
        }

        private void SettingsManager_EnumPropertyChanged(IEnumerable<string> _enum)
        {
            if (_enum.Count() >= 2)
            {
                if (_enum.ElementAt(0) == "CONFIGS")
                {
                    _ = PipeHelper.SendConPTYPacket(CONPTYMessageIds.ProcessIdStartupArgsChanged, _enum.ElementAt(1));
                    _ = ComponentTasksManager.Instance.SendTaskData(_enum.ElementAt(1));
                }
            }
        }

        public void Init()
        {
            PipeStream = new NamedPipeClientStream(".", SharedConstants.PipeName,
                            PipeDirection.InOut, PipeOptions.Asynchronous,
                            TokenImpersonationLevel.Impersonation);
        }

        public async void Start()
        {
            if (PipeStream == null)
            {
                throw new NullReferenceException("Call PipeClient.Instanse.Init first");
            }
            CreateCancellationToken();

            CancellationTokenSource.CancelAfter(2000);
            try
            {
                await ((NamedPipeClientStream)PipeStream).ConnectAsync(CancellationToken ?? default);
            }
            catch { }

            if (!PipeStream.IsConnected)
            {
                try
                {
                    string startupString = SettingsManager.Instance.GetValue<bool>("APPEARANCE", "hideToTrayOnStartup") ? "--autorun" : "--show-ui";
                    if (GoodbyeDpiMigrationActivation.TryFindArgument(
                        Environment.GetCommandLineArgs(), out var migrationRequest))
                    {
                        startupString = $"--show-ui {migrationRequest!.RawArgument}";
                    }
                    var psi = new ProcessStartInfo(Path.Combine(Data.Directories.CurrentDirectory, "CDPIUI_TrayIcon.exe"), startupString)
                    {
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                }
                catch { }
                Logger?.CreateErrorLog(nameof(PipeClientService), "Connection timeout");


                Process.GetCurrentProcess().Kill();
                return;
            }

            CancellationTokenSource.Dispose();
            CancellationTokenSource = null;
            CreateCancellationToken();

            try
            {
                await HandleConnectionAsync();
            }
            catch { }
            finally
            {
                CoreEvents.Instance.InvokeCriticalCoreExceptionHappens(new ErrorModel()
                {
                    ErrorCode = "ERR_PROCESS_DIED",
                    FriendlyDescription = "One of application process is died by unknown reason. Application can't restart this process, " +
                                          "because ERR_ACCESS_DENIED happens. Please, restart app manually.",
                    Object = nameof(PipeClientService)
                });
                Dispose();
            }
            
        }

        protected override async Task SendConnectionPacket()
        {
            await SendMessageAsync(ServiceMessageModel.RequestAuth(Shared.Secrets.Secret.AuthGuid).ToString());
        }

        protected override async Task RunMessageActions(IPipeMessage model)
        {
            Debug.WriteLine("Message actions");
            await base.RunMessageActions(model);

            CoreCommandsHandler.HandleCommand(model);

            MessageReceived?.Invoke(model);
        }
    }
}
