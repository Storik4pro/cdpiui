using CDPIUI.Shared.Extentions;
using System;
using System.Web;

namespace CDPIUI.Shared.Pipe.Models
{
    public class PipeModelConvertor()
    {
        public static IPipeMessage? ConvertBack(string str)
        {
            if (!Uri.TryCreate(str, UriKind.Absolute, out Uri result))
                return null;

            var parameters = HttpUtility.ParseQueryString(result.Query);

            var messageType = result.AbsolutePath.Replace("/", "");

            return result.Host.ToEnum<PipeMessageTargetIds>() switch
            {
                PipeMessageTargetIds.Service => new ServiceMessageModel()
                { MessageType = messageType.ToEnum<ServiceMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.Presentation => new PresentationMessageModel()
                { MessageType = messageType.ToEnum<PresentationMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.CONPTY => new CONPTYMessageModel()
                { MessageType = messageType.ToEnum<CONPTYMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.GoodCheck => new GoodCheckMessageModel()
                { MessageType = messageType.ToEnum<GoodCheckMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.Utils => new UtilsMessageModel()
                { MessageType = messageType.ToEnum<UtilsMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.Settings => new SettingsMessageModel()
                { MessageType = messageType.ToEnum<SettingsMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.Update => new UpdateMessageModel()
                { MessageType = messageType.ToEnum<UpdateMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.MSIInstallation => new MSIInstallationMessageModel()
                { MessageType = messageType.ToEnum<MSIInstallationMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.Proxy => new ProxyMessageModel()
                { MessageType = messageType.ToEnum<ProxyMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.CompatibilityCheck => new CompatibilityCheckMessageModel()
                { MessageType = messageType.ToEnum<CompatibilityCheckMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.Notifications => new NotificationsMessageModel()
                { MessageType = messageType.ToEnum<NotificationsMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.Application => new ApplicationMessageModel()
                { MessageType = messageType.ToEnum<ApplicationMessageIds>(), MessageData = parameters },

                PipeMessageTargetIds.ConditionalLaunch => new ConditionalLaunchMessageModel()
                { MessageType = messageType.ToEnum<ConditionalLaunchMessageIds>(), MessageData = parameters },

                _ => null,
            };
        }
    }
}
