using CDPIUI.Shared.Exceptions.Interface;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions.Database
{
    public class ApplicationItemRegistrationException : Exception, ICustomException
    {
        public ApplicationItemRegistrationException() : base() { }
        public ApplicationItemRegistrationException(string message) : base(message) { }
        public ApplicationItemRegistrationException(string message, Exception inner) : base(message, inner) { }

        protected ApplicationItemRegistrationException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }

        public PrettyErrorCode PrettyErrorCode { get => PrettyErrorCode.APPLICATION_ITEM_REGISTER; }
    }
}
