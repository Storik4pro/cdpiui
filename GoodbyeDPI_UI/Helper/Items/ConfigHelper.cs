using GoodbyeDPI_UI.Helper.Static;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Power;

namespace GoodbyeDPI_UI.Helper.Items
{
    public class ConfigItem
    {
        public string file_name;
        public string packId;
        public string name;
        public string meta;
        public List<string> target;
        public Dictionary<string, bool> jparams;
        public List<string> variables;
        public string startup_string;
        public List<string> toggle_lists;
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

        private string GetLocalizedConfigNameString(string name, string langCode, string directory, Dictionary<string, string> locPaths, ConfigLocaleHelper localeHelper)
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
    }
}
