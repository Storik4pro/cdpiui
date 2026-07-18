using CDPIUI.Shared.Logger;
using static CDPIUI.Helper.ErrorsHelper;

namespace CDPIUI.Helper.Basic
{
    public class Logger : LoggerBase, ILoggerInterface
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

        public Logger() : base(StateHelper.GetDataDirectory(), GetSelevirity()) { }


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
