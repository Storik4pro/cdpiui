using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.PrettyErrorConvertionService
{
    public class ErrorModel
    {
        public required string ErrorCode { get; set; }
        public string? Object { get; set; }

        public string? FriendlyDescription { get; set; }
        public Exception? Exception { get; set; }
        public string? StatusCode { get; set; }
        public string? HResult { get; set; }

        public static ErrorModel OnlyErrorCode(string errorCode) => new() { ErrorCode = errorCode };
        public static ErrorModel OnlyErrorCode(PrettyErrorCode errorCode) => new() { ErrorCode = errorCode.ToString() };
    }
}
