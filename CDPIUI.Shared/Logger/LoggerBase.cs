using System;
using System.Diagnostics;
using System.IO;

namespace CDPIUI.Shared.Logger
{
    public enum LogSelevirity
    {
        DEBG,
        INFO,
        WARN,
        CRIT,
    }


    /// <summary>
    /// Initialize new logger
    /// </summary>
    /// <param name="actualPath">Path, where logs are storaged</param>
    /// <param name="logLevel">Logging level</param>
    public class LoggerBase(string? actualPath, LogSelevirity logLevel)
    {
        /// <summary>
        /// Path, where logs are storaged
        /// </summary>
        public readonly string? ActualPath = actualPath;

        /// <summary>
        /// Logging level
        /// </summary>
        public readonly LogSelevirity? LogLevel = logLevel;
        private static readonly object _logLock = new object();

        private void LogWrite(string fileName, string logMessage)
        {
            if (string.IsNullOrEmpty(ActualPath)) return;
            try
            {
                string logFileDir = Path.Combine(ActualPath, LoggerResources.LogsFolder);
                string logFilePath = Path.Combine(logFileDir, $"{fileName}{LoggerResources.LogsFileExtention}");

                if (!Directory.Exists(logFileDir)) Directory.CreateDirectory(logFileDir);
                if (!File.Exists(logFilePath))
                {
                    File.WriteAllText(logFilePath, LoggerResources.LoggerReadyMessage + Environment.NewLine);
                }

                using StreamWriter w = File.AppendText(logFilePath);
                WriteMessageToTextWriter(logMessage, w);
            }
            catch { }
        }

        private static void WriteMessageToTextWriter(string logMessage, TextWriter txtWriter)
        {
            try
            {
                txtWriter.WriteLine("[{0} {1}] {2}", DateTime.Now.ToLongTimeString(),
                    DateTime.Now.ToShortDateString(), logMessage);
            }
            catch { }
        }

        private void CreateLog(string message, LogSelevirity severity, string sender)
        {
            lock (_logLock)
            {
                string logMessage = $"[{sender}] [{severity}] {message}";
                Debug.WriteLine(logMessage);
                bool write = true;
                try
                {
                    if (severity - LogLevel <= 0)
                    {
                        write = true;
                    }
                    else
                    {
                        write = false;
                    }
                }
                catch { }

                if (write)
                    LogWrite(sender, logMessage);
            }
        }

        /// <summary>
        /// Create new DEBUG log message
        /// </summary>
        /// <param name="sender">Sender of message</param>
        /// <param name="message">Log message</param>
        public void CreateDebugLog(string sender, string message)
        {
            CreateLog(message, LogSelevirity.DEBG, sender);
        }

        /// <summary>
        /// Create new INFO log message
        /// </summary>
        /// <param name="sender">Sender of message</param>
        /// <param name="message">Log message</param>
        public void CreateInfoLog(string sender, string message)
        {
            CreateLog(message, LogSelevirity.INFO, sender);
        }

        /// <summary>
        /// Create new WARNING log message
        /// </summary>
        /// <param name="sender">Sender of message</param>
        /// <param name="message">Log message</param>
        public void CreateWarningLog(string sender, string message)
        {
            CreateLog(message, LogSelevirity.WARN, sender);
        }

        /// <summary>
        /// Create new ERROR log message (marked as CRIT)
        /// </summary>
        /// <param name="sender">Sender of message</param>
        /// <param name="message">Log message</param>
        public void CreateErrorLog(string sender, string message)
        {
            CreateLog(message, LogSelevirity.CRIT, sender);
        }
    }
}
