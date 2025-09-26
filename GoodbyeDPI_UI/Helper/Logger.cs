using ABI.System;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static CDPI_UI.Helper.ErrorsHelper;
using Exception = System.Exception;

namespace CDPI_UI.Helper
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

        private string m_exePath;

        public Logger()
        {

        }

        private static readonly object _logLock = new object();

        private void LogWrite(string fileName, string logMessage)
        {
            m_exePath = StateHelper.GetDataDirectory();
            
            try
            {
                string logFileDir = Path.Combine(m_exePath, "Logs");
                string logFilePath = Path.Combine(logFileDir, $"{fileName}.log");

                if (!Directory.Exists(logFileDir)) Directory.CreateDirectory(logFileDir);
                if (!File.Exists(logFilePath))
                {
                    File.WriteAllText(logFilePath, "Logger is ready-to-work" + Environment.NewLine);
                }

                using StreamWriter w = File.AppendText(logFilePath);
                Log(logMessage, w);
            }
            catch 
            {
            }
        }

        private static void Log(string logMessage, TextWriter txtWriter)
        {
            try
            {
                txtWriter.WriteLine("[{0} {1}] {2}", DateTime.Now.ToLongTimeString(), 
                    DateTime.Now.ToShortDateString(), logMessage);
            }
            catch 
            {
            }
        }

        public void CreateLog(string message, string severity, string sender) 
        {
            lock (_logLock)
            {
                string logMessage = $"[{sender}] [{severity}] {message}";
                Debug.WriteLine(logMessage);
                bool write = true;
                try
                {
                    string _sev = SettingsManager.Instance.GetValue<string>("DEBUG", "logLevel");
                    if (_sev == "DEBG") write = true;
                    else if (_sev == "INFO" && severity != "DEBG") write = true;
                    else if (_sev == "WARN" && severity != "DEBG" && severity != "INFO") write = true;
                    else if (_sev == "CRIT" && severity != "DEBG" && severity != "INFO" && severity != "WARN") write = true;
                    else write = false;
                }
                catch { }

                if (write) 
                    LogWrite(sender, logMessage);
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
