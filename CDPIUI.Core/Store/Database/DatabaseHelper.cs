using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Shared.Extentions;
using CDPIUI.Shared;
using Microsoft.Data.Sqlite;
using System.Data;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.Exceptions.Database;

namespace CDPIUI.Core.Store.Database
{
    public class DatabaseHelper
    {
        private readonly object databaseRequestLock = new();

        private readonly string LocalDatabaseConnectionString;

        private static DatabaseHelper? _instance;
        private static readonly object _lock = new();

        public static DatabaseHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new DatabaseHelper();
                    return _instance;
                }
            }
        }
        DatabaseHelper() 
        {
            Directory.CreateDirectory(Directories.StoreLocalCacheDirectory);
            string DatabaseFilePath = Path.Combine(Directories.StoreLocalCacheDirectory, SharedConstants.DatabaseFileName);

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
                    DownloadFileType TEXT,
                    VersionControlType TEXT,
                    CurrentVersion TEXT,
                    RequiredItemIds TEXT,
                    DependentItemIds TEXT,
                    Icon TEXT,
                    Name TEXT,
                    ShortName TEXT,
                    Developer TEXT,
                    BackgroundColor TEXT
                );";
            lock (databaseRequestLock)
            {
                try
                {
                    cmd.ExecuteNonQuery();
                    var prCmd = connection.CreateCommand();
                    prCmd.CommandText = @"PRAGMA journal_mode=WAL";
                    prCmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateErrorLog(nameof(DatabaseHelper), $"{ex}");
                }
            }
        }

        public bool AddOrUpdateItem(DatabaseStoreItem item)
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO Items (Id, Type, Directory, Executable, UpdateCheckUrl, DownloadUrl, DownloadFileType, VersionControlType, 
                    CurrentVersion, RequiredItemIds, DependentItemIds, Icon, Name, ShortName, Developer, BackgroundColor)
                VALUES (@Id, @Type, @Directory, @Executable, @UpdateCheckUrl, @DownloadUrl, @DownloadFileType, @VersionControlType, @CurrentVersion,
                    @RequiredItemIds, @DependentItemIds, @Icon, @Name, @ShortName, @Developer, @BackgroundColor)";
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.AddWithValue("@Id", item.Id);
            cmd.Parameters.AddWithValue("@Type", item.Type);
            cmd.Parameters.AddWithValue("@Directory", item.Directory);
            cmd.Parameters.AddWithValue("@Executable", item.Executable == null? DBNull.Value : item.Executable);
            cmd.Parameters.AddWithValue("@UpdateCheckUrl", item.UpdateCheckUrl == null ? DBNull.Value : item.UpdateCheckUrl);
            cmd.Parameters.AddWithValue("@DownloadUrl", item.DownloadUrl == null ? DBNull.Value : item.DownloadUrl);
            cmd.Parameters.AddWithValue("@DownloadFileType", item.DownloadFileType == null ? DBNull.Value : item.DownloadFileType);
            cmd.Parameters.AddWithValue("@VersionControlType", item.VersionControlType);
            cmd.Parameters.AddWithValue("@CurrentVersion", item.CurrentVersion);
            cmd.Parameters.AddWithValue("@RequiredItemIds",
                (object)item.RequiredItemIds?.SerializeTuples() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DependentItemIds",
                (object)item.DependentItemIds?.SerializeTuples() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Icon", item.IconPath);
            cmd.Parameters.AddWithValue("@Name", item.Name);
            cmd.Parameters.AddWithValue("@ShortName", item.ShortName);
            cmd.Parameters.AddWithValue("@Developer", item.Developer);
            cmd.Parameters.AddWithValue("@BackgroundColor", item.BackgroudColor);

            lock (databaseRequestLock)
            {
                try
                {
                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch (Exception ex) 
                {
                    Logger.Instance.CreateErrorLog(nameof(DatabaseHelper), $"{ex}");
                    return false;
                }
            }
        }

        public DatabaseStoreItem? GetItemById(string id)
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Items WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);

            lock (databaseRequestLock)
            {
                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;
                DatabaseStoreItem item = CreateItemFromReader(reader);

                return item;
            }
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

        public List<DatabaseStoreItem> GetAllInstalledItems()
        {
            var items = new List<DatabaseStoreItem>();

            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Items";

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

        internal void DeleteItemById(string id)
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
                DownloadFileType = reader.IsDBNull(reader.GetOrdinal("DownloadFileType"))
                                ? null : reader.GetString(reader.GetOrdinal("DownloadFileType")),
                VersionControlType = reader.GetString(reader.GetOrdinal("VersionControlType")),
                CurrentVersion = reader.GetString(reader.GetOrdinal("CurrentVersion")),
                RequiredItemIds = reader.IsDBNull(reader.GetOrdinal("RequiredItemIds"))
                                ? null : reader.GetString(reader.GetOrdinal("RequiredItemIds")).DeserializeTuples(),
                DependentItemIds = reader.IsDBNull(reader.GetOrdinal("DependentItemIds"))
                                ? null : reader.GetString(reader.GetOrdinal("DependentItemIds")).DeserializeTuples(),
                IconPath = reader.GetString(reader.GetOrdinal("Icon")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                ShortName = reader.IsDBNull(reader.GetOrdinal("ShortName"))
                                ? null : reader.GetString(reader.GetOrdinal("ShortName")),
                Developer = reader.IsDBNull(reader.GetOrdinal("Developer"))
                                ? string.Empty : reader.GetString(reader.GetOrdinal("Developer")),
                BackgroudColor = reader.IsDBNull(reader.GetOrdinal("BackgroundColor"))
                                ? string.Empty : reader.GetString(reader.GetOrdinal("BackgroundColor")),
            };
        }

        public UnprocessedOperationResultModel<EmptyResult> RegisterUserCustomItem(
            string applicationVersion,
            object defaultConfigItem,
            bool manual=false)
        {
            if (GetItemById(SharedConstants.LocalUserItemsId) != null && GetItemById(SharedConstants.ApplicationStoreId) != null && !manual)
            {
                return UnprocessedOperationResultModel<EmptyResult>.SuccessResult();
            }

            string localAppData = Directories.DataDirectory;
            string targetFolder = Directories.StoreLocalUserItemDirectory;

            try
            {
                Directory.CreateDirectory(targetFolder);
                Directory.CreateDirectory(Path.Combine(targetFolder, SharedConstants.LocalUserItemSiteListsFolder));
                Directory.CreateDirectory(Path.Combine(targetFolder, SharedConstants.LocalUserItemBinsFolder));
                Directory.CreateDirectory(Path.Combine(targetFolder, SharedConstants.LocalUserItemLocFolder));
            }
            catch (Exception ex) 
            {
                return UnprocessedOperationResultModel<EmptyResult>.FailureResult(ex);
            }

            string jsonString = JSON.JSONConvertor.SerializeObject(defaultConfigItem);
            Logger.Instance.CreateDebugLog(nameof(DatabaseHelper), jsonString);
            File.WriteAllText(Path.Combine(targetFolder, "init.json"), jsonString);

            DatabaseStoreItem userItem = new()
            {
                Id = SharedConstants.LocalUserItemsId,
                Type = "configlist",
                Directory = targetFolder,
                Executable = null,
                UpdateCheckUrl = null,
                DownloadUrl = null,
                DownloadFileType = null,
                VersionControlType = "local",
                CurrentVersion = applicationVersion,
                RequiredItemIds = null,
                DependentItemIds = null,
                IconPath = "$STATICIMAGE(Store/empty.png)",
                Name = "Storage for custom items of current user",
                ShortName = "Local data storage",
                Developer = "Storik4",
                BackgroudColor = ""
            };

            if (!AddOrUpdateItem(userItem))
            {
                return UnprocessedOperationResultModel<EmptyResult>
                    .FailureResult(new DatabaseUserItemRegistrationException());
            }

            DatabaseStoreItem applicationItem = new()
            {
                Id = SharedConstants.ApplicationStoreId,
                Type = "CDPIUIUpdateItem",
                Directory = Directories.DataDirectory,
                Executable = null,
                UpdateCheckUrl = null,
                DownloadUrl = null,
                DownloadFileType = null,
                VersionControlType = "local",
                CurrentVersion = applicationVersion,
                RequiredItemIds = null,
                DependentItemIds = null,
                IconPath = "$STATICIMAGE(Store/empty.png)",
                Name = "CDPI UI updates helper item",
                ShortName = "CDPI UI updates helper item",
                Developer = "Storik4",
                BackgroudColor = ""
            };

            if (!AddOrUpdateItem(applicationItem))
            {
                return UnprocessedOperationResultModel<EmptyResult>
                    .FailureResult(new ApplicationItemRegistrationException());
            }

            return UnprocessedOperationResultModel<EmptyResult>.SuccessResult();
        }

        public OperationResultModel<List<DatabaseStoreItem>> RestorePaths()
        {
            string baseDir = Directories.DataDirectory;

            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Items";
            using var reader = cmd.ExecuteReader();

            var itemsToUpdate = new List<DatabaseStoreItem>();

            List<DatabaseStoreItem> RestoreFailureItems = [];

            while (reader.Read())
            {
                var item = CreateItemFromReader(reader);

                if (!item.Directory.StartsWith(baseDir) || !Directory.Exists(item.Directory))
                {
                    string folderName = Path.GetFileName(item.Directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    string newPath = Path.Combine(Directories.StoreItemsDirectory, folderName);

                    if (Directory.Exists(newPath))
                    {
                        item.Directory = newPath;
                        itemsToUpdate.Add(item);
                    }
                    else
                    {
                        RestoreFailureItems.Add(item);
                    }
                }
            }


            foreach (var item in itemsToUpdate)
            {
                AddOrUpdateItem(item);
            }
            if (RestoreFailureItems.Count > 0)
                return new()
                {
                    Success = false,
                    Result = RestoreFailureItems,

                    ErrorHappens = false
                };
            else
                return OperationResultModel<List<DatabaseStoreItem>>.SuccessResult();
        }
    }
}
