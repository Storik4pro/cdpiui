using CDPIUI.Shared.Exceptions.Interface;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions.Settings
{
    public class SettingNotFoundException : Exception, ICustomException
    {
        public SettingNotFoundException() : base() { }
        public SettingNotFoundException(string message) : base(message) { }
        public SettingNotFoundException(string message, Exception inner) : base(message, inner) { }

        protected SettingNotFoundException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }

        public PrettyErrorCode PrettyErrorCode { get => PrettyErrorCode.SETTING_NOT_FOUND; }
    }
}
