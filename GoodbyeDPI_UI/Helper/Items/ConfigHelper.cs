using CDPI_UI.Controls.Dialogs.CreateConfigHelper;
using CDPI_UI.Helper.LScript;
using CDPI_UI.Helper.Static;
using Newtonsoft.Json;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml.Linq;
using Windows.Devices.Power;

namespace CDPI_UI.Helper.Items
{
    public class AvailableVarValues
    {
        public string Comment { get; set; } = "";
        public string VarName { get; set; }
        public int CurrentValueIndex { get; set; }
        public List<string> Values { get; set; }
    }
    public class OldConfigItem
    {
        public string custom_parameters { get; set; }
    }
    public class ConfigItem
    {
        public string file_name;
        public string packId;
        public string meta { get; set; }
        public List<string> target { get; set; }
        public string name {  get; set; }
        public string not_converted_name;
        public Dictionary<string, bool> jparams { get; set; }
        public List<string> variables { get; set; }
        public Dictionary<string, string> commaVars { get; set; }
        public List<AvailableVarValues> availableCommaVarsValues { get; set; }
        public string startup_string { get; set; }
        public List<string> toggle_lists;
    }

    public class VariableItem
    {
        public string variable_name;
        public string name;
        public bool value;
    }

    public class SiteListItem
    {
        public string Name;
        public string Type;
        public string FilePath;
        public List<string> ApplyParams;
        public List<string> PrettyApplyParams;
    }

    public class ConfigInitItem
    {
        public List<string> toggleListAvailable { get; set; }
        public Dictionary<string, string> localized_strings_directory { get; set; }
    }
    public class ConfigHelper
    {
        private const string BatStartRegex = @"start\s+(?:"".*?"")?"".*?""\s+(((?:/min){0,1}\s+"".*?"")|(\S*))?(.*)\^?$";
        private const string VarInVarRegex = @"!(.*?)!";
        private const string VarInCommaRegex = @"%(.*?)%";

        private readonly List<ConfigItem> Items = [];

        private string Target;

        private class ConfigLocaleHelper
        {
            public string LocaleName;
            public Dictionary<string, string> keyValuePairs;
        }
        private List<Tuple<string, ConfigLocaleHelper>> ConfigLocaleHelpers = [];
        

        private readonly object _lock = new object();
        public ConfigHelper(string target) 
        {
            Target = target;
        }

        private List<ConfigItem> InitConfigDirectory(DatabaseStoreItem item)
        {
            string directory = item.Directory;
            string id = item.Id;
            List<ConfigItem> configItems = new List<ConfigItem>();

            string initFile = Path.Combine(directory, "init.json");

            if (!File.Exists(initFile)) 
                return configItems;

            ConfigInitItem configInitItem = Utils.LoadJson<ConfigInitItem>(initFile);

            string[] jsonFiles = Directory.GetFiles(directory, "*.json");

            ConfigLocaleHelper localeHelper = new ConfigLocaleHelper();
            ConfigLocaleHelpers.Add(Tuple.Create(id, localeHelper));

            InitLocaleHelper(localeHelper, configInitItem.localized_strings_directory, directory, Utils.GetStoreLikeLocale());

            foreach (string jsonFile in jsonFiles)
            {
                if (Path.GetFileName(jsonFile).StartsWith("init"))
                    continue;

                try
                {
                    ConfigItem configItem = Utils.LoadJson<ConfigItem>(jsonFile);

                    Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Now work at {jsonFile}");

                    if (configItem.target[0] != Target)
                        continue;

                    var result = Regex.Replace(
                        configItem.name,
                        @"\$.*?\((.*?)\)",
                        match =>
                        {
                            var key = match.Groups[1].Value;
                            if (configInitItem.localized_strings_directory == null)
                                return key;

                            var localized = GetLocalizedConfigNameString(key, Utils.GetStoreLikeLocale(), directory, configInitItem.localized_strings_directory, localeHelper);
                            return localized;
                        }
                    );

                    configItem.not_converted_name = configItem.name;
                    configItem.name = result;
                    configItem.file_name = Path.GetFileName(jsonFile);
                    configItem.packId = id;
                    configItem.toggle_lists = configInitItem.toggleListAvailable;

                    configItems.Add(configItem);
                }
                catch (Exception ex) 
                {
                    Logger.Instance.CreateWarningLog(nameof(ConfigHelper), $"Error happens: {ex}");
                }
            }
            ConfigItem[] array = [.. configItems];
            Array.Sort<ConfigItem>(array, new Utils.LogicalComparer());

            return [.. array];
        }

