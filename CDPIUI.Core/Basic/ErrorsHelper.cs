using CDPIUI.Shared.Logger;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using static CDPIUI.Core.Basic.ErrorsHelper;
using static System.Net.WebRequestMethods;

namespace CDPIUI.Core.Basic
{
    public static class ErrorsHelper
    {
        private static CustomErrorConvertor? _convertor;
        private static readonly object _lock = new object();

        public static CustomErrorConvertor Convertor
        {
            get
            {
                lock (_lock)
                {
                    _convertor ??= new CustomErrorConvertor();
                    return _convertor;
                }
            }
        }

    }

    public class CustomErrorConvertor : ErrorConvertor
    {
        public override PrettyErrorCode MapExceptionToCode(Exception ex, out uint? rawHResult, out int? statusCode)
        {
            statusCode = null;
            rawHResult = null;

            for (Exception current = ex; current != null; current = current.InnerException)
            {
                switch (current)
                {
                    case HttpRequestException:
                        if (current is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
                        {
                            statusCode = (int)httpEx.StatusCode.Value;
                            return PrettyErrorCode.UNEXPECTED_STATUS_CODE;
                        }
                        rawHResult = unchecked((uint)current.HResult);
                        return PrettyErrorCode.HTTP_REQUEST_EXCEPTION;
                }
            }
            return base.MapExceptionToCode(ex, out rawHResult, out statusCode);
        }

        public override string GetPrettyErrorCode(string preffix, int hcode, ILogger? _ = null)
        {
            return base.GetPrettyErrorCode(preffix, hcode, Logger.Instance);
        }

        public override string GetPrettyErrorCode(string preffix, Exception ex, ILogger? _ = null)
        {
            return base.GetPrettyErrorCode(preffix, ex, Logger.Instance);
        }

        public override ErrorModel GetErrorModel(string @object, Exception ex, ILogger? _ = null)
        {
            return base.GetErrorModel(@object, ex, null);
        }
    }
}
