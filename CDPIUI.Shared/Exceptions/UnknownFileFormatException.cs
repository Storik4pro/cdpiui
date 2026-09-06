using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions
{
    public class UnknownFileFormatException : System.Exception
    {
        public UnknownFileFormatException() : base() { }
        public UnknownFileFormatException(string message) : base(message) { }
        public UnknownFileFormatException(string message, System.Exception inner) : base(message, inner) { }

        protected UnknownFileFormatException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
