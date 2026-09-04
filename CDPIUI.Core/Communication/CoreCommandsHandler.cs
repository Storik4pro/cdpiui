using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.MSI;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.Shared.Extentions;
using System.Diagnostics;
using static CDPIUI.Core.Store.MSI.MsiInstallerService;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store;
using CDPIUI.Core.Features;
using CDPIUI.Shared.ConditionalLaunch;

namespace CDPIUI.Core.Communication
{
    public class CoreCommandsHandler
    {
        public static bool HandleCommand(IPipeMessage message)
        {
            if (!CanHandle(message))
                return false;

            _ = HandleCommandAsync(message);
            return true;
        }

        public static async Task<bool> HandleCommandAsync(IPipeMessage message)
        {
            switch (message)
            {
                case ServiceMessageModel model:
                    HandleServiceMessage(model);
                    break;

                case CONPTYMessageModel model:
                    await HandleCONPTYMessage(model);
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
                case ConditionalLaunchMessageModel model:
                    await HandleConditionalLaunchMessage(model);
                    break;
                default:
                    return false;
            }
            return true;
        }

        private static bool CanHandle(IPipeMessage message) =>
            message is ServiceMessageModel or
                CONPTYMessageModel or
                SettingsMessageModel or
                MSIInstallationMessageModel or
                ApplicationMessageModel or
                ConditionalLaunchMessageModel;

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

        private static async Task HandleCONPTYMessage(CONPTYMessageModel model)
        {
            if (ConPtyHelpCaptureClient.HandleMessage(model))
            {
                return;
            }

            switch (model.MessageType)
            {
                case CONPTYMessageIds.GetStartupString:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null) return;

                        await ComponentTasksManager.Instance.CreateAndRunNewTask(id);
                        return;
                    }

                case CONPTYMessageIds.GetAllStartupStrings:
                    await ComponentTasksManager.Instance.RunAllPreferredActions();
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
                            if (pm != null) await pm.ShowErrorMessage(title, text, showWindow: model.MessageData?["stateSnapshot"] != "true");
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

                        pm?.SetOutput(output);

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

        private static async Task HandleConditionalLaunchMessage(ConditionalLaunchMessageModel model)
        {
            if (model.MessageType != ConditionalLaunchMessageIds.ExecuteAction)
                return;

            var operationId = model.MessageData?["operationId"];
            if (string.IsNullOrWhiteSpace(operationId))
                return;

            try
            {
                if (!Enum.TryParse<ConditionalActionType>(
                    model.MessageData?["actionType"],
                    ignoreCase: true,
                    out var actionType))
                {
                    throw new ArgumentException("Unknown conditional action type.");
                }

                switch (actionType)
                {
                    case ConditionalActionType.ApplyPreset:
                        ApplyPreset(model);
                        break;

                    case ConditionalActionType.StartComponent:
                        {
                            var componentId = RequireParameter(model, "componentId");
                            if (!await ComponentTasksManager.Instance.IsTaskRunned(componentId))
                            {
                                await ComponentTasksManager.Instance.CreateAndRunNewTask(componentId);
                                var task = await ComponentTasksManager.Instance.GetTaskFromId(componentId);
                                if (task?.ProcessManager.IsErrorHappens == true)
                                {
                                    throw new InvalidOperationException(
                                        task.ProcessManager.LastError?.ErrorCode ??
                                        $"Component '{componentId}' could not be started.");
                                }
                            }
                            break;
                        }

                    case ConditionalActionType.StartAutorunComponents:
                        await ComponentTasksManager.Instance.RunAllPreferredActions();
                        break;

                    case ConditionalActionType.CheckStoreUpdates:
                        await StoreHelper.Instance.CheckUpdates();
                        if (StoreHelper.Instance.IsExceptonHappensWhileCheckingUpdates)
                            throw new InvalidOperationException("One or more Store update checks failed.");
                        break;

                    case ConditionalActionType.CheckApplicationUpdates:
                        await ApplicationUpdate.Instance.CheckForUpdates(notify: true);
                        if (ApplicationUpdate.Instance.ErrorHappened)
                        {
                            throw new InvalidOperationException(
                                ApplicationUpdate.Instance.ErrorInfo ??
                                "The application update check failed.");
                        }
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Action '{actionType}' cannot be executed by CDPIUI.Core.");
                }

                await PipeHelper.SendConditionalLaunchResult(operationId, success: true);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(
                    nameof(CoreCommandsHandler),
                    $"Conditional action failed: {ex}");
                await PipeHelper.SendConditionalLaunchResult(operationId, success: false, ex.Message);
            }
        }

        private static void ApplyPreset(ConditionalLaunchMessageModel model)
        {
            var componentId = RequireParameter(model, "componentId");
            var packId = RequireParameter(model, "packId");
            var fileName = RequireParameter(model, "fileName");

            ComponentItemsLoaderHelper.Instance.Init();
            var componentHelper = ComponentItemsLoaderHelper.Instance
                .GetComponentHelperFromId(componentId)
                ?? throw new InvalidOperationException($"Component '{componentId}' was not found.");

            SettingsManager.Instance.SetValue(["CONFIGS", componentId], "configFile", fileName);
            SettingsManager.Instance.SetValue(["CONFIGS", componentId], "configId", packId);
            componentHelper.ReInitConfigs();
        }

        private static string RequireParameter(
            ConditionalLaunchMessageModel model,
            string name)
        {
            var value = model.MessageData?[name];
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Parameter '{name}' is required.");
            return value;
        }
    }
}
