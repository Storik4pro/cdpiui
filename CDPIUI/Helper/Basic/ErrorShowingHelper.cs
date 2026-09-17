using CDPIUI.Core.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Basic
{
    /// <summary>
    /// Shows some error messages
    /// </summary>
    public class ErrorShowingHelper
    {
        /// <summary>
        /// Raise critical exception. That show modal error window, after closing of which
        /// exiting from GUI (CDPUI)
        /// </summary>
        /// <param name="sender">Who called this method</param>
        /// <param name="errorCode">Error code 
        /// (use <see cref="CDPIUI.Shared.PrettyErrorConvertionService.PrettyErrorCode"/>
        /// as <see cref="string"/> instead of custom error)
        /// </param>
        /// <param name="why">Why this happens</param>
        public static void RaiseCriticalException(string sender, string errorCode, string why)
        {
            Logger.Instance.CreateErrorLog(sender, $"{errorCode} => {why}");
            CriticalErrorHandlerWindow window = new(where: sender, why: $"Because exception happens \n{why}", errorCode: errorCode);
            window.Activate();
        }

        /// <summary>
        /// Raise critical exception. That show modal error window, after closing of which
        /// exiting from GUI (CDPUI)
        /// </summary>
        /// <param name="sender">Who called this method</param>
        /// <param name="exception">Exception</param>
        public static void RaiseCriticalException(string sender, System.Exception exception)
        {
            string readyToUseErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("ERR_INTERNAL", exception);

            Logger.Instance.CreateErrorLog(sender, $"{readyToUseErrorCode} => {exception}");
            CriticalErrorHandlerWindow window = new(where: sender, why: $"Because exception happens \n{exception}", errorCode: readyToUseErrorCode);
            window.Activate();
        }
    }
}
