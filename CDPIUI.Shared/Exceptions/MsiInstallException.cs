using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions
{
    public class MsiInstallException : System.Exception
    {
        public MsiInstallException() : base() { }
        public MsiInstallException(string message) : base(message) { }
        public MsiInstallException(string message, System.Exception inner) : base(message, inner) { }

        protected MsiInstallException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
