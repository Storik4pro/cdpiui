using GoodbyeDPI_UI.Helper.Static;
using Newtonsoft.Json;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Devices.Power;

namespace GoodbyeDPI_UI.Helper.Items
{
    public class ConfigItem
    {
        public string file_name;
        public string packId;
        public string name {  get; set; }
        public string not_converted_name;
        public string meta { get; set; }
        public List<string> target { get; set; }
        public Dictionary<string, bool> jparams { get; set; }
        public List<string> variables { get; set; }
        public string startup_string { get; set; }
        public List<string> toggle_lists;
    }

    public class VariableItem
    {
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
        public List<string> toggleListAvailable;
        public Dictionary<string, string> localized_strings_directory;
    }
    public class ConfigHelper
    {
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
                            var localized = GetLocalizedConfigNameString(key, "RU", directory, configInitItem.localized_strings_directory, localeHelper);
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

            return configItems;
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

            SaveConfigItem(filename, packId, configItem);
        }

        private async void SaveConfigItem(string filename, string packId, ConfigItem item)
        {
            string folder = GetItemFolderFromPackId(packId);
            string fileName = Path.Combine(folder, filename);

            ConfigItem readyToWriteConfigItem = new()
            {
                meta = item.meta,
                name = item.not_converted_name,
                target = item.target,
                jparams = item.jparams,
                variables = item.variables,
                startup_string = item.startup_string,
            };

            string jsonString = System.Text.Json.JsonSerializer.Serialize(readyToWriteConfigItem);
            Logger.Instance.CreateDebugLog(nameof(ConfigHelper), jsonString);
            File.WriteAllText(fileName, jsonString);

            await Task.CompletedTask;
        }

        public string GetLocalizedConfigVarName(string name, string packId)
        {
            foreach (Tuple<string, ConfigLocaleHelper> localeHelperTuple in ConfigLocaleHelpers) 
            { 
                if (localeHelperTuple.Item1 == packId)
                {
                    return localeHelperTuple.Item2.keyValuePairs[name];
                }
            }
            return $"Toggle {name}";
        }

        private static string GetLocalizedConfigNameString(string name, string langCode, string directory, Dictionary<string, string> locPaths, ConfigLocaleHelper localeHelper)
        {
            string localizedString = $"clocale:{name}";
            
            try
            {
                if (localeHelper.LocaleName != langCode)
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
                localizedString = localeHelper.keyValuePairs[name];

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cannot get locale {name}, error is {ex}");
            }


            return localizedString;
        }

        private Dictionary<string, string> GetReadyToUseVariables(string id, List<string> variables, Dictionary<string, bool> jparams)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string regexString = @"%(.*?)%=(.*?)$";

            foreach (string variable in variables)
            {
                Match match = Regex.Match(variable, regexString);

                if (!match.Success)
                    continue;

                result.Add(match.Groups[1].Value, LScript.LScriptLangHelper.ExecuteScript(match.Groups[2].Value, callItemId:id, jparams:jparams));
            }

            return result;
        }

        private static string ReplaceVariables(string input, IDictionary<string, string> readyToUseVars)
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

            string evaluator(Match match)
            {
                string value = "";
                var varName = match.Groups["name"].Value;
                if (readyToUseVars.TryGetValue(varName, out value))
                {
                    return value;
                }

                Logger.Instance.CreateDebugLog(nameof(ConfigHelper), $"Check {varName} >>> {value}");

                return match.Value;
            }

            return pattern.Replace(input, new MatchEvaluator(evaluator));
        }

        public string GetStartupParameters(string filename, string packId)
        {
            Dictionary<string, bool> jparams = null;
            List<string> variables = null;
            string startupString = null;

            foreach (ConfigItem item in Items)
            {
                if (item.packId == packId && item.file_name == filename)
                {
                    jparams = item.jparams;
                    variables = item.variables;
                    startupString = item.startup_string;
                    break;
                }
            }

            if (jparams == null || variables == null || startupString == null)
                return string.Empty;

            Dictionary<string, string> readyToUseVars = GetReadyToUseVariables(packId, variables, jparams);

            return ReplaceVariables(startupString, readyToUseVars);
        }

        public List<VariableItem> GetVariables(string filename, string packId)
        {
            List<VariableItem> variables = [];

            var configItem = Items.FirstOrDefault(
                x => string.Equals(x.packId, packId, StringComparison.Ordinal) && string.Equals(x.file_name, filename, StringComparison.Ordinal)
                );

            if (configItem == null)
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

        public List<SiteListItem> GetSiteListItems(string filename, string packId, bool unique = true)
        {
            string localItemFolder = GetItemFolderFromPackId(packId);

            string startupString = GetStartupParameters(filename, packId);

            var windows = startupString
                .Split(new[] { "--new" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => w.Length > 0)
                .ToList();

            var results = new List<SiteListItem>();
            var seenNames = new HashSet<string>();

            foreach (var window in windows)
            {
                string prettyWindow = string.Empty;
                prettyWindow = window.Replace(localItemFolder, "");

                var tokenPattern = @"(?<=\s|^)(?:""[^""]*""|[^ ]+)+";
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
                        type = "blacklist";
                    }
                    else if (param.StartsWith("--ipset="))
                    {
                        name = param.Substring("--ipset=".Length);
                        type = "iplist";
                    }
                    else if (param.StartsWith("--hostlist-auto="))
                    {
                        name = param.Substring("--hostlist-auto=".Length);
                        type = "autoblacklist";
                    }
                    else if ((param == "--hostlist" || param == "--ipset" || param == "--hostlist-auto")
                             && i + 1 < tokens.Count)
                    {
                        name = tokens[i + 1];
                        type = param == "--hostlist"
                                   ? "blacklist"
                                   : param == "--ipset"
                                       ? "iplist"
                                       : "autoblacklist";
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
                if (!found)
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

        private static string GetItemFolderFromPackId(string packId)
        {
            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string localItemFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName, packId);
            return localItemFolder;
        }
    }
}