        public void Init()
        {
            lock (_lock)
            {
                Items.Clear();
                ConfigLocaleHelpers.Clear();
                List<DatabaseStoreItem> configItems = DatabaseHelper.Instance.GetItemsByType("configlist");

                List<DatabaseStoreItem> itemsToCheck = new List<DatabaseStoreItem>();

                foreach (DatabaseStoreItem item in configItems)
                {
                    if (!Path.Exists(item.Directory))
                        continue;

                    itemsToCheck.Add(item);
                }


                foreach (DatabaseStoreItem item in itemsToCheck)
                {
                    Items.AddRange(InitConfigDirectory(item));
                }
            }
        }

        public List<ConfigItem> GetConfigItems()
        {
            lock (_lock)
            {
                return Items;
            }
        }

        public void ChangeVariableValue(string filename, string packId, string key, bool value)
        {
            var configItem = Items.FirstOrDefault(
                x => string.Equals(x.packId, packId, StringComparison.Ordinal) && string.Equals(x.file_name, filename, StringComparison.Ordinal)
            );

            if (configItem == null)
                return;

            configItem.jparams[key] = value;

            _ = SaveConfigItem(filename, packId, configItem);
        }

        public static async Task SaveConfigItem(string filename, string packId, ConfigItem item)
        {
            string folder = GetItemFolderFromPackId(packId);
            string fileName = Path.Combine(folder, filename);

            item.name = item.not_converted_name;

            string jsonString = System.Text.Json.JsonSerializer.Serialize(item);
            Logger.Instance.CreateDebugLog(nameof(ConfigHelper), jsonString);
            File.WriteAllText(fileName, jsonString);

            await Task.CompletedTask;
        }

        public string GetLocalizedConfigVarName(string name, string packId)
        {
            try
            {
                foreach (Tuple<string, ConfigLocaleHelper> localeHelperTuple in ConfigLocaleHelpers)
                {
                    if (localeHelperTuple.Item1 == packId)
                    {
                        if (!localeHelperTuple.Item2.keyValuePairs.ContainsKey(name))
                            return $"Toggle \"{name}\"";
                        return localeHelperTuple.Item2.keyValuePairs[name];
                    }
                }
            }
            catch { }

            return $"Toggle \"{name}\"";
        }

        private static void InitLocaleHelper(ConfigLocaleHelper localeHelper, Dictionary<string, string> locPaths, string directory, string langCode)
        {
            try
            {
                string locFilePath;
                if (!locPaths.ContainsKey(langCode))
                {
                    locFilePath = Path.Combine(directory, locPaths["EN"]);
                }
                else
                {
                    locFilePath = Path.Combine(directory, locPaths[langCode]);
                }
                using (StreamReader r = new StreamReader(locFilePath))
                {
                    string json = r.ReadToEnd();
                    Dictionary<string, string> localizationDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                    localeHelper.LocaleName = langCode;
                    localeHelper.keyValuePairs = localizationDict;
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cannot init locale, error is {ex}");
            }
        }

        private static string GetLocalizedConfigNameString(string name, string langCode, string directory, Dictionary<string, string> locPaths, ConfigLocaleHelper localeHelper)
        {
            string localizedString = $"clocale:{name}";
            
            try
            {
                if (localeHelper.LocaleName != langCode)
                {
                    InitLocaleHelper(localeHelper, locPaths, directory, langCode);
                }
                localizedString = localeHelper.keyValuePairs[name];

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cannot get locale {name}, error is {ex}");
            }


            return localizedString;
        }

        public static Dictionary<string, string> GetReadyToUseVariables(string id, List<string> variables, Dictionary<string, bool> jparams)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string regexString = @"%(.*?)%=(.*?)$";

            if (variables == null || jparams == null)
            {
                return null;
            }

            foreach (string variable in variables)
            {
                Match match = Regex.Match(variable, regexString);

                if (!match.Success)
                    continue;

                result.Add(match.Groups[1].Value, LScript.LScriptLangHelper.ExecuteScript(match.Groups[2].Value, callItemId:id, jparams:jparams));
            }

            return result;
        }

