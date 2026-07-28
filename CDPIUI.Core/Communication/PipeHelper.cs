using CDPIUI.Shared.Pipe.Models;
using System.Collections.Specialized;
using CDPIUI.Shared.ConditionalLaunch;

namespace CDPIUI.Core.Communication
{
    public static class PipeHelper
    {
        public static async Task SendSettingsPacket(
            SettingsMessageIds messageId, NameValueCollection? data = null)
        {
            SettingsMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = data ?? []
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendUpdatePacket(
            UpdateMessageIds messageId,
            string? filePath = null)
        {
            UpdateMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = new()
            {
                { "filePath", filePath }
            }
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendApplicationPacket(ApplicationMessageIds messageId)
        {
            ApplicationMessageModel model = new()
            {
                MessageType = messageId,
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendCompatibilityCheckPacket()
        {
            CompatibilityCheckMessageModel model = new()
            {
                MessageType = CompatibilityCheckMessageIds.Begin,
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendNotificationPacket(
            NotificationsMessageIds messageId,
            NameValueCollection? @params = null)
        {
            NotificationsMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = @params ?? []
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendGoodCheckPacket(
            GoodCheckMessageIds messageId,
            string? operationId = null,
            string? exeFileName = null,
            string? args = null)
        {
            GoodCheckMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = new()
            {
                { "operationId", operationId },
                { "exeFileName", exeFileName },
                { "args", args }
            }
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendMSIPacket(
            MSIInstallationMessageIds messageId,
            string operationId,
            string? fileName = null)
        {
            MSIInstallationMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = new()
            {
                { "operationId", operationId },
                { "fileName", fileName }
            }
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendConPTYPacket(
            CONPTYMessageIds messageId,
            string? componentId = null,
            string? exePath = null,
            string? args = null)
        {
            CONPTYMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = new()
            {
                { "componentId", componentId },
                { "exePath", exePath },
                { "args", args }
            }
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendProxyPacket(
            ProxyMessageIds messageId,
            string componentId,
            string? proxyType = null,
            string? ip = null,
            string? port = null,
            string? proxyFirePath = null)
        {
            ProxyMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = new()
            {
                { "componentId", componentId },
                { "proxyType", proxyType },
                { "ip", ip },
                { "port", port },
                { "proxyFirePath", proxyFirePath }
            }
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendGrantAccessPacket(string file)
        {
            UtilsMessageModel model = new()
            {
                MessageType = UtilsMessageIds.GrantAccessRequest,
                MessageData = new()
            {
                { "file", file }
            }
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendConditionalLaunchResult(
            string operationId,
            bool success,
            string? error = null)
        {
            ConditionalLaunchMessageModel model = new()
            {
                MessageType = ConditionalLaunchMessageIds.ActionCompleted,
                MessageData = new()
                {
                    { "operationId", operationId },
                    { "success", success.ToString() },
                    { "error", error }
                }
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendConditionalTasksReloadPacket()
        {
            ConditionalLaunchMessageModel model = new()
            {
                MessageType = ConditionalLaunchMessageIds.ReloadTasks,
                MessageData = new()
                {
                    {
                        "tasksDirectory",
                        ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile(
                            SettingsManager.Instance.SettingsFilePath)
                    }
                }
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task SendConditionalTaskExecutePacket(string taskId)
        {
            ConditionalLaunchMessageModel model = new()
            {
                MessageType = ConditionalLaunchMessageIds.ExecuteTask,
                MessageData = new()
                {
                    { "taskId", taskId }
                }
            };

            await PipeClientService.Instance.SendMessageAsync(model.ToString());
        }
    }
}
