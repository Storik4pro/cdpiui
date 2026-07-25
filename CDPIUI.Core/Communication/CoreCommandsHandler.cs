using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.MSI;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.Shared.Extentions;
using System.Diagnostics;
using static CDPIUI.Core.Store.MSI.MsiInstallerService;
using CDPIUI.Core.ComponentServices;

namespace CDPIUI.Core.Communication
{
    internal class CoreCommandsHandler
    {
        public static void HandleCommand(IPipeMessage message)
        {
            switch (message)
            {
                case ServiceMessageModel model:
                    HandleServiceMessage(model);
                    break;

                case CONPTYMessageModel model:
                    HandleCONPTYMessage(model);
                    break;

                case SettingsMessageModel model:
                    HandleSettingsMessage(model);
                    break;

                case MSIInstallationMessageModel model:
                    HandleMSIMessage(model);
                    break;

                case ApplicationMessageModel model:
                    HandleApplicationMessage(model);
                    break;
            }
        }

        private static void HandleServiceMessage(ServiceMessageModel model)
        {
            switch (model.MessageType)
            {
                case ServiceMessageIds.AuthFAIL:
                    Logger.Instance
                        .CreateErrorLog(nameof(PipeClientService), "Auth status: FAIL. Check auth guid!");
                    return;
                default: return;
            }
        }

        private static void HandleCONPTYMessage(CONPTYMessageModel model)
        {
            switch (model.MessageType)
            {
                case CONPTYMessageIds.GetStartupString:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null) return;

                        ComponentTasksManager.Instance.CreateAndRunNewTask(id);
                        return;
                    }

                case CONPTYMessageIds.GetAllStartupStrings:
                    ComponentTasksManager.Instance.RunAllPreferredActions();
                    return;

                case CONPTYMessageIds.CleanOutputForId:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null) return;

                        ComponentTasksManager.Instance
                            .GetTaskFromId(id)
                            .Result?
                            .ProcessManager?
                            .ClearOutput();

                        return;
                    }

                case CONPTYMessageIds.MarkProcessIdAsStarted:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null) return;

                        ComponentTasksManager.Instance
                            .GetTaskFromId(id)
                            .Result?
                            .ProcessManager?
                            .MarkAsStarted();

                        return;
                    }

                case CONPTYMessageIds.MarkProcessIdAsStopped:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null) return;

                        var title = model.MessageData?["errorMessage"];
                        var text = model.MessageData?["errorObject"];

                        var pm = ComponentTasksManager.Instance
                            .GetTaskFromId(id)
                            .Result?
                            .ProcessManager;

                        if (title != null && text != null)
                        {
                            Debug.WriteLine(title, text);
                            pm?.ShowErrorMessage(title, text);
                        }
                        else
                        {
                            pm?.MarkAsFinished();
                        }

                        return;
                    }

                case CONPTYMessageIds.ChangeProcessIdExecutable:
                    {
                        var id = model.MessageData?["componentId"];
                        var processName = model.MessageData?["processName"];

                        if (id == null || processName == null)
                            return;

                        ComponentTasksManager.Instance
                            .GetTaskFromId(id)
                            .Result?
                            .ProcessManager?
                            .ChangeProcName(processName);

                        return;
                    }

                case CONPTYMessageIds.ProcessIdNewOutput:
                    {
                        var id = model.MessageData?["componentId"];
                        var output = model.MessageData?["output"];

                        if (id == null || output == null)
                            return;

                        ComponentTasksManager.Instance
                            .GetTaskFromId(id)
                            .Result?
                            .ProcessManager?
                            .AddOutput(output);

                        return;
                    }

                case CONPTYMessageIds.ProcessIdFullOutput:
                    {
                        var id = model.MessageData?["componentId"];
                        var output = model.MessageData?["output"];

                        if (id == null || output == null)
                            return;

                        var pm = ComponentTasksManager.Instance
                            .GetTaskFromId(id)
                            .Result?
                            .ProcessManager;

                        pm?.ClearOutput();
                        pm?.AddOutput(output);

                        return;
                    }
            }
        }

        private static void HandleSettingsMessage(SettingsMessageModel model)
        {
            switch (model.MessageType)
            {
                case SettingsMessageIds.AutorunFalse:
                    SettingsManager.Instance.SetValue("SYSTEM", "autorun", false);
                    return;
            }
        }

        private static void HandleMSIMessage(MSIInstallationMessageModel model)
        {
            switch (model.MessageType)
            {
                case MSIInstallationMessageIds.SetOperationStatus:
                    {
                        var id = model.MessageData?["operationId"];
                        var state = model.MessageData?["state"]?.ToEnum<MsiState>();

                        if (id == null || state == null)
                            return;

                        MsiInstallerQueueManager.Instance.GetMsiInstallerMessage(id, (MsiState)state);
                        return;
                    }

                case MSIInstallationMessageIds.RemoveOperationId:
                    {
                        var id = model.MessageData?["operationId"];
                        if (id == null)
                            return;

                        MsiInstallerQueueManager.Instance.RemoveMsiInstallerModel(id, notify: false);
                        return;
                    }
            }
        }


        private static void HandleApplicationMessage(ApplicationMessageModel model)
        {
            switch (model.MessageType)
            {
                case ApplicationMessageIds.CloseApplicationUI:
                    Process.GetCurrentProcess().Kill();
                    return;
            }
        }
    }
}