        public static string ReplaceVariables(string input, IDictionary<string, string> readyToUseVars)
        {
            if (string.IsNullOrEmpty(input) || readyToUseVars == null || readyToUseVars.Count == 0)
            {
                return input;
            }

            var pattern = new Regex("%(?<name>[A-Za-z0-9_]+)%", RegexOptions.Compiled);

            foreach (var variable in readyToUseVars)
            {
                Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"{variable.Key}, {variable.Value}");
            }
            Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Check {input}");

            string evaluator(Match match)
            {
                string value = "";
                Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Check {value}");

                var varName = match.Groups["name"].Value;
                if (readyToUseVars.TryGetValue(varName, out value))
                {
                    Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Check {varName} >>> {value}");
                    return value;
                }

                Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Check {varName} >>> {value}");

                return match.Value;
            }

            return pattern.Replace(input, new MatchEvaluator(evaluator));
        }

        public static string GetStartupParametersByConfigItem(ConfigItem item)
        {
            Dictionary<string, bool> jparams = item.jparams;
            List<string> variables = item.variables;
            string startupString = item.startup_string;
            string packId = item.packId;
            Dictionary<string,string> commaVars = item.commaVars;

            if (jparams == null || variables == null || startupString == null)
                return string.Empty;

            Dictionary<string, string> readyToUseVars = GetReadyToUseVariables(packId, variables, jparams);

            startupString = ReplaceVariables(startupString, readyToUseVars);
            startupString = ReplaceCommaVariables(startupString, commaVars);

            startupString = LScriptLangHelper.ExecuteScriptUnsafe(startupString, callItemId: packId);
            return startupString;
        }
        private static string ReplaceCommaVariables(string startupString, Dictionary<string, string> commaVars)
        {
            Dictionary<string, string> vars = commaVars;

            if (vars == null || vars.Count == 0)
                return startupString;

            string result = ReplaceVariables(startupString, vars);
            return result;
        }

        public string GetStartupParameters(string filename, string packId)
        {
            Dictionary<string, bool> jparams = null;
            List<string> variables = null;
            string startupString = null;
            Dictionary<string, string> commaVars = null;

            foreach (ConfigItem item in Items)
            {
                if (item.packId == packId && item.file_name == filename)
                {
                    jparams = item.jparams;
                    variables = item.variables;
                    startupString = item.startup_string;
                    commaVars = item.commaVars;
                    break;
                }
            }

            if (startupString == null)
                return string.Empty;

            Dictionary<string, string> readyToUseVars = GetReadyToUseVariables(packId, variables, jparams);

            startupString = ReplaceVariables(startupString, readyToUseVars);
            startupString = ReplaceCommaVariables(startupString, commaVars);

            startupString = LScriptLangHelper.ExecuteScriptUnsafe(startupString, callItemId: packId);
            return startupString;
        }

        public static List<VariableItem> GetVariables(ConfigItem configItem)
        {
            List<VariableItem> variables = [];

            foreach (var variable in configItem.jparams)
            {
                VariableItem variableItem = new()
                {
                    name = variable.Key,
                    value = variable.Value,
                };
                variables.Add(variableItem);
            }

            return variables;
        }

        public List<VariableItem> GetVariables(string filename, string packId)
        {
            List<VariableItem> variables = [];

            var configItem = Items.FirstOrDefault(
                x => string.Equals(x.packId, packId, StringComparison.Ordinal) && string.Equals(x.file_name, filename, StringComparison.Ordinal)
                );

            if (configItem == null)
                return variables;

            if (configItem.jparams == null)
                return variables;

            foreach (var variable in configItem.jparams)
            {
                VariableItem variableItem = new() 
                { 
                    name = variable.Key,
                    value = variable.Value,
                };
                variables.Add(variableItem);
            }

            return variables;
        }

        public List<string> GetToggleLists(string filename, string packId)
        {
            var configItem = Items.FirstOrDefault(
                x => string.Equals(x.packId, packId, StringComparison.Ordinal) && string.Equals(x.file_name, filename, StringComparison.Ordinal)
                );

            return configItem != null ? configItem.toggle_lists:[];
        }

