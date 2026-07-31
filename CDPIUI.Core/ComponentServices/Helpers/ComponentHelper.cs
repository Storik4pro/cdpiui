using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.Core.ComponentServices.Helpers
{
    public class ComponentHelper
    {
        public readonly string Id;

        private string? ExecutablePath;
        private string? Directory;
        private DatabaseStoreItem? DatabaseStoreItem;

        private readonly ConfigurationService ConfigHelper;

        public Action? ConfigListUpdated;

        public ComponentHelper(string id)
        {
            Id = id;

            DatabaseStoreItem = DatabaseHelper.Instance.GetItemById(Id);

            Directory = DatabaseStoreItem.Directory;
            ExecutablePath = Path.Combine(
                DatabaseStoreItem.Directory!, 
                DatabaseStoreItem.Executable + ".exe");

            if (Id == HardcodedItemIds.ComponentIds[Components.Zapret2])
            {
                try
                {
                    Zapret2LegacyConfigService.EnsureStorageRegistered();
                }
                catch (Exception ex)
                {
                    Basic.Logger.Instance.CreateWarningLog(
                        nameof(ComponentHelper),
                        $"Cannot initialize the converted Zapret config storage: {ex}");
                }
            }

            ConfigHelper = new(id);
            ConfigHelper.Init();
        }

        public void ReInitConfigs()
        {
            ConfigHelper.Init();
            ConfigListUpdated?.Invoke();
        }

        public ConfigurationService GetConfigHelper()
        {
            return ConfigHelper;
        }

        public string? GetExecutablePath()
        {
            if (File.Exists(ExecutablePath))
                return ExecutablePath;

            return TryGetNewPath();
        }

        public string? GetDirectory()
        {
            if (File.Exists(Directory))
                return Directory;

            return TryGetNewDirectory();
        }

        public string GetStartupParams()
        {
            string configId = 
                SettingsManager.Instance.GetValue<string>(["CONFIGS", Id], "configId");
            string configFile = 
                SettingsManager.Instance.GetValue<string>(["CONFIGS", Id], "configFile");

            ConfigItem? config = ConfigHelper.GetConfigItem(configFile!, configId!);
            if (config == null)
            {
                return string.Empty;
            }

            string startupString = ConfigurationService.GetStartupParametersByConfigItem(config);
            if (Id != HardcodedItemIds.ComponentIds[Components.Zapret2] || !config.IsLegacy)
            {
                return startupString;
            }

            bool validateHashes = SettingsManager.Instance.GetValueOrDefault(
                Zapret2LegacyConfigService.HashValidationSettingsGroup,
                Zapret2LegacyConfigService.HashValidationSettingsKey,
                defaultValue: Zapret2LegacyConfigService.DefaultHashValidationValue);

            return Zapret2LegacyConfigService.GetStartupString(
                config,
                startupString,
                validateHashes);
        }

        public void PrepareSelectedConfig(string configFile, string configId)
        {
            if (Id != HardcodedItemIds.ComponentIds[Components.Zapret2])
            {
                return;
            }

            ConfigItem? config = ConfigHelper.GetConfigItem(configFile, configId);
            if (config == null || !config.IsLegacy)
            {
                return;
            }

            string startupString = ConfigurationService.GetStartupParametersByConfigItem(config);
            _ = Zapret2LegacyConfigService.GetStartupString(
                config,
                startupString,
                validateHashes: true,
                forceRebuild: true);
        }

        private string? TryGetNewPath()
        {
            string localItemsFolder = Directories.StoreItemsDirectory;
            ExecutablePath = Path.Combine(
                localItemsFolder, 
                Id, 
                DatabaseStoreItem.Executable + ".exe");

            return File.Exists(ExecutablePath) ? ExecutablePath : null;
        }
        private string? TryGetNewDirectory()
        {
            string localItemsFolder = Directories.StoreItemsDirectory;
            Directory = Path.Combine(localItemsFolder, Id);
            return Path.Exists(Directory) ? Directory : null;
        }
    }
}
