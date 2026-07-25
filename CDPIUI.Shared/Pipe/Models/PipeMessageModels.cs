using CDPIUI.Shared.Extentions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;

namespace CDPIUI.Shared.Pipe.Models
{
    public class MessageBaseModel<T>(PipeMessageTargetIds target) : IPipeMessageBase<T, NameValueCollection> where T : Enum
    {
        public PipeMessageTargetIds Target { get; private set; } = target;

        public required T MessageType { get; set; }
        public int IntMessageType => Convert.ToInt32(MessageType);

        public NameValueCollection? MessageData { get; set; }

        public override string ToString()
        {
            if (MessageData is null)
                return $"{SharedConstants.Schema}://{Target}/{MessageType}";

            string @params = string.Empty;

            foreach (var name in MessageData.AllKeys)
            {
                @params += $"{name}={MessageData[name]}&";
            }

            return $"{SharedConstants.Schema}://{Target}/{MessageType}?{@params}";
        }
    }

    public class ServiceMessageModel : MessageBaseModel<ServiceMessageIds>
    {
        public ServiceMessageModel() : base(PipeMessageTargetIds.Service) { }

        public static ServiceMessageModel ConnectionSuccessful() =>
            new() { MessageType = ServiceMessageIds.ConnectOK };
        public static ServiceMessageModel AuthSuccessful() =>
            new() { MessageType = ServiceMessageIds.AuthOK };
        public static ServiceMessageModel AuthFailure() =>
            new() { MessageType = ServiceMessageIds.AuthFAIL };
        public static ServiceMessageModel RequestAuth(string guid) =>
            new() { MessageType = ServiceMessageIds.RequestAuth, MessageData = new()
            {
                { "GUID", guid }
            }};
    }

    public class PresentationMessageModel : MessageBaseModel<PresentationMessageIds>
    {
        public PresentationMessageModel() : base(PipeMessageTargetIds.Presentation) { }
    }

    public class CONPTYMessageModel : MessageBaseModel<CONPTYMessageIds>
    {
        public CONPTYMessageModel() : base(PipeMessageTargetIds.CONPTY) { }
    }

    public class GoodCheckMessageModel : MessageBaseModel<GoodCheckMessageIds>
    {
        public GoodCheckMessageModel() : base(PipeMessageTargetIds.GoodCheck) { }
    }

    public class UtilsMessageModel : MessageBaseModel<UtilsMessageIds>
    {
        public UtilsMessageModel() : base(PipeMessageTargetIds.Utils) { }
    }

    public class SettingsMessageModel : MessageBaseModel<SettingsMessageIds>
    {
        public SettingsMessageModel() : base(PipeMessageTargetIds.Settings) { }
    }

    public class UpdateMessageModel : MessageBaseModel<UpdateMessageIds>
    {
        public UpdateMessageModel() : base(PipeMessageTargetIds.Update) { }
    }

    public class MSIInstallationMessageModel : MessageBaseModel<MSIInstallationMessageIds>
    {
        public MSIInstallationMessageModel() : base(PipeMessageTargetIds.MSIInstallation) { }
    }

    public class ProxyMessageModel : MessageBaseModel<ProxyMessageIds>
    {
        public ProxyMessageModel() : base(PipeMessageTargetIds.Proxy) { }
    }

    public class CompatibilityCheckMessageModel : MessageBaseModel<CompatibilityCheckMessageIds>
    {
        public CompatibilityCheckMessageModel() : base(PipeMessageTargetIds.CompatibilityCheck) { }
    }

    public class NotificationsMessageModel : MessageBaseModel<NotificationsMessageIds>
    {
        public NotificationsMessageModel() : base(PipeMessageTargetIds.Notifications) { }
    }

    public class ApplicationMessageModel : MessageBaseModel<ApplicationMessageIds>
    {
        public ApplicationMessageModel() : base(PipeMessageTargetIds.Application) { }
    }
}
