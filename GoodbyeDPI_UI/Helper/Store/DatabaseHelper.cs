using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                    Name TEXT
                );";
            lock (databaseRequestLock)
            {
                cmd.ExecuteNonQuery();
            }
        }

        public void AddOrUpdateItem(DatabaseStoreItem item)
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO Items (Id, Type, Directory, Executable, UpdateCheckUrl, DownloadUrl, VersionControlType, 
                    CurrentVersion, RequiredItemIds, DependentItemIds, Icon, Name)
                VALUES (@Id, @Type, @Directory, @Executable, @UpdateCheckUrl, @DownloadUrl, @VersionControlType, @CurrentVersion,
                    @RequiredItemIds, @DependentItemIds, @Icon, @Name)";
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.AddWithValue("@Id", item.Id);
            cmd.Parameters.AddWithValue("@Type", item.Type);
            cmd.Parameters.AddWithValue("@Directory", item.Directory);
            cmd.Parameters.AddWithValue("@Executable", item.Executable == null? DBNull.Value : item.Executable);
            cmd.Parameters.AddWithValue("@UpdateCheckUrl", item.UpdateCheckUrl);
            cmd.Parameters.AddWithValue("@DownloadUrl", item.DownloadUrl);
            cmd.Parameters.AddWithValue("@VersionControlType", item.VersionControlType);
            cmd.Parameters.AddWithValue("@CurrentVersion", item.CurrentVersion);
            cmd.Parameters.AddWithValue("@RequiredItemIds",
                (object)Static.Utils.SerializeTuples(item.RequiredItemIds) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DependentItemIds",
                (object)Static.Utils.SerializeTuples(item.DependentItemIds) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Icon", item.IconPath);
            cmd.Parameters.AddWithValue("@Name", item.Name);

            lock (databaseRequestLock)
            {
                cmd.ExecuteNonQuery();
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
                cmd.ExecuteNonQuery();
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
                RequiredItemIds = Static.Utils.DeserializeTuples(reader.GetString(reader.GetOrdinal("RequiredItemIds"))),
                DependentItemIds = Static.Utils.DeserializeTuples(reader.GetString(reader.GetOrdinal("DependentItemIds"))),
                IconPath = reader.GetString(reader.GetOrdinal("Icon")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
            };
        }
    }
}
