using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Logger
{
    public interface ILogger 
    {
        static LoggerBase? Instance { get; }

        void CreateDebugLog(string sender, string message);
        void CreateInfoLog(string sender, string message);
        void CreateWarningLog(string sender, string message);
        void CreateErrorLog(string sender, string message);

    }
}