        public List<SiteListItem> GetExcludedSiteListItems(string filename, string packId, bool unique = true, bool ignoreNull = false)
        {
            GetAllWindowsInConfig(filename, packId, out var localItemFolder, out var windows);

            var results = new List<SiteListItem>();
            var seenNames = new HashSet<string>();

            foreach (var window in windows)
            {
                string prettyWindow = string.Empty;
                prettyWindow = Regex.Replace(window.Replace(localItemFolder, ""), @"("")(/.*?\\)(.*?"")", m => m.Groups[1].Value + m.Groups[3].Value);

                var tokenPattern = @"(?<=\s|^)(?:(?:(?:--|/)[^\s]*=""[^""]*"")|""[^""]*""|[^ ]+)+";
                var tokens = Regex.Matches(window, tokenPattern)
                                  .Cast<Match>()
                                  .Select(m => m.Value.Trim('"'))
                                  .ToList();



                bool found = false;
                for (int i = 0; i < tokens.Count; i++)
                {
                    string param = tokens[i];
                    string name = null, type = null;

                    if (param.StartsWith("--hostlist-exclude="))
                    {
                        name = param.Substring("--hostlist-exclude=".Length);
                        type = "SiteList";
                    }
                    else if (param.StartsWith("--ipset-exclude="))
                    {
                        name = param.Substring("--ipset-exclude=".Length);
                        type = "IpList";
                    }
                    else if ((param == "--hostlist-exclude" || param == "--ipset-exclude")
                             && i + 1 < tokens.Count)
                    {
                        name = tokens[i + 1];
                        type = param == "--hostlist-exclude"
                                   ? "SiteList"
                                   : "IpList";
                        i++;
                    }

                    if (name != null)
                    {

                        if (unique && seenNames.Contains(name))
                        {
                            var item = results.FirstOrDefault(x => string.Equals(x.Name, Path.GetFileName(name), StringComparison.Ordinal));

                            if (item != null)
                            {
                                item.ApplyParams.Add(window);
                                item.PrettyApplyParams.Add(prettyWindow);
                            }
                            found = true;
                            break;
                        }

                        seenNames.Add(name);

                        var before = string.Join(" ", tokens.Take(i - (param.Contains('=') ? 0 : 1)));
                        var after = string.Join(" ", tokens.Skip(i + 1));

                        results.Add(new SiteListItem
                        {
                            Name = Path.GetFileName(name),
                            Type = type,
                            FilePath = name.Replace("\"", ""),
                            ApplyParams = [window],
                            PrettyApplyParams = [prettyWindow]
                        });

                        found = true;
                        break;
                    }
                }
                if (!found && !ignoreNull)
                {
                    results.Add(new SiteListItem
                    {
                        Name = "",
                        Type = "NULL",
                        FilePath = "",
                        ApplyParams = [window],
                        PrettyApplyParams = [prettyWindow]
                    });
                }
            }
            return results;
        }

