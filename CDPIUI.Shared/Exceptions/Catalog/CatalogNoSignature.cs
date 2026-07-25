using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions.Catalog
{
    public class CatalogNoSignature : System.Exception
    {
        public CatalogNoSignature() : base() { }
        public CatalogNoSignature(string message) : base(message) { }
        public CatalogNoSignature(string message, System.Exception inner) : base(message, inner) { }

        protected CatalogNoSignature(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
