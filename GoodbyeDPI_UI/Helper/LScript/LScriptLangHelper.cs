using GoodbyeDPI_UI.Helper.Static;
using MS.WindowsAPICodePack.Internal;
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
        private const string Pattern = @"\$(STATICIMAGE|DYNAMICIMAGE|LOADDYNAMIC|GETCURRENTDIR|LOCALCONDITION)(?:\((.*?)\))?";

        public LScriptLangHelper() { }


        public static string ExecuteScript(
            string scriptString, 
            string scriptArgs = null, 
            string callItemId = null,
            Dictionary<string, bool> jparams = null
            )
        {
            string executeResult = scriptString.Replace("$EMPTY", "");
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
                        string localAppData = StateHelper.GetDataDirectory();
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

        public static string ExecuteScriptUnsafe(
            string scriptString,
            string scriptArgs = null,
            string callItemId = null,
            Dictionary<string, bool> jparams = null
            )
        {
            if (string.IsNullOrEmpty(scriptString))
                return scriptString;

            string result = scriptString.Replace("$EMPTY", "");

            try
            {
                string localAppData = StateHelper.GetDataDirectory();
                string localItemsFolder = Path.Combine(
                    localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName);

                if (callItemId != null)
                {
                    localItemsFolder = Path.Combine(localItemsFolder, callItemId);
                }


                result = Regex.Replace(result, Pattern, (Match m) =>
                {
                    string command = m.Groups[1].Value.ToUpperInvariant();
                    string rawArg = m.Groups[2].Success ? m.Groups[2].Value : "";
                    string scriptData = rawArg;

                    if (!string.IsNullOrEmpty(scriptArgs))
                    {
                        scriptData = Regex.Replace(scriptData, @"{.*?}", scriptArgs);
                    }

                    string replacement = m.Value;

                    try
                    {
                        switch (command)
                        {
                            case "STATICIMAGE":
                                replacement = Static.Utils.StaticImageScript(scriptData);
                                break;

                            case "DYNAMICIMAGE":
                                replacement = Static.Utils.DynamicPathConverter(scriptData);
                                break;

                            case "LOADDYNAMIC":
                                replacement = Static.Utils.LoadAllTextFromFile(Static.Utils.DynamicPathConverter(scriptData));
                                break;

                            case "GETCURRENTDIR":
                                replacement = localItemsFolder;
                                break;

                            case "LOCALCONDITION":
                                replacement = LocalCondition(scriptData, jparams);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.CreateErrorLog(nameof(UIHelper), $"Error executing script {m.Value}: {ex}");
                        replacement = m.Value;
                    }

                    Logger.Instance.CreateDebugLog(nameof(UIHelper), $"Script {m.Value} execute result is {replacement}, {scriptData}");
                    return replacement ?? "";
                }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(UIHelper), $"ExecuteScriptUnsafe general error: {ex}");
            }

            return result;
        }

        private static string LocalCondition(string condition, Dictionary<string, bool> jparams)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return string.Empty;

            var qPos = condition.IndexOf('?');
            int cPos;
            if (condition.Contains("$SEPARATOR"))
            {
                cPos = condition.IndexOf("$SEPARATOR");
                condition = condition.Replace("$SEPARATOR", "$");
            }
            else
                cPos = condition.IndexOf(':');
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

            var exprToEval = condValue ? trueExpr.Replace("$EMPTY", "") : falseExpr.Replace("$EMPTY", "");

            var resultObj = exprToEval;
            return resultObj.ToString();
        }

        public static Tuple<string, string, string, string> GetNameOnOffValuesFromConditionString(string conditionString)
        {
            conditionString = Regex.Replace(conditionString, Pattern, (Match m) =>
            {
                string command = m.Groups[1].Value.ToUpperInvariant();
                string rawArg = m.Groups[2].Success ? m.Groups[2].Value : "";
                string scriptData = rawArg;

                return scriptData;
            });

            if (string.IsNullOrWhiteSpace(conditionString))
                return null;

            var qPos = conditionString.IndexOf('?');
            int cPos;
            if (conditionString.Contains("$SEPARATOR"))
            {
                cPos = conditionString.IndexOf("$SEPARATOR");
                conditionString = conditionString.Replace("$SEPARATOR", "$");
            }
            else
                cPos = conditionString.IndexOf(':');

            if (qPos < 0 || cPos < 0 || cPos < qPos)
            {
                Logger.Instance.CreateWarningLog(nameof(LScriptLangHelper), $"0x0 Not correct condition");
                return null;
            }

            var condExpr = conditionString.Substring(0, qPos).Trim();
            var trueExpr = conditionString.Substring(qPos + 1, cPos - qPos - 1).Trim();
            var falseExpr = conditionString.Substring(cPos + 1).Trim();

            var parts = condExpr.Split(new[] { "==" }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                Logger.Instance.CreateWarningLog(nameof(LScriptLangHelper), $"0x1 Not correct condition");
                return null;
            }

            string varName = parts[0].Trim();
            string conditionVarName = "";

            Match match = Regex.Match(varName, @"^%(.*?)%=");
            if (match.Success)
            {
                conditionVarName = match.Groups[1].Value;
            }
            varName = Regex.Replace(varName, @"^%.*?%=", "");


            return Tuple.Create(varName, conditionVarName, trueExpr, falseExpr);
        }

        
    }
}
