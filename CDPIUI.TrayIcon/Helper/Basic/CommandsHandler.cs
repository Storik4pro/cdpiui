using CDPIUI.Shared.Pipe.Models;

namespace CDPIUI.TrayIcon.Helper.Basic
{
    internal class CommandsHandler
    {
        public static async Task HandleCommandAsync(IPipeMessage message)
        {
            switch (message)
            {
                case CONPTYMessageModel model:
                    await HandleCONPTYMessage(model);
                    break;

                case GoodCheckMessageModel model:
                    await HandleGoodCheckMessage(model);
                    break;

                case SettingsMessageModel model:
                    await HandleSettingsMessage(model);
                    break;

                case UtilsMessageModel model:
                    HandleUtilsMessage(model);
                    break;

                case UpdateMessageModel model:
                    HandleUpdateMessage(model);
                    break;

                case MSIInstallationMessageModel model:
                    HandleMSIMessage(model);
                    break;

                case ProxyMessageModel model:
                    HandleProxyMessage(model);
                    break;

                case NotificationsMessageModel model:
                    HandleNotificationMessage(model);
                    break;

                case ApplicationMessageModel model:
                    await HandleApplicationMessage(model);
                    break;

                case ConditionalLaunchMessageModel model:
                    HandleConditionalLaunchMessage(model);
                    break;
            }
        }

        private static async Task HandleCONPTYMessage(CONPTYMessageModel model)
        {
            switch (model.MessageType)
            {
                case CONPTYMessageIds.StartProcessId:
                    {
                        var id = model.MessageData?["componentId"];
                        var executable = model.MessageData?["exePath"];
                        var args = model.MessageData?["args"];

                        if (id == null || executable == null || args == null)
                            return;

                        await TasksHelper.Instance.CreateAndRunNewTask(id, executable, args);
                        return;
                    }

                case CONPTYMessageIds.StopService:
                    await TasksHelper.Instance.StopService();
                    return;

                case CONPTYMessageIds.StopProcessId:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null)
                            return;

                        await TasksHelper.Instance.StopTask(id);
                        return;
                    }

                case CONPTYMessageIds.RestartProcessId:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null)
                            return;

