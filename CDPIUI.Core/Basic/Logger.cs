using CDPIUI.Core.Data;
using CDPIUI.Shared.Extentions;
using CDPIUI.Shared.Logger;

namespace CDPIUI.Core.Basic
{
    public class Logger : LoggerBase, ILogger
    {
        private static Logger? _instance;
        private static readonly object _lock = new object();

        public static Logger Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new Logger();
                    return _instance;
                }
            }
        }

        public Logger() : base(Directories.DataDirectory, GetSelevirity()) { }

        

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
