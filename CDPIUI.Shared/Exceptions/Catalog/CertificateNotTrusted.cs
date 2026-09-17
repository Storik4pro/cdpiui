using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions.Catalog
{
    public class CertificateNotTrusted : System.Exception
    {
        public CertificateNotTrusted() : base() { }
        public CertificateNotTrusted(string message) : base(message) { }
        public CertificateNotTrusted(string message, System.Exception inner) : base(message, inner) { }

        protected CertificateNotTrusted(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
