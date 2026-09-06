using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions
{
    public class AsyncOperationException : System.Exception
    {
        public AsyncOperationException() : base() { }
        public AsyncOperationException(string message) : base(message) { }
        public AsyncOperationException(string message, System.Exception inner) : base(message, inner) { }

        protected AsyncOperationException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
