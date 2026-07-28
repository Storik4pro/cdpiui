using CDPIUI.Shared.Pipe.Models;
using CDPIUI.TrayIcon.Helper.Basic;
using System.Collections.Specialized;
using static CDPIUI.TrayIcon.Helper.MsiInstallerHelper;
using CDPIUI.Shared.ConditionalLaunch;

namespace CDPIUI.TrayIcon.Helper
{
    public static class PipeHelper
    {
        public static async Task<bool> SendOpenWindowPacket(string window, bool openIfNotConnected = false)
        {
            return await SendOpenWindowPacket(window, [], openIfNotConnected);
        }

        public static async Task<bool> SendOpenWindowPacket(string window, NameValueCollection @params, bool openIfNotConnected = false)
        {
            NameValueCollection collection = new()
            {
                { "windowName", window },
                @params
            };

            PresentationMessageModel model = new()
            {
                MessageType = PresentationMessageIds.ShowWindow,
                MessageData = collection,
            };

            if (!openIfNotConnected) return await PipeServer.Instance.SendMessageAsync(model.ToString());
            else return await TrySendMessage(model);
        }

        public static async Task<bool> SendApplicationPacket(ApplicationMessageIds targetAction) 
        {
            ApplicationMessageModel model = new()
            {
                MessageType = targetAction,
            };
            return await PipeServer.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task<bool> SendMsiInstallationPacket(
            MSIInstallationMessageIds messageId, 
            string operationId, 
            MsiState? state = null)
        {
            MSIInstallationMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = new()
                {
                    { "operationId", operationId },
                    { "state", state.ToString() }
                }
            };

            return await PipeServer.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task<bool> SendConPTYPacket(
            CONPTYMessageIds messageId, 
            string id, 
            NameValueCollection? @params = null,
            bool openIfNotConnected = false)
        {
            NameValueCollection collection = new()
            {
                { "componentId", id },
                @params ?? []
            };

            CONPTYMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = collection,
            };

            if (!openIfNotConnected) return await PipeServer.Instance.SendMessageAsync(model.ToString());
            else return await TrySendMessage(model);
        }

        public static async Task<bool> SendGoodCheckPacket(GoodCheckMessageIds messageId, string operationId, string? error = null)
        {
            GoodCheckMessageModel model = new()
            {
                MessageType = messageId,
                MessageData = new()
                {
                    { "operationId", operationId },
                    { "error", error  }
                }
            };

            return await PipeServer.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task<bool> SendGrantAcessPacket(bool result)
        {
            UtilsMessageModel model = new()
            {
                MessageType = UtilsMessageIds.GrantAccessResponse,
                MessageData = new()
                {
                    { "result", result.ToString() },
                }
            };

            return await PipeServer.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task<bool> SendCheckUpdatesPacket()
        {
            UpdateMessageModel model = new()
            {
                MessageType = UpdateMessageIds.CheckForUpdates,
            };

            return await TrySendMessage(model);
        }

        public static async Task<bool> SendCompatibilityCheckPacket()
        {
            CompatibilityCheckMessageModel model = new()
            {
                MessageType = CompatibilityCheckMessageIds.Begin,
            };

            return await TrySendMessage(model);
        }

        public static async Task<bool> SendSettingsPacket(SettingsMessageIds messageId)
        {
            SettingsMessageModel model = new()
            {
                MessageType = messageId,
            };

            return await PipeServer.Instance.SendMessageAsync(model.ToString());
        }

        public static async Task<bool> SendConditionalActionPacket(
            string operationId,
            ConditionalAction action)
        {
            NameValueCollection data = new()
            {
                { "operationId", operationId },
                { "actionType", action.Type.ToString() }
            };

            foreach (var parameter in action.Parameters)
                data[parameter.Name] = parameter.Value;

            ConditionalLaunchMessageModel model = new()
            {
                MessageType = ConditionalLaunchMessageIds.ExecuteAction,
                MessageData = data
            };

            return await TrySendMessage(model);
        }


        private static async Task<bool> TrySendMessage<T>(MessageBaseModel<T> message) where T : Enum 
        {
            if (!await PipeServer.Instance.SendMessageAsync(message.ToString()))
            {
                if (message.Target == PipeMessageTargetIds.Service || 
                    message.Target == PipeMessageTargetIds.CONPTY || 
                    message.Target == PipeMessageTargetIds.Settings ||
                    message.Target == PipeMessageTargetIds.Application ||
                    message.Target == PipeMessageTargetIds.ConditionalLaunch)
                {
                    var backgroundArgument = message.Target == PipeMessageTargetIds.ConditionalLaunch
                        ? "--create-no-window --exit-after-conditional-action "
                        : string.Empty;
                    RunHelper.RunAsDesktopUser(
                        Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"),
                        $"{backgroundArgument}--direct:{message}");
                }
                else 
                {
                    RunHelper.RunAsDesktopUser(Path.Combine(Utils.GetDataDirectory(), "CDPIUI.exe"), $"----ms-protocol:{message.ToString()}");
                }
            }

            return true;
        }
    }
}
