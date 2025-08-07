using GoodbyeDPI_UI.Helper.Static;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoodbyeDPI_UI.Helper.LScript
{
    public class LScriptLangHelper
    {

        private const string ScriptGetArgsRegex = @"\$.*?\((.*?)\)";

        public LScriptLangHelper() { }


        public static string ExecuteScript(
            string scriptString, 
            string scriptArgs = null, 
            string callItemId = null,
            Dictionary<string, bool> jparams = null
            )
        {
            string executeResult = scriptString;
            try
            {
                if (scriptString != null && scriptString.StartsWith("$"))
                {
                    Match match = Regex.Match(scriptString, ScriptGetArgsRegex);
                    string scriptData = "";

                    if (match.Success)
                    {
                        scriptData = match.Groups[1].Value;
                    }

                    if (scriptArgs != null)
                        scriptData = Regex.Replace(scriptData, @"{.*?}", scriptArgs);

                    if (scriptString.StartsWith("$STATICIMAGE"))
                    {
                        executeResult = Static.Utils.StaticImageScript(scriptData);
                    }
                    else if (scriptString.StartsWith("$DYNAMICIMAGE"))
                    {
                        executeResult = Static.Utils.DynamicPathConverter(scriptData);
                    }
                    else if (scriptString.StartsWith("$LOADDYNAMIC"))
                    {
                        executeResult = Static.Utils.LoadAllTextFromFile(Static.Utils.DynamicPathConverter(scriptData));
                    }
                    else if (scriptString.StartsWith("$GETCURRENTDIR"))
                    {
                        string localAppData = AppDomain.CurrentDomain.BaseDirectory;
                        string localItemsFolder = Path.Combine(
                            localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName);

                        if (callItemId != null)
                        {
                            executeResult = Path.Combine(localItemsFolder, callItemId) + scriptString.Replace("$GETCURRENTDIR()", "");
                        }
                        else
                        {
                            executeResult = localItemsFolder + scriptString.Replace("$GETCURRENTDIR()", "");
                        }
                    }
                    else if (scriptString.StartsWith("$LOCALCONDITION"))
                    {
                        executeResult = LocalCondition(scriptData, jparams);
                    }
                    Logger.Instance.CreateDebugLog(nameof(UIHelper), $"Script {scriptString} execute result is {executeResult}, {scriptData}");
                }
            }
            catch (Exception ex)
            {
                // pass
            }

            return executeResult;
        }

        private static string LocalCondition(string condition, Dictionary<string, bool> jparams)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return string.Empty;

            var qPos = condition.IndexOf('?');
            var cPos = condition.IndexOf(':');
            if (qPos < 0 || cPos < 0 || cPos < qPos)
            {
                Logger.Instance.CreateWarningLog(nameof(LScriptLangHelper), $"0x0 Not correct condition");
                return string.Empty;
            }

            var condExpr = condition.Substring(0, qPos).Trim();
            var trueExpr = condition.Substring(qPos + 1, cPos - qPos - 1).Trim();
            var falseExpr = condition.Substring(cPos + 1).Trim();

            bool condValue;
            var parts = condExpr.Split(new[] { "==" }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                Logger.Instance.CreateWarningLog(nameof(LScriptLangHelper), $"0x1 Not correct condition");
                return string.Empty;
            }

            var varName = parts[0].Trim();
            var literal = parts[1].Trim();

            if (!jparams.TryGetValue(varName, out var varBool))
            {
                Logger.Instance.CreateWarningLog(nameof(LScriptLangHelper), $"Param {varName} not exist");
                return string.Empty;
            }

            var literalBool = bool.Parse(literal);
            condValue = (varBool == literalBool);

            var exprToEval = condValue ? trueExpr : falseExpr;

            var resultObj = exprToEval;
            return resultObj.ToString();
        }

        
    }
}
