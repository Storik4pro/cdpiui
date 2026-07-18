using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper.Items
{
    public class ComponentHelper
    {
        public readonly string Id;

        private string ExecutablePath;
        private string Directory;
        private DatabaseStoreItem DatabaseStoreItem;

        private readonly ConfigHelper ConfigHelper;

        public Action ConfigListUpdated;
        
        public ComponentHelper(string id) 
        {
            Id = id;

            DatabaseStoreItem = DatabaseHelper.Instance.GetItemById(Id);

            Directory = DatabaseStoreItem.Directory;
            ExecutablePath = Path.Combine(DatabaseStoreItem.Directory, DatabaseStoreItem.Executable + ".exe");

            ConfigHelper = new(id);
            ConfigHelper.Init();
        }

        public void ReInitConfigs()
        {
            ConfigHelper.Init();
            ConfigListUpdated?.Invoke();
        }

        public ConfigHelper GetConfigHelper()
        {
            return ConfigHelper;
        }

        public string GetExecutablePath()
        {
            if (File.Exists(ExecutablePath))
                return ExecutablePath;

            return TryGetNewPath();
        }

        public string GetDirectory()
        {
            if (File.Exists(Directory))
                return Directory;

            return TryGetNewDirectory();
        }

        public string GetStartupParams()
        {
            string configId = SettingsManager.Instance.GetValue<string>(["CONFIGS", Id], "configId");
            string configFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", Id], "configFile");

            return ConfigHelper.GetStartupParameters(configFile, configId);
        }

        private string TryGetNewPath()
        {
            string localAppData = StateHelper.GetDataDirectory();
            string localItemsFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName);
            ExecutablePath = Path.Combine(localItemsFolder, Id, DatabaseStoreItem.Executable + ".exe");
            return File.Exists(ExecutablePath) ? ExecutablePath : null;
        }
        private string TryGetNewDirectory()
        {
            string localAppData = StateHelper.GetDataDirectory();
            string localItemsFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName);
            Directory = Path.Combine(localItemsFolder, Id);
            return Path.Exists(Directory) ? Directory : null;
        }
    }
}
