using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions.Database
{
    public class AddonNotInstalledException : Exception
    {
        public AddonNotInstalledException() : base() { }
        public AddonNotInstalledException(string message) : base(message) { }
        public AddonNotInstalledException(string message, Exception inner) : base(message, inner) { }

        protected AddonNotInstalledException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
