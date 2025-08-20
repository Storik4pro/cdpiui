using ABI.System;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GoodbyeDPI_UI.Helper.ErrorsHelper;

namespace GoodbyeDPI_UI.Helper
{
    public class Logger
    {
        private static Logger _instance;
        private static readonly object _lock = new object();

        public static Logger Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new Logger();
                    return _instance;
                }
            }
        }

        public Logger()
        {

        }

        private static readonly object _logLock = new object();

        public void CreateLog(string message, string severity, string sender) 
        {
            lock (_logLock)
            {
                Debug.WriteLine($"[{sender}] [{severity}] {message}");
            }
        }

        public void CreateDebugLog(string sender, string message)
        {
            CreateLog(message, "DEBG", sender);
        }

        public void CreateInfoLog(string sender, string message)
        {
            CreateLog(message, "INFO", sender);
        }

        public void CreateWarningLog(string sender, string message)
        {
            CreateLog(message, "WARN", sender);
        }

        public void CreateErrorLog(string sender, string message)
        {
            CreateLog(message, "CRIT", sender);
        }

        public void RaiseCriticalException(string sender, string errorCode, string why)
        {
            CreateErrorLog(sender, $"{errorCode} => {why}");
            CriticalErrorHandlerWindow window = new(where: sender, why: $"Because exception happens \n{why}", errorCode: errorCode);
            window.Activate();
        }

        public void RaiseCriticalException(string sender, System.Exception exception)
        {
            var prettyErrorCode = ErrorHelper.MapExceptionToCode(exception, out uint? hr);
            string code = prettyErrorCode.ToString();

            string readyToUseErrorCode;
            if (hr != null)
            {
                string hrHex = $"0x{hr.Value:X8}";
                readyToUseErrorCode = $"ERR_INTERNAL_{code} ({hrHex})";
            }
            else
            {
                readyToUseErrorCode = $"ERR_INTERNAL_{code}";
            }
            CreateErrorLog(sender, $"{readyToUseErrorCode} => {exception}");
            CriticalErrorHandlerWindow window = new(where:sender, why:$"Because exception happens \n{exception}", errorCode:readyToUseErrorCode);
            window.Activate();
        }
    }
}
