using CDPIUI.Shared.Exceptions.Interface;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions.Settings
{
    public class SettingTypeNotSupported : Exception, ICustomException
    {
        public SettingTypeNotSupported() : base() { }
        public SettingTypeNotSupported(string message) : base(message) { }
        public SettingTypeNotSupported(string message, Exception inner) : base(message, inner) { }

        protected SettingTypeNotSupported(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }

        public PrettyErrorCode PrettyErrorCode { get => PrettyErrorCode.SETTING_TYPE_NOT_SUPPORTED; }
    }
}
