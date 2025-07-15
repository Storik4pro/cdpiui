using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
