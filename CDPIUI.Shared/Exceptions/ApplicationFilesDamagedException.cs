using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions
{
    public class ApplicationFilesDamagedException : System.Exception
    {
        public ApplicationFilesDamagedException() : base() { }
        public ApplicationFilesDamagedException(string message) : base(message) { }
        public ApplicationFilesDamagedException(string message, System.Exception inner) : base(message, inner) { }

        protected ApplicationFilesDamagedException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
