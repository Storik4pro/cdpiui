using GoodbyeDPI_UI.Helper.Items;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Popups;
using static GoodbyeDPI_UI.Helper.StoreHelper;

namespace GoodbyeDPI_UI.Helper
{
    public class DatabaseStoreItem
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Directory { get; set; }
        public string Executable { get; set; }
        public string UpdateCheckUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string VersionControlType { get; set; }
        public string CurrentVersion { get; set; }
        public List<Tuple<string, string>> RequiredItemIds { get; set; }
        public List<Tuple<string, string>> DependentItemIds { get; set; }
        public string IconPath { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
    }
    public class DatabaseHelper
    {
        private readonly object databaseRequestLock = new object();

        private string LocalDatabaseConnectionString;

        private static DatabaseHelper _instance;
        private static readonly object _lock = new object();

        public static DatabaseHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new DatabaseHelper();
                    return _instance;
                }
            }
        }
        DatabaseHelper() 
        {
            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string DatabaseFolderPath = Path.Combine(localAppData, 
                StateHelper.StoreDirName, StateHelper.StoreRepoCache, StateHelper.StoreLocalDirName);

            Directory.CreateDirectory(DatabaseFolderPath);
            string DatabaseFilePath = Path.Combine(DatabaseFolderPath, "storedata.db");

            LocalDatabaseConnectionString = new SqliteConnectionStringBuilder { DataSource = DatabaseFilePath }.ConnectionString;

            InitializeDatabase();
        }

        public void InitializeDatabase()
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Items (
                    Id TEXT PRIMARY KEY,
                    Type TEXT,
                    Directory TEXT,
                    Executable TEXT,
                    UpdateCheckUrl TEXT,
                    DownloadUrl TEXT,
                    VersionControlType TEXT,
                    CurrentVersion TEXT,
                    RequiredItemIds TEXT,
                    DependentItemIds TEXT,
                    Icon TEXT,
                    Name TEXT,
                    ShortName TEXT
                );";
            lock (databaseRequestLock)
            {
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateErrorLog(nameof(DatabaseHelper), $"{ex}");
                }
            }
        }

        public void AddOrUpdateItem(DatabaseStoreItem item)
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO Items (Id, Type, Directory, Executable, UpdateCheckUrl, DownloadUrl, VersionControlType, 
                    CurrentVersion, RequiredItemIds, DependentItemIds, Icon, Name, ShortName)
                VALUES (@Id, @Type, @Directory, @Executable, @UpdateCheckUrl, @DownloadUrl, @VersionControlType, @CurrentVersion,
                    @RequiredItemIds, @DependentItemIds, @Icon, @Name, @ShortName)";
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.AddWithValue("@Id", item.Id);
            cmd.Parameters.AddWithValue("@Type", item.Type);
            cmd.Parameters.AddWithValue("@Directory", item.Directory);
            cmd.Parameters.AddWithValue("@Executable", item.Executable == null? DBNull.Value : item.Executable);
            cmd.Parameters.AddWithValue("@UpdateCheckUrl", item.UpdateCheckUrl == null ? DBNull.Value : item.UpdateCheckUrl);
            cmd.Parameters.AddWithValue("@DownloadUrl", item.DownloadUrl == null ? DBNull.Value : item.DownloadUrl);
            cmd.Parameters.AddWithValue("@VersionControlType", item.VersionControlType);
            cmd.Parameters.AddWithValue("@CurrentVersion", item.CurrentVersion);
            cmd.Parameters.AddWithValue("@RequiredItemIds",
                (object)Static.Utils.SerializeTuples(item.RequiredItemIds) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DependentItemIds",
                (object)Static.Utils.SerializeTuples(item.DependentItemIds) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Icon", item.IconPath);
            cmd.Parameters.AddWithValue("@Name", item.Name);
            cmd.Parameters.AddWithValue("@ShortName", item.ShortName);

            lock (databaseRequestLock)
            {
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex) 
                {
                    Logger.Instance.CreateErrorLog(nameof(DatabaseHelper), $"{ex}");
                }
            }
        }

        public DatabaseStoreItem GetItemById(string id)
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Items WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            DatabaseStoreItem item = CreateItemFromReader(reader);

            return item;
        }

        public List<DatabaseStoreItem> GetItemsByType(string type)
        {
            var items = new List<DatabaseStoreItem>();

            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Items WHERE Type = @Type";
            cmd.Parameters.AddWithValue("@Type", type);

            lock (databaseRequestLock)
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    DatabaseStoreItem item = CreateItemFromReader(reader);
                    items.Add(item);
                }
            }

            return items;
        }

        public void DeleteItemById(string id)
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Items WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);

            lock (databaseRequestLock)
            {
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateErrorLog(nameof(DatabaseHelper), $"{ex}");
                }
            }
        }

        public bool IsItemInstalled(string id)
        {
            if (GetItemById(id) == null) return false; return true;
        }

        private static DatabaseStoreItem CreateItemFromReader(SqliteDataReader reader)
        {
            return new DatabaseStoreItem
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Type = reader.GetString(reader.GetOrdinal("Type")),
                Directory = reader.GetString(reader.GetOrdinal("Directory")),
                Executable = reader.IsDBNull(reader.GetOrdinal("Executable"))
                                ? null : reader.GetString(reader.GetOrdinal("Executable")),
                UpdateCheckUrl = reader.IsDBNull(reader.GetOrdinal("UpdateCheckUrl"))
                                ? null : reader.GetString(reader.GetOrdinal("UpdateCheckUrl")),
                DownloadUrl = reader.IsDBNull(reader.GetOrdinal("DownloadUrl"))
                                ? null : reader.GetString(reader.GetOrdinal("DownloadUrl")),
                VersionControlType = reader.GetString(reader.GetOrdinal("VersionControlType")),
                CurrentVersion = reader.GetString(reader.GetOrdinal("CurrentVersion")),
                RequiredItemIds = reader.IsDBNull(reader.GetOrdinal("RequiredItemIds"))
                                ? null : Static.Utils.DeserializeTuples(reader.GetString(reader.GetOrdinal("RequiredItemIds"))),
                DependentItemIds = reader.IsDBNull(reader.GetOrdinal("DependentItemIds"))
                                ? null : Static.Utils.DeserializeTuples(reader.GetString(reader.GetOrdinal("DependentItemIds"))),
                IconPath = reader.GetString(reader.GetOrdinal("Icon")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                ShortName = reader.IsDBNull(reader.GetOrdinal("ShortName"))
                                ? null : reader.GetString(reader.GetOrdinal("ShortName")),
            };
        }

        public void RegisterUserCustomItem(bool manual=false)
        {
            if (GetItemById(StateHelper.LocalUserItemsId) != null && !manual)
            {
                return;
            }

            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string targetFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName, StateHelper.LocalUserItemsId);

            try
            {
                Directory.CreateDirectory(targetFolder);
                Directory.CreateDirectory(Path.Combine(targetFolder, StateHelper.LocalUserItemSiteListsFolder));
                Directory.CreateDirectory(Path.Combine(targetFolder, StateHelper.LocalUserItemBinsFolder));
                Directory.CreateDirectory(Path.Combine(targetFolder, StateHelper.LocalUserItemLocFolder));
            }
            catch (Exception ex) 
            {
                Logger.Instance.RaiseCriticalException(nameof(RegisterUserCustomItem), ex);
                return;
            }

            Dictionary<string, string> localLoc = new();
            localLoc.Add("EN", $"{StateHelper.LocalUserItemLocFolder}/strings.json");

            ConfigInitItem configInitItem = new()
            {
                toggleListAvailable = [],
                localized_strings_directory = localLoc,
            };

            string jsonString = System.Text.Json.JsonSerializer.Serialize(configInitItem);
            Logger.Instance.CreateDebugLog(nameof(ConfigHelper), jsonString);
            File.WriteAllText(Path.Combine(targetFolder, "init.json"), jsonString);

            DatabaseStoreItem userItem = new()
            {
                Id = StateHelper.LocalUserItemsId,
                Type = "configlist",
                Directory = targetFolder,
                Executable = null,
                UpdateCheckUrl = null,
                DownloadUrl = null,
                VersionControlType = "local",
                CurrentVersion = StateHelper.Instance.Version,
                RequiredItemIds = null,
                DependentItemIds = null,
                IconPath = "$STATICIMAGE(Store/empty.png)",
                Name = "Storage for custom items of current user",
                ShortName = "Local data storage"
            };

            AddOrUpdateItem(userItem);
        }

        public void QuickRestore()
        {
            RegisterUserCustomItem();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Items";
            using var reader = cmd.ExecuteReader();

            var itemsToUpdate = new List<DatabaseStoreItem>();
            while (reader.Read())
            {
                var item = CreateItemFromReader(reader);

                if (!item.Directory.StartsWith(baseDir) || !Directory.Exists(item.Directory))
                {
                    string folderName = Path.GetFileName(item.Directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    string newPath = Path.Combine(baseDir, StateHelper.StoreDirName, StateHelper.StoreItemsDirName, folderName);

                    if (Directory.Exists(newPath))
                    {
                        item.Directory = newPath;
                        itemsToUpdate.Add(item);
                    }
                    else
                    {
                        QuickRestoreFailure(item);
                    }
                }
            }

            foreach (var item in itemsToUpdate)
            {
                AddOrUpdateItem(item);
            }
        }

        private void QuickRestoreFailure(DatabaseStoreItem item)
        {
            if (item.Id == StateHelper.LocalUserItemsId)
            {
                RegisterUserCustomItem(manual: true);
                return;
            }
        }
    }
}
