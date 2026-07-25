using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions
{
    public class UnknownException : System.Exception
    {
        public UnknownException() : base() { }
        public UnknownException(string message) : base(message) { }
        public UnknownException(string message, System.Exception inner) : base(message, inner) { }

        protected UnknownException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
