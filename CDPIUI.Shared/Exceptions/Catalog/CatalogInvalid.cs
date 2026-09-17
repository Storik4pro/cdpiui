using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions.Catalog
{
    public class CatalogInvalid : System.Exception
    {
        public CatalogInvalid() : base() { }
        public CatalogInvalid(string message) : base(message) { }
        public CatalogInvalid(string message, System.Exception inner) : base(message, inner) { }

        protected CatalogInvalid(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