                        await TasksHelper.Instance.RestartTask(id);
                        return;
                    }

                case CONPTYMessageIds.GetProcessIdFullOutput:
                    TasksHelper.Instance.SendAllTasksOutput();
                    return;

                case CONPTYMessageIds.GetAllProcessStates:
                    TasksHelper.Instance.SendAllTasksState();
                    return;

                case CONPTYMessageIds.ProcessIdStartupArgsChanged:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null)
                            return;

                        TasksHelper.Instance.SetIsStartArgsChangedProperty(id, true);
                        return;
                    }

                case CONPTYMessageIds.CaptureHelpOutput:
                    _ = CaptureHelpOutputAsync(model);
                    return;
            }
        }

        private static async Task CaptureHelpOutputAsync(CONPTYMessageModel model)
        {
            const int OutputChunkSize = 12 * 1024;
            string requestId = model.MessageData?["requestId"] ?? string.Empty;
            int totalChunks = 0;
            int exitCode = -1;
            bool timedOut = false;
            string error = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(requestId))
                {
                    return;
                }

                string encodedPath = model.MessageData?["exePath"] ?? string.Empty;
                string executablePath = CDPIUI.Shared.Pipe.PipePayloadCodec.DecodeString(encodedPath);
                if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                {
                    throw new FileNotFoundException(
                        "The component executable was not found.",
                        executablePath);
                }

                ConPTYCaptureResult result = await ConPTYHelper.CaptureProcessOutputAsync(
                    executablePath,
                    "--help",
                    Path.GetDirectoryName(executablePath) ?? string.Empty,
                    TimeSpan.FromSeconds(10));
                exitCode = result.ExitCode;
                timedOut = result.TimedOut;

                byte[] outputBytes = System.Text.Encoding.UTF8.GetBytes(result.Output);
                totalChunks = outputBytes.Length == 0
                    ? 0
                    : (outputBytes.Length + OutputChunkSize - 1) / OutputChunkSize;
                for (int index = 0; index < totalChunks; index++)
                {
                    int offset = index * OutputChunkSize;
                    int count = Math.Min(OutputChunkSize, outputBytes.Length - offset);
                    byte[] chunk = new byte[count];
                    Buffer.BlockCopy(outputBytes, offset, chunk, 0, count);
                    if (!await PipeHelper.SendHelpOutputChunk(requestId, index, chunk))
                    {
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }

            await PipeHelper.SendHelpOutputCompleted(
                requestId,
                totalChunks,
                exitCode,
                timedOut,
                error);
        }

        private static async Task HandleGoodCheckMessage(GoodCheckMessageModel model)
        {
            switch (model.MessageType)
            {
                case GoodCheckMessageIds.Start:
                    {
                        var taskId = model.MessageData?["operationId"];
                        var executable = model.MessageData?["exeFileName"];
                        var arguments = model.MessageData?["args"];

                        if (taskId == null || executable == null || arguments == null)
                            return;

                        await GoodCheckProcessHelper.Instance.StartAsync(executable, arguments, taskId);
                        TasksHelper.Instance.ApplyStatusToAllTasks(false);
                        return;
                    }

                case GoodCheckMessageIds.Stop:
                    GoodCheckProcessHelper.Instance.Stop();
                    TasksHelper.Instance.ApplyStatusToAllTasks(true);
                    return;
            }
        }

        private static async Task HandleSettingsMessage(SettingsMessageModel model)
        {
            switch (model.MessageType)
            {
                case SettingsMessageIds.AddToAutorun:
                    if (!AutoStartManager.AddToAutorun())
                    {
                        await PipeHelper.SendSettingsPacket(SettingsMessageIds.AutorunFalse);

                        NotifyHelper.ShowMessage(
                            LocaleHelper.GetLocaleString("Autorun"),
                            LocaleHelper.GetLocaleString("AutorunERR"),
                            "OPEN_AUTORUN_ERROR");
                    }
                    return;

                case SettingsMessageIds.RemoveFromAutorun:
                    AutoStartManager.RemoveFromAutorun();
                    return;

                case SettingsMessageIds.ReloadSettings:
                    SettingsManager.Instance.Reload();
                    return;

                case SettingsMessageIds.ComponentSetupFinished:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null)
                            return;

                        TasksHelper.Instance.AddNewTask(id);
                        TasksHelper.Instance.SetTaskStatus(id, true);
                        return;
                    }

                case SettingsMessageIds.ComponentSetupNotFinished:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null)
                            return;

                        TasksHelper.Instance.AddNewTask(id);
                        TasksHelper.Instance.SetTaskStatus(id, false);
                        return;
                    }

                case SettingsMessageIds.ComponentNotInstalled:
                    {
                        var id = model.MessageData?["componentId"];
                        if (id == null)
                            return;

                        await TasksHelper.Instance.StopAndRemoveTaskAsync(id);
                        return;
                    }
            }
        }
        private static void HandleUtilsMessage(UtilsMessageModel model)
        {
            switch (model.MessageType)
            {
                case UtilsMessageIds.GrantAccessRequest:
                    {
                        var path = model.MessageData?["file"];
                        if (path == null)
                            return;

                        Utils.GrantAccess(path, true);
                        return;
                    }
            }
        }

        private static void HandleUpdateMessage(UpdateMessageModel model)
        {
            switch (model.MessageType)
            {
                case UpdateMessageIds.BeginApplicationUpdate:
                    {
                        var archive = model.MessageData?["filePath"];
                        if (archive == null)
                            return;

                        Utils.StartUpdate(archive);
                        return;
                    }

                case UpdateMessageIds.UpdatesAreAvailable:
                    NotifyHelper.ShowMessage(
                        "CDPI UI",
                        LocaleHelper.GetLocaleString("UpdateAvailable"),
                        "UPDATE:OPEN_DOWNLOAD_PAGE");
                    return;
            }
        }

        private static void HandleMSIMessage(MSIInstallationMessageModel model)
        {
            switch (model.MessageType)
            {
                case MSIInstallationMessageIds.Begin:
                    {
                        var operationId = model.MessageData?["operationId"];
                        var fileName = model.MessageData?["fileName"];

                        if (operationId == null || fileName == null)
                            return;

                        MsiInstallerHelper.Instance.AddToQueue(operationId, fileName);
                        return;
                    }

                case MSIInstallationMessageIds.Kill:
                    {
                        var operationId = model.MessageData?["operationId"];
                        if (operationId == null)
                            return;

                        MsiInstallerHelper.Instance.RemoveFromQueue(operationId);
                        return;
                    }
            }
        }

        private static void HandleProxyMessage(ProxyMessageModel model)
        {
            switch (model.MessageType)
            {
                case ProxyMessageIds.Init:
                    {
                        var taskId = model.MessageData?["componentId"];
                        var proxyFirePath = model.MessageData?["proxyFirePath"];

                        if (taskId == null || proxyFirePath == null)
                            return;

                        TasksHelper.Instance.InitProxyOnTask(taskId, proxyFirePath);
                        return;
                    }

                case ProxyMessageIds.Setup:
                    {
                        var taskId = model.MessageData?["componentId"];
                        var proxyType = model.MessageData?["proxyType"];
                        var ip = model.MessageData?["ip"];
                        var port = model.MessageData?["port"];

                        if (taskId == null || proxyType == null ||
                            ip == null || port == null)
                            return;

                        TasksHelper.Instance.EnableProxyOnTask(
                            taskId, proxyType, ip, port);
                        return;
                    }

                case ProxyMessageIds.Clean:
                    {
                        var taskId = model.MessageData?["componentId"];
                        if (taskId == null)
                            return;

                        TasksHelper.Instance.CleanProxyOnTask(taskId);
                        return;
                    }
            }
        }

        private static void HandleNotificationMessage(NotificationsMessageModel model)
        {
            switch (model.MessageType)
            {
                case NotificationsMessageIds.ProxySetupRequired:
                    {
                        var taskName = model.MessageData?["componentName"];
                        if (taskName == null)
                            return;

                        NotifyHelper.ShowMessage(
                            "CDPI UI",
                            string.Format(LocaleHelper.GetLocaleString("ProxySetupAsk"), taskName),
                            "OPEN_PROXY_SETUP");

                        return;
                    }

                case NotificationsMessageIds.CompatibilityCheckAssistant:
                    {
                        var component = model.MessageData?["componentName"];
                        if (component == null)
                            return;

                        NotifyHelper.ShowMessage(
                            LocaleHelper.GetLocaleString("CompatibilityCheckAssistant"),
                            string.Format(LocaleHelper.GetLocaleString("ConfigRequiredNewestVersionOfComponent"), component),
                            "OPEN_BEGIN_STORE_UPDATE_CHECK");

                        return;
                    }
            }
        }

        private static async Task HandleApplicationMessage(ApplicationMessageModel model)
        {
            switch (model.MessageType)
            {
                case ApplicationMessageIds.HardRestart:
                    await PipeHelper.SendApplicationPacket(ApplicationMessageIds.CloseApplicationUI);

                    RunHelper.RunAsDesktopUser(
                        Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"),
                        "");

                    NotifyHelper.Instance.Dispose();
                    Application.Exit();
                    return;
            }
        }

        private static void HandleConditionalLaunchMessage(ConditionalLaunchMessageModel model)
        {
            switch (model.MessageType)
            {
                case ConditionalLaunchMessageIds.ActionCompleted:
                    ConditionalLaunch.ConditionalActionExecutor.CompleteCoreAction(
                        model.MessageData?["operationId"],
                        model.MessageData?["success"],
                        model.MessageData?["error"]);
                    return;

                case ConditionalLaunchMessageIds.ReloadTasks:
                    ConditionalLaunch.ConditionalLaunchEngine.Instance.Reload(
                        model.MessageData?["tasksDirectory"]);
                    return;

                case ConditionalLaunchMessageIds.ExecuteTask:
                    ConditionalLaunch.ConditionalLaunchEngine.Instance.RunTask(
                        model.MessageData?["taskId"]);
                    return;
            }
        }
    }
}