        public List<SiteListItem> GetSiteListItems(string filename, string packId, bool unique = true, bool ignoreNull = false)
        {
            GetAllWindowsInConfig(filename, packId, out var localItemFolder, out var windows);

            var results = new List<SiteListItem>();
            var seenNames = new HashSet<string>();

            foreach (var window in windows)
            {
                string prettyWindow = string.Empty;
                prettyWindow = Regex.Replace(window.Replace(localItemFolder, ""), @"("")(/.*?\\)(.*?"")", m => m.Groups[1].Value + m.Groups[3].Value);

                var tokenPattern = @"(?<=\s|^)(?:(?:(?:--|/)[^\s]*=""[^""]*"")|""[^""]*""|[^ ]+)+";
                var tokens = Regex.Matches(window, tokenPattern)
                                  .Cast<Match>()
                                  .Select(m => m.Value.Trim('"'))
                                  .ToList();



                bool found = false;
                for (int i = 0; i < tokens.Count; i++)
                {
                    string param = tokens[i];
                    string name = null, type = null;

                    if (param.StartsWith("--hostlist="))
                    {
                        name = param.Substring("--hostlist=".Length);
                        type = "SiteList";
                    }
                    else if (param.StartsWith("--blacklist="))
                    {
                        name = param.Substring("--blacklist=".Length);
                        type = "SiteList";
                    }
                    else if (param.StartsWith("--ipset="))
                    {
                        name = param.Substring("--ipset=".Length);
                        type = "IpList";
                    }
                    else if (param.StartsWith("--hostlist-auto="))
                    {
                        name = param.Substring("--hostlist-auto=".Length);
                        type = "AutoSiteList";
                    }
                    else if ((param == "--hostlist" || param == "--ipset" || param == "--hostlist-auto" || param == "--blacklist")
                             && i + 1 < tokens.Count)
                    {
                        name = tokens[i + 1];
                        type = param == "--hostlist" || param == "--blacklist"
                                   ? "SiteList"
                                   : param == "--ipset"
                                       ? "IpList"
                                       : "AutoSiteList";
                        i++;
                    }

                    if (name != null)
                    {

                        if (unique && seenNames.Contains(name))
                        {
                            var item = results.FirstOrDefault(x => string.Equals(x.Name, Path.GetFileName(name), StringComparison.Ordinal));

                            if (item != null)
                            {
                                item.ApplyParams.Add(window);
                                item.PrettyApplyParams.Add(prettyWindow);
                            }
                            found = true;
                            break;
                        }

                        seenNames.Add(name);

                        var before = string.Join(" ", tokens.Take(i - (param.Contains('=') ? 0 : 1)));
                        var after = string.Join(" ", tokens.Skip(i + 1));

                        results.Add(new SiteListItem
                        {
                            Name = Path.GetFileName(name),
                            Type = type,
                            FilePath = name.Replace("\"", ""),
                            ApplyParams = [window],
                            PrettyApplyParams = [prettyWindow]
                        });

                        found = true;
                        break;
                    }
                }
                if (!found && !ignoreNull)
                {
                    results.Add(new SiteListItem
                    {
                        Name = "",
                        Type = "NULL",
                        FilePath = "",
                        ApplyParams = [window],
                        PrettyApplyParams = [prettyWindow]
                    });
                }
            }
            return results;
        }

        private void GetAllWindowsInConfig(string filename, string packId, out string localItemFolder, out List<string> windows)
        {
            localItemFolder = GetItemFolderFromPackId(packId);
            string startupString = GetStartupParameters(filename, packId);
            Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"{startupString}");

            windows = startupString
                .Split(new[] { "--new" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => w.Length > 0)
                .ToList();
        }

