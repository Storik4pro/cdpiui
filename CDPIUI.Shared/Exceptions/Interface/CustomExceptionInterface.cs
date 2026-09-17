using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Exceptions.Interface
{
    public interface ICustomException
    {
        public PrettyErrorCode PrettyErrorCode { get; }
    }
}
