using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core
{
    public class CoreEvents
    {
        private static CoreEvents? _instance;
        private static readonly object _lock = new();

        public static CoreEvents Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new CoreEvents();
                    return _instance;
                }
            }
        }

        public ErrorModel? LastCriticalError;

        public event Action<ErrorModel>? CriticalCoreExceptionHappens;

        public void InvokeCriticalCoreExceptionHappens(Exception ex)
        {
            LastCriticalError = ErrorsHelper.Convertor.GetErrorModel("CDPIUI.CORE", ex);
            CriticalCoreExceptionHappens?.Invoke(LastCriticalError);
        }

        public void InvokeCriticalCoreExceptionHappens(ErrorModel model)
        {
            LastCriticalError = model;
            CriticalCoreExceptionHappens?.Invoke(LastCriticalError);
        }
    }
}
