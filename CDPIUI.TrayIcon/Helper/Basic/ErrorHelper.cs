using CDPIUI.Shared.Logger;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.TrayIcon.Helper.Basic
{
    public static class ErrorHelper
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
        public override string GetPrettyErrorCode(string preffix, int hcode, ILogger? _ = null)
        {
            return base.GetPrettyErrorCode(preffix, hcode, Logger.Instance);
        }

        public override string GetPrettyErrorCode(string preffix, Exception ex, ILogger? _ = null)
        {
            return base.GetPrettyErrorCode(preffix, ex, Logger.Instance);
        }
    }
}
