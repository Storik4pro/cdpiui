using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers
{
    public static class Autocorrector
    {
        public static string FindAutoCorrectPath(string filePath, ConfigItem configItem, string configPath)
        {
            try
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(filePath), "autohostlist", StringComparison.OrdinalIgnoreCase))
                {
                    return filePath;
                }
                if (configItem != null && configItem.packId != null && configItem.jparams != null)
                {
                    string _filePath = LScript.LScriptCore.ExecuteScriptUnsafe(
                        ConfigurationService.ReplaceVariables(
                            filePath,
                            ConfigurationService.GetReadyToUseVariables(
                                configItem.packId, 
                                configItem.variables!, 
                                configItem.jparams)!),
                        callItemId: configItem.packId);

                    _filePath = LScript.LScriptCore.ExecuteScriptUnsafe(
                        ConfigurationService.ReplaceCommaVariables(
                            _filePath,
                            configItem.commaVars!),
                        callItemId: configItem.packId);

                    if (Path.Exists(_filePath))
                        return _filePath;
                }
                if (File.Exists(filePath))
                {
                    return filePath;
                }

                if (File.Exists(Path.Combine(Path.GetDirectoryName(configPath)!, filePath)))
                {
                    return Path.Combine(Path.GetDirectoryName(configPath)!, filePath);
                }

                var items = DatabaseHelper.Instance.GetItemsByType("configlist");
                foreach (var item in items)
                {
                    string itemPath = item.Directory;
                    var files = Directory.EnumerateFiles(
                        itemPath!, 
                        $"*{Path.GetExtension(filePath)}", 
                        SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        if (Path.GetFileName(filePath) == Path.GetFileName(file))
                            return file;
                    }
                }
                var components = DatabaseHelper.Instance.GetItemsByType("component");
                foreach (var item in components)
                {
                    string itemPath = item.Directory;
                    var files = Directory.EnumerateFiles(
                        itemPath!, 
                        $"*{Path.GetExtension(filePath)}", 
                        SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        if (Path.GetFileName(filePath) == Path.GetFileName(file))
                            return file;
                    }
                }
                var addOns = DatabaseHelper.Instance.GetItemsByType("addon");
                foreach (var item in addOns)
                {
                    string itemPath = item.Directory;
                    var files = Directory.EnumerateFiles(
                        itemPath!, 
                        $"*{Path.GetExtension(filePath)}", 
                        SearchOption.AllDirectories);
                    foreach (var file in files)
                    {

                        if (Path.GetFileName(filePath) == Path.GetFileName(file))
                            return file;
                    }
                }
            }
            catch
            {
                // pass
            }
            return string.Empty;
        }
    }
}
