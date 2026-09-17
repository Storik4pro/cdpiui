using CDPIUI.Core;
using CDPIUI.Shared.PrettyErrorConvertionService;

namespace CDPIUI.Helper.Basic
{
    public class CoreErrorsLookupService
    {
        private static CoreErrorsLookupService _instance;
        private static readonly object _lock = new();

        public static CoreErrorsLookupService Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new CoreErrorsLookupService();
                    return _instance;
                }
            }
        }

        public void Init() 
        {
            CoreEvents.Instance.CriticalCoreExceptionHappens += CoreEventsCriticalExceptionHandler;
            if (CoreEvents.Instance.LastCriticalError !=  null) CoreEventsCriticalExceptionHandler(CoreEvents.Instance.LastCriticalError);
        }

        private CoreErrorsLookupService()
        {
            
        }

        private void CoreEventsCriticalExceptionHandler(ErrorModel errorModel)
        {
            ErrorShowingHelper.RaiseCriticalException(
                errorModel.Object,
                string.Format("{0} ({1}) {2}", errorModel.ErrorCode, errorModel.HResult, errorModel.StatusCode),
                $"APPLICATION CORE EXCEPTION HAPPENS.\n Core log: {errorModel.FriendlyDescription}");
        }
    }
}
