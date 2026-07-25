using CDPIUI.Shared.Logger;
using CDPIUI.Shared.Extentions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.TrayIcon.Helper.Basic
{
    public class Logger : LoggerBase, ILogger
    {
        private static Logger? _instance;
        private static readonly object _lock = new();

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

        

        public Logger() : base(Utils.GetDataDirectory(), GetSelevirity()) { }

        private static LogSelevirity GetSelevirity()
        {
#if DEBUG
            return LogSelevirity.DEBG;
#else
            return SettingsManager.Instance.GetValue<string>("DEBUG", "logLevel").ToEnum<LogSelevirity>(LogSelevirity.DEBG);
#endif
        }
    }
}
