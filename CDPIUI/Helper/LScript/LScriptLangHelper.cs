using CDPIUI.Core.LScript;
using System.Collections.Generic;
using System.Diagnostics;
using WinUI3Localizer;

namespace CDPIUI.Helper.LScript
{
    public class LScriptLangHelper : LScriptCore
    {
        public LScriptLangHelper() { }

        public new static string ExecuteScript(
            string scriptString,
            string scriptArgs = null,
            string callItemId = null,
            Dictionary<string, bool> jparams = null
            )
        {
            if (string.IsNullOrEmpty(scriptString)) return string.Empty;
            string executeResult = scriptString.Replace("$EMPTY", "");

            string scriptData = GetScriptArgs(scriptString, scriptArgs);

            if (scriptString.StartsWith("$Q_LINK"))
            {
                ILocalizer localizer = Localizer.Get();
                executeResult = localizer.GetLocalizedString($"{scriptArgs}{scriptData}");
                return executeResult;
            }

            return new LScriptCore().ExecuteScriptWork(scriptString, scriptArgs, callItemId, jparams);
        }
    }
}
