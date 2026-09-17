using CDPIUI.Core.Basic;
using CDPIUI.Core.Communication;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Data;
using CDPIUI.Shared.Extentions;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CDPIUI.Core.LScript
{
    public class LScriptCore
    {

        public const string ScriptGetArgsRegex = @"\$.*?\((.*?)\)";
        public const string Pattern = @"\$(STATICIMAGE|DYNAMICIMAGE|LOADDYNAMIC|GETCURRENTDIR|LOCALCONDITION|GETSRDIR)(?:\((.*?)\))?";

        public const string ConditionPattern = "{0}==true ? {1} : {2}";

        public LScriptCore() { }

        public static string GetArgumentsFromScript(string scriptString)
        {
            Match match = Regex.Match(scriptString, ScriptGetArgsRegex);
            string scriptData = "";

            if (match.Success)
            {
                scriptData = match.Groups[1].Value;
            }

            return scriptData;
        }

        public static string GetScriptArgs(string scriptString, string scriptArgs = null)
        {
            string scriptData = "";
            if (scriptString != null && scriptString.StartsWith("$"))
            {
                Match match = Regex.Match(scriptString, ScriptGetArgsRegex);

                if (match.Success)
                {
                    scriptData = match.Groups[1].Value;
                }

                if (scriptArgs != null)
                    scriptData = Regex.Replace(scriptData, @"{.*?}", scriptArgs);
            }
            return scriptData;
        }

        public static string ExecuteScript(
            string scriptString,
            string? scriptArgs = null,
            string? callItemId = null,
            Dictionary<string, bool>? jparams = null
            )
        {
            return new LScriptCore().ExecuteScriptWork(scriptString, scriptArgs, callItemId, jparams);
        }

        public virtual string ExecuteScriptWork(
            string scriptString,
            string? scriptArgs = null,
            string? callItemId = null,
            Dictionary<string, bool>? jparams = null
            )
        {
            if (string.IsNullOrEmpty(scriptString)) return string.Empty;
            string executeResult = scriptString.Replace("$EMPTY", "");
            try
            {
                if (scriptString != null && scriptString.StartsWith("$"))
                {
                    string scriptData = GetScriptArgs(scriptString, scriptArgs);

                    if (scriptString.StartsWith("$STATICIMAGE"))
                    {
                        executeResult = DefaultMessageHandler.StaticImageScript(scriptData);
                    }
                    else if (scriptString.StartsWith("$DYNAMICIMAGE"))
                    {
                        executeResult = DefaultMessageHandler.DynamicPathConverter(scriptData, scriptArgs);
                    }
                    else if (scriptString.StartsWith("$LOADDYNAMIC"))
                    {
                        executeResult = DefaultMessageHandler.LoadAllTextFromFile(
                            DefaultMessageHandler.DynamicPathConverter(scriptData));
                    }
                    else if (scriptString.StartsWith("$GETCURRENTDIR"))
                    {
                        string localItemsFolder = Directories.StoreItemsDirectory;

                        if (callItemId != null)
                        {
                            executeResult = Path.Combine(localItemsFolder, callItemId) +
                                scriptString.Replace("$GETCURRENTDIR()", "");
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
                    Logger.Instance.CreateDebugLog(nameof(LScriptCore), 
                        $"Script {scriptString} execute result is {executeResult}, {scriptData}");
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
            string? scriptArgs = null,
            string? callItemId = null,
            Dictionary<string, bool>? jparams = null
            )
        {
            if (string.IsNullOrEmpty(scriptString))
                return scriptString;

            string result = scriptString.Replace("$EMPTY", "");

            try
            {
                Debug.WriteLine(Directories.StoreDirectory);
                string localItemsFolder = Directories.StoreItemsDirectory;

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
                                replacement = DefaultMessageHandler.StaticImageScript(scriptData);
                                break;

                            case "DYNAMICIMAGE":
                                replacement = DefaultMessageHandler.DynamicPathConverter(scriptData, scriptArgs);
                                break;

                            case "LOADDYNAMIC":
                                replacement = DefaultMessageHandler.LoadAllTextFromFile(
                                    DefaultMessageHandler.DynamicPathConverter(scriptData));
                                break;

                            case "GETCURRENTDIR":
                                replacement = localItemsFolder;
                                break;

                            case "LOCALCONDITION":
                                replacement = LocalCondition(scriptData, jparams);
                                break;

                            case "GETSRDIR":
                                replacement = Directories.StoreRepoCacheDirectory;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.CreateErrorLog(nameof(LScriptCore), $"Error executing script {m.Value}: {ex}");
                        replacement = m.Value;
                    }

                    Logger.Instance.CreateDebugLog(nameof(LScriptCore), 
                        $"Script {m.Value} execute result is {replacement}, {scriptData}");
                    return replacement ?? "";
                }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(LScriptCore), $"ExecuteScriptUnsafe general error: {ex}");
            }

            return result;
        }



        public static string LocalCondition(string condition, Dictionary<string, bool>? jparams)
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
                Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"0x0 Not correct condition");
                return string.Empty;
            }

            var condExpr = condition.Substring(0, qPos).Trim();
            var trueExpr = condition.Substring(qPos + 1, cPos - qPos - 1).Trim();
            var falseExpr = condition.Substring(cPos + 1).Trim();

            bool condValue;
            var parts = condExpr.Split(new[] { "==" }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"0x1 Not correct condition");
                return string.Empty;
            }

            var varName = parts[0].Trim();
            var literal = parts[1].Trim();

            if (!jparams.TryGetValue(varName, out var varBool))
            {
                Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"Param {varName} not exist");
                return string.Empty;
            }

            var literalBool = bool.Parse(literal);
            condValue = (varBool == literalBool);

            var exprToEval = condValue ? trueExpr.Replace("$EMPTY", "") : falseExpr.Replace("$EMPTY", "");

            var resultObj = exprToEval;
            return resultObj.ToString();
        }

        public static Tuple<string, string, string, string>? GetNameOnOffValuesFromConditionString(string conditionString)
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
                Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"0x0 Not correct condition");
                return null;
            }

            var condExpr = conditionString.Substring(0, qPos).Trim();
            var trueExpr = conditionString.Substring(qPos + 1, cPos - qPos - 1).Trim();
            var falseExpr = conditionString.Substring(cPos + 1).Trim();

            var parts = condExpr.Split(new[] { "==" }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"0x1 Not correct condition");
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

        public static async Task<string?> RunScript(
            string? scriptString, 
            Dictionary<string, string>? extraArgs = null, 
            CancellationToken? cancellationToken = default)
        {
            if (string.IsNullOrEmpty(scriptString))
                return null;

            if (cancellationToken is null) cancellationToken = default;

            string result = "";

            foreach (string script in scriptString.Split(";"))
            {
                if (cancellationToken?.IsCancellationRequested ?? false) return null;

                Match match = Regex.Match(script, ScriptGetArgsRegex);
                string scriptData = "";

                if (match.Success)
                {
                    scriptData = match.Groups[1].Value;
                }

                string[] parts = scriptData.Split(", ", StringSplitOptions.RemoveEmptyEntries);
                string scriptName = parts[0];
                string[] scriptArgs = parts.Skip(1).ToArray();

                switch (scriptName)
                {
                    case "finish_component_setup":
                        FinishComponentSetup(scriptArgs);
                        break;
                    case "download_easy_designer_annotation_file":
                        if (scriptArgs.Length < 1)
                            break;
                        result += $"DOWNLOAD={DownloadEasyDesignerAnnotationFile(scriptArgs[0])}$SEPARATORedannotationfile;";
                        break;
                    case "install_msi":
                        if (scriptArgs.Length < 2)
                            break;
                        LScriptMsiHandler.InstallMsi(scriptArgs, extraArgs, cancellationToken ?? default);
                        break;
                    default:
                        Logger.Instance.CreateWarningLog(nameof(LScriptCore), $"Unknown script command: {scriptName}");
                        break;
                }
            }
            await Task.CompletedTask;

            return result;
        }

        public static string CreateCondition(string varName, string onValue, string offValue)
        {
            return string.Format(ConditionPattern, varName, onValue, offValue);
        }

        private static void FinishComponentSetup(string[] args)
        {
            if (args.Length < 1)
                return;
            string componentName = args[0];

            ComponentTasksManager.Instance.UpdateTaskList();

            if (componentName == "byedpi" || componentName == "spoofdpi" || componentName == "nodpi")
            {
                if (SettingsManager.Instance.GetValue<string>("PROXY", "proxyType") == "None")
                {
                    _ = PipeHelper.SendNotificationPacket(
                        Shared.Pipe.Models.NotificationsMessageIds.ProxySetupRequired,
                        new()
                        {
                            { "componentName", NormalizeComponentName(componentName) }
                        });
                }
            }
        }

        private static string DownloadEasyDesignerAnnotationFile(string url)
        {
            return url;
        }

        private static string NormalizeComponentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            return name.Replace("dpi", "DPI", StringComparison.OrdinalIgnoreCase).FirstCharToUpper();
        }
    }
}
