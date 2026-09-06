using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;

namespace CDPIUI.Helper.Basic;

public sealed class TasksManagerHelper
{
    private static TasksManagerHelper instance;
    public static TasksManagerHelper Instance => instance ??= new TasksManagerHelper();
    private readonly DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();

    private TasksManagerHelper()
    {
        ComponentTasksManager.Instance.ShowErrorMessageForTaskId += HandleErrorMessage;
    }

    private void HandleErrorMessage(string id)
    {
        if (!SettingsManager.Instance.GetValueOrDefault<bool>("NOTIFICATIONS", "componentErrorWindow", defaultValue: true)) return;
        dispatcher.TryEnqueue(async () =>
        {
            try
            {
                var process = (await ComponentTasksManager.Instance.GetTaskFromId(id))?.ProcessManager;
                if (process?.IsErrorHappens != true) return;
                var window = await ((App)Application.Current).UnsafeCreateNewWindow<ComponentErrorWindow>(activate: false, id: id);
                window.SetError(id, process.LastError);
                App.ActivateWindow(window);
            }
            catch (Exception exception)
            {
                Logger.Instance.CreateErrorLog(nameof(TasksManagerHelper), exception.ToString());
            }
        });
    }
}
