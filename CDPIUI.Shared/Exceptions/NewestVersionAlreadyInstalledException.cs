using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions
{
    public class NewestVersionAlreadyInstalledException : System.Exception
    {
        public NewestVersionAlreadyInstalledException() : base() { }
        public NewestVersionAlreadyInstalledException(string message) : base(message) { }
        public NewestVersionAlreadyInstalledException(string message, System.Exception inner) : base(message, inner) { }

        protected NewestVersionAlreadyInstalledException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