        public static string GetItemFolderFromPackId(string packId)
        {
            string localAppData = StateHelper.GetDataDirectory();
            string localItemFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName, packId);
            return localItemFolder;
        }

        private static Tuple<ConfigItem, bool> ConvertConfigFromBAT(string filepath)
        {
            // TODO: recursevily call other .BAT files via GOTO check
            string[] lines =  File.ReadAllLines(filepath, Encoding.UTF8);
            Dictionary<string, string> vars = [];
            List<AvailableVarValues> availableVarValues = [];

            bool errorHappens = false;
            bool commaBuilderMode = false;

            string comment = "";
            string comma = "";

            List<string> target = null;
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                if (line.StartsWith("set"))
                {
                    string[] setLine = line[3..].Split("=");
                    if (setLine.Length >= 2)
                    {
                        string key = setLine[0].Replace("\"", "");
                        string value = line[3..].Replace($"{setLine[0]}=", "").Replace("\"", "").Trim();

                        Match match = Regex.Match(setLine[1], VarInVarRegex);
                        if (match.Success)
                        {
                            string varInVar = match.Groups[1].Value;
                            if (vars.ContainsKey(varInVar))
                            {
                                value = value.Replace($"!{varInVar}!", vars[varInVar]);
                            }
                            else
                            {
                                errorHappens = true;
                                Logger.Instance.CreateWarningLog(nameof(ConfigHelper), $"Error happens, cannot find var {varInVar}");
                            }
                        }
                        if (!vars.ContainsKey(key.Trim()))
                            vars.Add(key.Trim(), value);
                        else
                            vars[key.Trim()] = value;

                        AvailableVarValues availableVar = availableVarValues.FirstOrDefault(x => x.VarName == key);
                        if (availableVar != null)
                        {
                            availableVarValues.Remove(availableVar);
                            availableVar.Values.Add(value);
                            availableVar.CurrentValueIndex = availableVar.Values.Count - 1;
                            availableVarValues.Add(availableVar);
                        }

                        Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Found var {key} = {value}");
                    }

                    continue;
                }
                if (line.StartsWith("::") && line.EndsWith("::"))
                {
                    comment = line[2..^2].Trim();
                    continue;
                }
                if (line.StartsWith("rem") && line.Contains("set"))
                {
                    string _l = line[3..];
                    var rx = new Regex(@"\s+set\b", RegexOptions.None);
                    _l = rx.Replace(_l, "", 1);

                    string[] remLine = _l.Split("=");
                    if (remLine.Length >= 2)
                    {
                        remLine[0] = remLine[0].Trim();
                        remLine[1] = remLine[1].Trim();
                        string value = rx.Replace(line[3..], "", 1).Replace($"{remLine[0]}=", "").Trim();

                        bool found = false;
                        foreach (var availableVar in availableVarValues)
                        {
                            if (availableVar.VarName.Trim() == remLine[0])
                            {
                                if (!availableVar.Values.Contains(value))
                                    availableVar.Values.Add(value);
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            List<string> values = [];
                            if (vars.TryGetValue(remLine[0], out string _value))
                                values.Add(_value);

                            values.Add(value);

                            availableVarValues.Add(new()
                            {
                                Comment = comment,
                                VarName = remLine[0],
                                CurrentValueIndex = 0,
                                Values = [value]
                            });
                            comment = "";
                        }
                        if (!vars.ContainsKey(remLine[0]))
                            vars.Add(remLine[0], "$EMPTY");
                        Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Found REM var {remLine[0]} = {value}");
                    }
                    continue;
                }
                if (line.StartsWith("start"))
                {
                    if (line.Contains("winws.exe"))
                    {
                        string ver = DatabaseHelper.Instance.IsItemInstalled(StateHelper.Instance.FindKeyByValue("Zapret")) ?
                            DatabaseHelper.Instance.GetItemById(StateHelper.Instance.FindKeyByValue("Zapret")).CurrentVersion : "%CURRENT%";
                        target = [StateHelper.Instance.FindKeyByValue("Zapret"), ver];
                    }

                    if (line.Contains('^'))
                        commaBuilderMode = true;
                    Match match = Regex.Match(line, BatStartRegex);
                    if (match.Success)
                    {
                        comma = match.Groups[4].Value.Trim().Replace('^', ' ');
                        Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Found start line {comma}");
                    }
                    continue;
                }
                if (commaBuilderMode)
                {
                    comma += line.Replace('^', ' ');

                    Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Comma++ {comma}");

                    if (!line.EndsWith('^'))
                    {
                        commaBuilderMode = false;
                        break;
                    }
                    continue;
                }
            }

            foreach (var _var in vars)
            {
                if (_var.Value.StartsWith("%~dp0"))
                {
                    vars.Remove(_var.Key);
                    comma = comma.Replace($"%{_var.Key}%", _var.Value.Replace("%~dp0", ""));
                }
            }

            ConfigItem configItem = new()
            {
                meta = "pUC:v1.0",
                name = Path.GetFileNameWithoutExtension(filepath),
                target = target,
                commaVars = vars.Count > 0 ? vars : null,
                availableCommaVarsValues = availableVarValues.Count > 0 ? availableVarValues : null,
                jparams = [],
                variables = [],
                startup_string = comma,
            };

            return Tuple.Create(configItem, errorHappens);
        }

        public static Tuple<ConfigItem, bool> LoadConfigFromFile(string filepath)
        {
            if (!File.Exists(filepath))
                return null;


            if (Path.GetExtension(filepath).Equals(".json", StringComparison.CurrentCultureIgnoreCase))
            {
                OldConfigItem oldConfig = Utils.LoadJson<OldConfigItem>(filepath);
                if (oldConfig != null && oldConfig.custom_parameters != null)
                {
                    ConfigItem newConfig = new ConfigItem()
                    {
                        meta = "pUC:v1.0",
                        name = Path.GetFileNameWithoutExtension(filepath),
                        target = null,
                        jparams = [],
                        variables = [],
                        startup_string = oldConfig.custom_parameters,
                    };
                    return Tuple.Create(newConfig, true);
                }
                return Tuple.Create(Utils.LoadJson<ConfigItem>(filepath), true);
            }
            else if (Path.GetExtension(filepath).Equals(".bat", StringComparison.CurrentCultureIgnoreCase) ||
                     Path.GetExtension(filepath).Equals(".cmd", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    var (configItem, errorHappens) = ConvertConfigFromBAT(filepath);

                    return Tuple.Create(configItem, errorHappens);
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateWarningLog(nameof(ConfigHelper), $"Error happens: {ex}");
                    return null;
                }

            }

            return null;
        }

        public static List<string> GetUsedFilesFromConfigItem(ConfigItem configItem)
        {
            List<string> files = [];

            string startupString;
            
            startupString = configItem.startup_string;

            files = GetUsedFilesFromString(startupString, files);

            

            if (configItem.commaVars == null)
                return files;

            foreach (var commaVar in configItem.commaVars)
            {
                files = GetUsedFilesFromString(commaVar.Value, files);
            }

            if (configItem.availableCommaVarsValues == null)
                return files;

            foreach (var availableVars in configItem.availableCommaVarsValues)
            {
                foreach (string valueString in availableVars.Values)
                {
                    files = GetUsedFilesFromString(valueString, files);
                }
            }

            return files;
        }

        private static List<string> GetUsedFilesFromString(string startupString, List<string> files = null)
        {
            if (files == null) files = [];

            string[] flags = startupString.Split(" ");

            for (int i = 0; i < flags.Length; i++)
            {
                string flag = flags[i];
                string flagName = flag.Split("=")[0];
                string value;
                if (flag.Split("=").Length < 2 && i + 1 < flags.Length)
                {
                    if (flags[i + 1].StartsWith("-"))
                        continue;
                    else
                    {
                        value = flags[i + 1];
                        i++;
                    }
                }
                else if (flag.Split("=").Length < 2 && i + 1 >= flags.Length)
                {
                    continue;
                }
                else
                {
                    value = flag.Replace($"{flagName}=", "");
                }
                value = value.Replace("%~dp0", "");
                value = value.Replace("\"", "");
                value = value.Replace("\'", "");

                if (flag.StartsWith("--debug", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (flag.StartsWith("--hostlist", StringComparison.OrdinalIgnoreCase) || flag.StartsWith("--ipset", StringComparison.OrdinalIgnoreCase))
                {
                    if (!value.EndsWith(".bin") && !value.EndsWith(".txt"))
                        continue;

                    if (!files.Contains(value))
                        files.Add(value);
                    continue;
                }

                if (value.EndsWith(".bin") || value.EndsWith(".txt"))
                {
                    if (!files.Contains(value))
                    {
                        files.Add(value);
                        Debug.WriteLine($"Found file {value}");
                    }
                    continue;
                }
            }
            return files;
        }

        public static ConfigItem ReplaceFilesPath(ConfigItem configItem, Dictionary<string, string> files)
        {
            static string ReplaceInString(string input, Dictionary<string, string> files)
            {
                if (string.IsNullOrEmpty(input) || files == null || files.Count == 0)
                    return input;

                input = input.Replace("%~dp0", "").Replace("\'", "\"");

                foreach (var kvp in files)
                {
                    string oldPath = kvp.Key;
                    string newPath = kvp.Value;

                    if (input.Contains(oldPath))
                    {
                        input = input.Replace(oldPath, $"\"{newPath}\"");
                        Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"{input}");
                    }
                }
                input = input.Replace("\"\"", "\"");

                return input;
            }

            if (!string.IsNullOrEmpty(configItem.startup_string))
            {
                string startupString;
                startupString = ReplaceInString(configItem.startup_string, files);

                configItem.startup_string = startupString;
            }

            if (configItem.commaVars != null)
            {
                var keys = configItem.commaVars.Keys.ToList();
                foreach (var key in keys)
                {
                    if (!string.IsNullOrEmpty(configItem.commaVars[key]))
                        configItem.commaVars[key] = ReplaceInString(configItem.commaVars[key], files);
                }
            }

            if (configItem.availableCommaVarsValues != null)
            {
                foreach (var varValue in configItem.availableCommaVarsValues)
                {
                    if (varValue.Values != null)
                    {
                        for (int i = 0; i < varValue.Values.Count; i++)
                        {
                            varValue.Values[i] = ReplaceInString(varValue.Values[i], files);
                        }
                    }
                }
            }

            return configItem;
        }
    }
}
