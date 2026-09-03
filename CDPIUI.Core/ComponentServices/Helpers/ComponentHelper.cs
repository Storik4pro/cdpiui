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

        private readonly Lazy<ConfigurationService> ConfigHelper;

        public Action? ConfigListUpdated;

        public ComponentHelper(string id)
        {
            Id = id;

            DatabaseStoreItem = DatabaseHelper.Instance.GetItemById(Id);

            Directory = DatabaseStoreItem.Directory;
            ExecutablePath = Path.Combine(
                DatabaseStoreItem.Directory!, 
                DatabaseStoreItem.Executable + ".exe");

            ConfigHelper = new Lazy<ConfigurationService>(
                CreateConfigHelper,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private ConfigurationService CreateConfigHelper()
        {
            EnsureConvertedZapretStorageRegistered();

            ConfigurationService configHelper = new(Id);
            configHelper.Init();
            return configHelper;
        }

        private void EnsureConvertedZapretStorageRegistered()
        {
            if (Id != HardcodedItemIds.ComponentIds[Components.Zapret2])
                return;

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

        public void ReInitConfigs()
        {
            if (ConfigHelper.IsValueCreated)
                ConfigHelper.Value.Init();
            else
                _ = ConfigHelper.Value;

            ConfigListUpdated?.Invoke();
        }

        public ConfigurationService GetConfigHelper()
        {
            return ConfigHelper.Value;
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

            ConfigItem? config = ConfigHelper.IsValueCreated
                ? ConfigHelper.Value.GetConfigItem(configFile!, configId!)
                : null;

            config ??= ConfigurationService.LoadConfigItemFromPack(
                configFile!,
                configId!,
                Id);
            if (config == null)
            {
                return string.Empty;
            }

            if (config.IsLegacy)
                EnsureConvertedZapretStorageRegistered();

            string startupString = ConfigurationService.GetStartupParametersByConfigItem(config);
            return startupString;
        }

        public void PrepareSelectedConfig(string configFile, string configId)
        {
            if (Id != HardcodedItemIds.ComponentIds[Components.Zapret2])
            {
                return;
            }

            ConfigItem? config = ConfigHelper.Value.GetConfigItem(configFile, configId);
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
