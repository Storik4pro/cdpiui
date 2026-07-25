using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.Data;
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

            return ConfigHelper.GetStartupParameters(configFile!, configId!);
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
