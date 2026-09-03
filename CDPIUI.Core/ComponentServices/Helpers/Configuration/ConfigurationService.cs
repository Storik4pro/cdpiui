using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.LScript;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Shared.Extentions;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.Core.ComponentServices.Configuration
{
    public partial class ConfigurationService
    {
        private const string BatStartRegex = @"start\s+(?:"".*?"")?"".*?""\s+(((?:/min){0,1}\s+"".*?"")|(\S*))?(.*)\^?$";

        private readonly List<ConfigItem> Items = [];

        private string Target;

        
        private List<Tuple<string, LocaleModel>> ConfigLocaleHelpers = [];


        private readonly object _lock = new object();
        public ConfigurationService(string target)
        {
            Target = target;
        }

        private List<ConfigItem> InitConfigDirectory(DatabaseStoreItem item)
        {
            string directory = item.Directory;
            string id = item.Id;

            if (string.IsNullOrEmpty(id)) return [];

            List<ConfigItem> configItems = new List<ConfigItem>();

            string initFile = Path.Combine(directory, "init.json");

            if (!File.Exists(initFile))
                return configItems;

            ConfigInitItem configInitItem = JSONConvertor.LoadJson<ConfigInitItem>(initFile);

            string[] jsonFiles = Directory.GetFiles(directory, "*.json");

            LocaleModel localeModel = new LocaleModel();
            ConfigLocaleHelpers.Add(Tuple.Create(id, localeModel));
            ConfigLocalizationHelper.InitLocaleHelper(
                localeModel, 
                configInitItem.localized_strings_directory!, 
                directory, 
                StoreLocalizationHelper.GetStoreLikeLocale());

            foreach (string jsonFile in jsonFiles)
            {
                if (Path.GetFileName(jsonFile).StartsWith("init"))
                    continue;

                try
                {
                    ConfigItem? configItem = JSONConvertor.LoadJson<ConfigItem>(jsonFile);

                    if (configItem == null ||
                        !MatchesTarget(configItem, Target, out bool isLegacyZapretConfig))
                        continue;

                    configItem.IsLegacy = isLegacyZapretConfig;

                    var result = Regex.Replace(
                        configItem.name!,
                        @"\$.*?\((.*?)\)",
                        match =>
                        {
                            var key = match.Groups[1].Value;
                            if (configInitItem.localized_strings_directory == null)
                                return key;

                            var localized = ConfigLocalizationHelper.GetLocalizedConfigNameString(
                                key, 
                                StoreLocalizationHelper.GetStoreLikeLocale(), 
                                directory, 
                                configInitItem.localized_strings_directory, 
                                localeModel);

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
                    Logger.Instance.CreateWarningLog(nameof(ConfigurationService), $"Error happens: {ex}");
                }
            }
            ConfigItem[] array = [.. configItems];
            Array.Sort(array, new LogicalComparer());

            return [.. array];
        }

        private static bool MatchesTarget(
            ConfigItem? configItem,
            string target,
            out bool isLegacyZapretConfig)
        {
            if (configItem == null)
            {
                isLegacyZapretConfig = false;
                return false;
            }

            string? configTarget = configItem.target?.FirstOrDefault();
            isLegacyZapretConfig =
                target == HardcodedItemIds.ComponentIds[Components.Zapret2] &&
                configTarget == HardcodedItemIds.ComponentIds[Components.Zapret];

            return target == "$ANY" || configTarget == target || isLegacyZapretConfig;
        }

        public static ConfigItem? LoadConfigItemFromPack(
            string filename,
            string packId,
            string target)
        {
            if (string.IsNullOrWhiteSpace(filename) ||
                string.IsNullOrWhiteSpace(packId) ||
                string.IsNullOrWhiteSpace(target) ||
                !string.Equals(filename, Path.GetFileName(filename), StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                DatabaseStoreItem? pack = DatabaseHelper.Instance.GetItemById(packId);
                if (string.IsNullOrWhiteSpace(pack?.Directory))
                    return null;

                string configPath = Path.Combine(pack.Directory, filename);
                if (!File.Exists(configPath))
                    return null;

                ConfigItem? configItem = JSONConvertor.LoadJson<ConfigItem>(configPath);
                if (configItem == null ||
                    !MatchesTarget(configItem, target, out bool isLegacyZapretConfig))
                    return null;

                configItem.file_name = filename;
                configItem.packId = packId;
                configItem.IsLegacy = isLegacyZapretConfig;
                return configItem;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(ConfigurationService),
                    $"Cannot load selected config '{packId}/{filename}': {ex.Message}");
                return null;
            }
        }

        public void Init(string itemId = "")
        {
            lock (_lock)
            {
                Items.Clear();
                ConfigLocaleHelpers.Clear();
                List<DatabaseStoreItem> configItems =
                    string.IsNullOrEmpty(itemId) ? 
                    DatabaseHelper.Instance.GetItemsByType("configlist") : [DatabaseHelper.Instance.GetItemById(itemId)];

                List<DatabaseStoreItem> itemsToCheck = new List<DatabaseStoreItem>();

                foreach (DatabaseStoreItem item in configItems)
                {
                    if (!Path.Exists(item?.Directory))
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
                return [.. Items];
            }
        }

        public ConfigItem? GetConfigItem(string filename, string packId)
        {
            lock (_lock)
            {
                var configItem = Items.FirstOrDefault(
                    x => string.Equals(x.packId, packId, StringComparison.Ordinal) && 
                    string.Equals(x.file_name, filename, StringComparison.Ordinal)
                );
                return configItem;
            }
        }

        public void SetConfigItem(ConfigItem newConfigItem)
        {
            lock (_lock)
            {
                var configItem = Items.FirstOrDefault(
                    x => string.Equals(x.packId, newConfigItem.packId, StringComparison.Ordinal) &&
                    string.Equals(x.file_name, newConfigItem.file_name, StringComparison.Ordinal)
                );
                if (configItem != null)
                    Items.Remove(configItem);

                Items.Add(newConfigItem);
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

        public void ChangeCommaVariableValue(string filename, string packId, string key, string value)
        {
            var configItem = Items.FirstOrDefault(
                x => string.Equals(x.packId, packId, StringComparison.Ordinal) &&
                     string.Equals(x.file_name, filename, StringComparison.Ordinal));

            if (configItem?.commaVars == null)
                return;

            string? actualKey = configItem.commaVars.Keys.FirstOrDefault(
                candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase));
            AvailableVarValues? availableValues = configItem.availableCommaVarsValues?.FirstOrDefault(
                item => string.Equals(item.VarName, key, StringComparison.OrdinalIgnoreCase));
            if (actualKey == null)
                return;

            configItem.commaVars[actualKey] = value;
            if (availableValues != null)
            {
                availableValues.CurrentValueIndex = availableValues.Values?.FindIndex(
                    candidate => string.Equals(candidate, value, StringComparison.Ordinal)) ?? -1;
            }
            _ = SaveConfigItem(filename, packId, configItem);
        }

        public static async Task<string> SaveConfigItem(string filename, string packId, ConfigItem item)
        {
            string folder = GetItemFolderFromPackId(packId);
            string fileName = Path.Combine(folder, filename);

            item.name = item.not_converted_name;

            string jsonString = JSONConvertor.SerializeObject(item);
            Logger.Instance.CreateDebugLog(nameof(ConfigurationService), jsonString);
            try
            {
                File.WriteAllText(fileName, jsonString);
            }
            catch (Exception ex)
            {
                return ErrorsHelper.Convertor.GetPrettyErrorCode("SAVE_CFG", ex);
            }

            await Task.CompletedTask;
            return string.Empty;
        }

        public static string GetDefaultLocalePath(string packId)
        {
            string folder = GetItemFolderFromPackId(packId);

            string initFile = Path.Combine(folder, "init.json");

            if (!File.Exists(initFile))
                return string.Empty;

            ConfigInitItem configInitItem = JSONConvertor.LoadJson<ConfigInitItem>(initFile);
            string _preFilepath = configInitItem.localized_strings_directory?.GetValueOrDefault("EN", null) ?? string.Empty;
            return string.IsNullOrEmpty(_preFilepath) ? string.Empty : Path.Combine(folder, _preFilepath);
        }

        public string GetLocalizedConfigVarName(string name, string packId)
        {
            try
            {
                foreach (Tuple<string, LocaleModel> localeHelperTuple in ConfigLocaleHelpers)
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

        public static Dictionary<string, string>? GetReadyToUseVariables(
            string id, 
            List<string> variables, 
            Dictionary<string, bool> jparams)
        {
            Dictionary<string, string> result = [];
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

                result.Add(
                    match.Groups[1].Value, 
                    LScriptCore.ExecuteScript(match.Groups[2].Value, callItemId: id, jparams: jparams));
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
                Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"{variable.Key}, {variable.Value}");
            }
            Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Check {input}");

            string evaluator(Match match)
            {
                string value = "";
                Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Check {value}");

                var varName = match.Groups["name"].Value;
                if (readyToUseVars.TryGetValue(varName, out value))
                {
                    Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Check {varName} >>> {value}");
                    return value;
                }

                Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Check {varName} >>> {value}");

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
            Dictionary<string, string> commaVars = item.commaVars;

            if (jparams == null || variables == null || startupString == null)
                return string.Empty;

            Dictionary<string, string> readyToUseVars = GetReadyToUseVariables(packId, variables, jparams);

            startupString = ReplaceVariables(startupString, readyToUseVars);
            startupString = ReplaceCommaVariables(startupString, commaVars);

            startupString = LScript.LScriptCore.ExecuteScriptUnsafe(startupString, callItemId: packId);

            if (!item.IsLegacy)
            {
                return startupString;
            }

            bool validateHashes = SettingsManager.Instance.GetValueOrDefault(
                Zapret2LegacyConfigService.HashValidationSettingsGroup,
                Zapret2LegacyConfigService.HashValidationSettingsKey,
                defaultValue: Zapret2LegacyConfigService.DefaultHashValidationValue);

            return Zapret2LegacyConfigService.GetStartupString(
                item,
                startupString,
                validateHashes);
        }
        public static string ReplaceCommaVariables(string startupString, Dictionary<string, string> commaVars)
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

            startupString = LScript.LScriptCore.ExecuteScriptUnsafe(startupString, callItemId: packId);
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
                x => string.Equals(x.packId, packId, StringComparison.Ordinal) && 
                string.Equals(x.file_name, filename, StringComparison.Ordinal)
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

        public List<CommaVariableItem> GetCommaVariables(string filename, string packId)
        {
            var configItem = Items.FirstOrDefault(
                x => string.Equals(x.packId, packId, StringComparison.Ordinal) &&
                     string.Equals(x.file_name, filename, StringComparison.Ordinal));
            if (configItem?.commaVars == null)
                return [];

            var result = new List<CommaVariableItem>();
            foreach (var commaVariable in configItem.commaVars)
            {
                AvailableVarValues? availableValues = configItem.availableCommaVarsValues?.FirstOrDefault(
                    item => string.Equals(item.VarName, commaVariable.Key, StringComparison.OrdinalIgnoreCase));
                List<string> values = (availableValues?.Values ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                result.Add(new CommaVariableItem
                {
                    name = commaVariable.Key,
                    comment = availableValues?.Comment,
                    value = commaVariable.Value,
                    values = values,
                });
            }

            return result;
        }

        public List<string> GetToggleLists(string filename, string packId)
        {
            var configItem = Items.FirstOrDefault(
                x => string.Equals(x.packId, packId, StringComparison.Ordinal) && 
                string.Equals(x.file_name, filename, StringComparison.Ordinal)
                );

            return configItem != null ? configItem.toggle_lists : [];
        }

        public async Task<List<SiteListItem>> GetExcludedSiteListItemsAsync(
            string filename, 
            string packId, 
            bool unique = true, 
            bool ignoreNull = false)
        {
            await Task.CompletedTask;
            return GetExcludedSiteListItems(filename, packId, unique, ignoreNull);
        }

        public List<SiteListItem> GetExcludedSiteListItems(
            string filename, 
            string packId, 
            bool unique = true, 
            bool ignoreNull = false)
        {
            GetAllWindowsInConfig(filename, packId, out var localItemFolder, out var windows);

            var results = new List<SiteListItem>();
            var seenNames = new HashSet<string>();

            foreach (var window in windows)
            {
                string prettyWindow = string.Empty;
                prettyWindow = WindowConverterRegex()
                    .Replace(window.Replace(localItemFolder, ""), m => m.Groups[1].Value + m.Groups[3].Value);

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
                            var item = results.FirstOrDefault(
                                x => string.Equals(x.Name, Path.GetFileName(name), StringComparison.Ordinal));

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

        public async Task<List<SiteListItem>> GetSiteListItemsAsync(
            string filename, 
            string packId, 
            bool unique = true, 
            bool ignoreNull = false)
        {
            await Task.CompletedTask;
            return GetSiteListItems(filename, packId, unique, ignoreNull);
        }

        public List<SiteListItem> GetSiteListItems(
            string filename, 
            string packId, 
            bool unique = true, 
            bool ignoreNull = false)
        {
            GetAllWindowsInConfig(filename, packId, out var localItemFolder, out var windows);

            var results = new List<SiteListItem>();
            var seenNames = new HashSet<string>();

            foreach (var window in windows)
            {
                string prettyWindow = string.Empty;
                prettyWindow = WindowConverterRegex()
                    .Replace(window.Replace(localItemFolder, ""), m => m.Groups[1].Value + m.Groups[3].Value);

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

        private void GetAllWindowsInConfig(
            string filename,
            string packId, 
            out string localItemFolder, 
            out List<string> windows)
        {
            localItemFolder = GetItemFolderFromPackId(packId);
            string startupString = GetStartupParameters(filename, packId);
            Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"{startupString}");

            windows = startupString
                .Split(new[] { "--new" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => w.Length > 0)
                .ToList();
        }

        public static string GetItemFolderFromPackId(string packId)
        {
            string localItemFolder = Path.Combine(Directories.StoreItemsDirectory, packId);
            return localItemFolder;
        }

        private static Tuple<ConfigItem, bool> ConvertConfigFromBAT(string filepath)
        {
            // TODO: recursevily call other .BAT files via GOTO check
            string[] lines = File.ReadAllLines(filepath, Encoding.UTF8);
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

                        Match match = VarInVarRegex().Match(setLine[1]);
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
                                Logger.Instance.CreateWarningLog(
                                    nameof(ConfigurationService), 
                                    $"Error happens, cannot find var {varInVar}");
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

                        Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Found var {key} = {value}");
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
                        Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Found REM var {remLine[0]} = {value}");
                    }
                    continue;
                }
                if (line.StartsWith("start"))
                {
                    if (line.Contains("winws.exe"))
                    {
                        string ver = DatabaseHelper.Instance
                            .IsItemInstalled(HardcodedItemIds.ComponentIds[Components.Zapret]) ?
                            DatabaseHelper.Instance
                            .GetItemById(HardcodedItemIds.ComponentIds[Components.Zapret]).CurrentVersion : 
                            "%CURRENT%";
                        target = [HardcodedItemIds.ComponentIds[Components.Zapret], ver];
                    }

                    if (line.Contains('^'))
                        commaBuilderMode = true;
                    Match match = BatFileStartRegex().Match(line);
                    if (match.Success)
                    {
                        comma = match.Groups[4].Value.Trim().Replace('^', ' ');
                        Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Found start line {comma}");
                    }
                    continue;
                }
                if (commaBuilderMode)
                {
                    comma += line.Replace('^', ' ');

                    Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"Comma++ {comma}");

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

        public static Tuple<ConfigItem, bool>? LoadConfigFromFile(string filepath)
        {
            if (!File.Exists(filepath))
                return null;


            if (Path.GetExtension(filepath).Equals(".json", StringComparison.CurrentCultureIgnoreCase))
            {
                OldConfigItem oldConfig = JSONConvertor.LoadJson<OldConfigItem>(filepath);
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
                return Tuple.Create(JSONConvertor.LoadJson<ConfigItem>(filepath), true);
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
                    Logger.Instance.CreateWarningLog(nameof(ConfigurationService), $"Error happens: {ex}");
                    return null;
                }

            }

            return null;
        }

        public static List<string> GetUsedFilesFromConfigItem(ConfigItem configItem) =>
            configItem.UsedFiles
                .Select(file => file.ExpandedPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

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
                        Logger.Instance.CreateDebugLog(nameof(ConfigurationService), $"{input}");
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

        public string RemoveConfig(string filename, string packId, bool removeFile)
        {
            string folder = GetItemFolderFromPackId(packId);
            string packPath = Path.Combine(folder, filename);

            var item = Items.FirstOrDefault(x => x.packId == packId && x.file_name == filename);
            if (item != null)
            {
                if (removeFile)
                {
                    Items.Remove(item);
                }
                else
                {
                    item.MarkAsRemoved = true;
                }
            }

            if (removeFile)
            {
                try
                {
                    File.Delete(packPath);
                }
                catch (Exception ex)
                {
                    return ErrorsHelper.Convertor.GetPrettyErrorCode("SAVE_CFG", ex);
                }
            }
            return string.Empty;
        }

        public static string GetLastEditTimeFromConfigFile(string filename, string packId)
        {
            string filePath = Path.Combine(GetItemFolderFromPackId(packId), filename);

            try
            {
                DateTime lastEditTime = File.GetLastWriteTime(filePath);
                return lastEditTime.ToString();
            }
            catch
            {
                return "01.12.1970";
            }
        }



        [GeneratedRegex(@"("")(/.*?\\)(.*?"")")]
        private static partial Regex WindowConverterRegex();
        [GeneratedRegex(BatStartRegex)]
        private static partial Regex BatFileStartRegex();
        [GeneratedRegex(@"!(.*?)!")]
        private static partial Regex VarInVarRegex();
    }
}
