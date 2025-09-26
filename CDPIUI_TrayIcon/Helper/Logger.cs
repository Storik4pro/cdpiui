using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI_TrayIcon.Helper
{
    public class Logger
    {
        private static Logger? _instance;
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

        private string? m_exePath;

        public Logger()
        {

        }

        private static readonly object _logLock = new object();

        private void LogWrite(string fileName, string logMessage)
        {
            m_exePath = Utils.GetDataDirectory();

            try
            {
                string logFileDir = Path.Combine(m_exePath, "Logs");
                string logFilePath = Path.Combine(logFileDir, $"{fileName}.log");

                if (!Directory.Exists(logFileDir)) Directory.CreateDirectory(logFileDir);
                if (!File.Exists(logFilePath))
                {
                    File.Create(logFilePath);
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
    }
}
