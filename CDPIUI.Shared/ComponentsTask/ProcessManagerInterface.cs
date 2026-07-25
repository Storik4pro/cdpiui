using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Threading.Tasks;

namespace CDPIUI.Shared.ComponentsTask
{
    public interface IProcessService
    {
        string Id { get; set; }
        string ProcessName { get; }

        bool IsProcessRunning { get; }

        event Action<Tuple<string, bool>>? ProcessStateChanged;
        event Action<ErrorModel>? ErrorHappens;
        event Action<string>? OutputReceived;
        event Action<string>? ProcessNameChanged;
        event Action<string>? ShowErrorMessageWindow;

        bool IsErrorHappens { get; }
        ErrorModel? LastError { get; }

        Task StartProcess();
        Task StartProcess(string stringParameter1, string? stringParameter2);
        Task StopProcess(bool boolParameter = true);
        Task RestartProcess();

        static Task? StopService;
    }
}
