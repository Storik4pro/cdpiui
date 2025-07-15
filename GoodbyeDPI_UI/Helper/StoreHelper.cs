using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Services.Maps.LocalSearch;
using static GoodbyeDPI_UI.Helper.ErrorsHelper;
using TimeSpan = System.TimeSpan;

namespace GoodbyeDPI_UI.Helper
{
    public class UICategoryData
    {
        public string StoreId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }

        public ObservableCollection<UIElement> Items { get; set; }
    }
    public class StoreHelper
    {
        private const string StoreRepo = "Storik4pro/CDPIUI-Store";
        private const string StoreDirName = "Store";
        private const string StoreItemsDirName = "Items";
        private const string StoreRepoDirName = "Repo";
        private const string StoreRepoCache = "Cache";
        private const string StoreLocalDirName = "Local";
        private const string GitHubApiToken = "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";

        private string LocalDatabaseConnectionString;

        private List<string> SupportedCategoryTypes = ["basic_category", "second_category"];

        private const string ScriptGetArgsRegex = @"\$.*?\((.*?)\)";

        private Dictionary<string, string> StoreLocalizationPaths;
        public List<RepoCategory> FormattedStoreDatabase;

        private DownloadManager DownloadManager { get; set; }

        private class RepoInit
        {
            public Dictionary<string, string> localized_strings_directory;
            public List<string> categories;
            public Dictionary<string, string> categories_directory;
        }

        private class RepoCategoryInit
        {
            public string store_id;
            public string type;
            public List<string> items;
            public Dictionary<string, string> items_directories;
        }

        public class RepoCategory
        {
            public string store_id;
            public string type;
            public string name;
            public List<RepoCategoryItem> items;
        }

        public class Link
        {
            public string name;
            public string url;
        }

        public class RepoCategoryItem
        {
            public string store_id;
            public string category_id;
            public string type;
            public string name;
            public string developer;
            public string icon;
            public string background;
            public string stars;
            public string small_description;
            public string description;
            public bool display_warning;
            public string warning_text;
            public List<Link> links;
            public string version_control;
            public string version_control_link;
            public string download_link;
            public string filetype;
            public string archive_root_folder;
            public string target_executable_file;
            public string after_install_actions;
            public string target_minversion;
            public string target_maxversion;
        }

        public class DatabaseStoreItem
        {
            public string Id { get; set; }
            public string Directory { get; set; }
            public string UpdateCheckUrl { get; set; }
            public string DownloadUrl { get; set; }
            public string VersionControlType { get; set; }
            public string CurrentVersion { get; set; }
            public List<string> RequiredItemIds { get; set; }
            public List<string> DependentItemIds { get; set; }
            public string IconPath { get; set; }
            public string Name { get; set; }
        }

        // Download queue

        private class QueueItem
        {
            public string OperationId { get; }
            public string ItemId { get; }
            public string Version { get; }

            public QueueItem(string itemId, string operationId, string version=null)
            {
                ItemId = itemId;
                Version = version;
                OperationId = operationId;
            }
        }
        private readonly Queue<QueueItem> _queue = new();
        private readonly object _queueLock = new();

        private class StoreLocaleHelper
        {
            public string LocaleName;
            public Dictionary<string, string> keyValuePairs;
        }

        private List<RepoCategoryItem> ItemsList = new();
        private StoreLocaleHelper localeHelper = new();

        private static StoreHelper _instance;
        private static readonly object _lock = new object();

        public static StoreHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new StoreHelper();
                    return _instance;
                }
            }
        }

        public event Action<string> StoreInternalErrorHappens;
        public event Action UpdatingDatabaseStarted;
        public event Action<Tuple<string, string, List<string>>> SelectFileNeeded;
        public event Action<string> NowProcessItemActions;
        public event Action<string> ItemActionsStopped;

        public event Action<Tuple<string, string>> ItemInstallingErrorHappens;
        public event Action<Tuple<string, double>> ItemDownloadSpeedChanged;
        public event Action<Tuple<string, double>> ItemDownloadProgressChanged;
        public event Action<Tuple<string, TimeSpan>> ItemTimeRemainingChanged;
        public event Action<Tuple<string, string>> ItemDownloadStageChanged;

        private StoreHelper()
        {
            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string DatabaseFolderPath = Path.Combine(localAppData, StoreDirName, StoreRepoCache, StoreLocalDirName);
            Directory.CreateDirectory(DatabaseFolderPath);

            string DatabaseFilePath = Path.Combine(DatabaseFolderPath, "storedata.db");
            LocalDatabaseConnectionString = $"Data Source={DatabaseFilePath}";

            InitializeDatabase();
        }

        public async Task<bool> LoadAllStoreDatabase()
        {
            try
            {
                if (FormattedStoreDatabase != null)
                {
                    return true;
                }

                string localAppData = AppDomain.CurrentDomain.BaseDirectory;
                string targetFolder = Path.Combine(localAppData, StoreDirName, StoreRepoCache, StoreRepoDirName);

                string zipUrl = $"https://api.github.com/repos/{StoreRepo}/zipball/main";

                using HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CDPIStore", "0.0.0.0"));
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", GitHubApiToken);

                using HttpResponseMessage response = await client.GetAsync(zipUrl);
                response.EnsureSuccessStatusCode();

                string tempZipPath = Path.Combine(Path.GetTempPath(), "store_repo.tmp");
                await using (var fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }

                if (Directory.Exists(targetFolder))
                    Directory.Delete(targetFolder, recursive: true);
                Directory.CreateDirectory(targetFolder);

                using (var archive = ZipFile.OpenRead(tempZipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                            continue;

                        var parts = entry.FullName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length <= 1)
                            continue;

                        string[] subParts = parts.Skip(1).ToArray();
                        string relativePath = Path.Combine(subParts);

                        string destinationPath = Path.Combine(targetFolder, relativePath);

                        string destDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);

                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }
                }

                File.Delete(tempZipPath);

                FormattedStoreDatabase = GetFormattedStoreDatabase();

                return true;
            } 
            catch (Exception ex) 
            {
                StoreInternalErrorHappens?.Invoke($"Error loading store database: {ex.Message}");
                Debug.WriteLine($"Error loading store database: {ex.Message}");
            }
            return false;
        }

        private static T LoadJson<T>(string filepath)
        {
            string json = File.ReadAllText(filepath);
            return JsonConvert.DeserializeObject<T>(json);
        }

        private List<RepoCategory> GetFormattedStoreDatabase()
        {
            ItemsList?.Clear();
            List<RepoCategory> categories = new List<RepoCategory>();

            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string localRepoFolder = Path.Combine(localAppData, StoreDirName, StoreRepoCache, StoreRepoDirName);

            string localRepoInitFile = Path.Combine(localRepoFolder, "init.json");

            if (!File.Exists(localRepoInitFile))
                return categories;

            try
            {
                RepoInit repoInitData = LoadJson<RepoInit>(localRepoInitFile);

                StoreLocalizationPaths = repoInitData.localized_strings_directory;

                List<string> categoriesAvailable = repoInitData.categories;
                Dictionary<string, string> categoriesPaths = repoInitData.categories_directory;

                List<List<string>> pathsToCheck = new List<List<string>>();

                foreach (string category in categoriesAvailable)
                {
                    Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Checking the category: {category}");
                    if (!categoriesPaths.ContainsKey(category))
                    {
                        Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Skip the category: {category}");
                        continue;
                    }

                    pathsToCheck.Add([categoriesPaths[category], category]);
                }

                foreach (List<string> _cat in pathsToCheck)
                {
                    string categoryPath = _cat[0];
                    string categoryName = _cat[1];

                    string categoryInitPath = Path.Combine(localRepoFolder, categoryPath, "init.json");

                    if (!Path.Exists(categoryInitPath))
                    {
                        Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Skip the category: {categoryName}, {categoryPath}");
                        continue;
                    }

                    RepoCategoryInit repoCategoryInit = LoadJson<RepoCategoryInit>(categoryInitPath);

                    if (!SupportedCategoryTypes.Contains(repoCategoryInit.type))
                    {
                        Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Skip the category: {categoryName}, {categoryPath}");
                        continue;
                    }

                    List<string> items = repoCategoryInit.items;
                    Dictionary<string, string> categoryItemsPaths = repoCategoryInit.items_directories;

                    List<string> categoryItemsPathsToCheck = new List<string>();

                    foreach (string item in items)
                    {
                        if (!categoryItemsPaths.ContainsKey(item))
                        {
                            Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Skip the item: {categoryName}, {categoryPath} >>> {item}");
                            continue;
                        }
                            
                        categoryItemsPathsToCheck.Add(categoryItemsPaths[item]);
                    }

                    List<RepoCategoryItem> categoryItems = new List<RepoCategoryItem>();

                    foreach (string categoryItemPath in categoryItemsPathsToCheck)
                    {
                        string categoryItemInitPath = Path.Combine(localRepoFolder, categoryPath, categoryItemPath, "init.json");

                        if (!Path.Exists(categoryItemInitPath)) 
                        { 
                            Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Skip the item: {categoryName}, {categoryPath} >>> {categoryItemInitPath}");
                            continue;
                        }

                        RepoCategoryItem repoCategoryItemInit = LoadJson<RepoCategoryItem>(categoryItemInitPath);

                        repoCategoryItemInit.category_id = repoCategoryInit.store_id;

                        ItemsList.Add(repoCategoryItemInit);
                        categoryItems.Add(repoCategoryItemInit);
                    }

                    RepoCategory repoCategory = new RepoCategory
                    {
                        store_id = repoCategoryInit.store_id,
                        type = repoCategoryInit.type,
                        name = categoryName,
                        items = categoryItems,
                    };
                    Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Category: {categoryName}, {categoryPath} >>> {repoCategoryInit.store_id}");

                    categories.Add(repoCategory);
                }

            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"Cannot load items: {ex}");
                StoreInternalErrorHappens?.Invoke($"Cannot load items: {ex}");
            }

            return categories;
        }

        public RepoCategory GetCategoryFromStoreId(string storeId)
        {
            foreach (RepoCategory repoCategory in FormattedStoreDatabase)
            {
                if (repoCategory.store_id == storeId)
                {
                    return repoCategory;
                }
            }
            return null;
        }

        public RepoCategoryItem GetItemInfoFromStoreId(string storeId)
        {
            foreach (RepoCategoryItem repoCategoryItem in ItemsList)
            {
                if (repoCategoryItem.store_id == storeId)
                {
                    return repoCategoryItem;
                }
            }
            return null;
        }

        public string GetLocalizedStoreItemName(string name, string langCode)
        {
            string localizedString = $"slocale:{name}";

            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string localRepoFolder = Path.Combine(localAppData, StoreDirName, StoreRepoCache, StoreRepoDirName);

            try
            {
                if (localeHelper.LocaleName != langCode)
                {
                    string locFilePath;
                    if (!StoreLocalizationPaths.ContainsKey(langCode))
                    {
                        locFilePath = Path.Combine(localRepoFolder, StoreLocalizationPaths["EN"]);
                    }
                    else
                    {
                        locFilePath = Path.Combine(localRepoFolder, StoreLocalizationPaths[langCode]);
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

        private static string StaticImageScript(string data)
        {
            return $"ms-appx:///Assets/{data}";
        }

        private static string DynamicPathConverter(string data)
        {
            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string targetFolder = Path.Combine(localAppData, StoreDirName, StoreRepoCache, StoreRepoDirName, data);
            return targetFolder;
        }

        public string ExecuteScript(string scriptString, string scriptArgs=null)
        {
            string executeResult = scriptString;
            if (scriptString.StartsWith("$"))
            {
                Match match = Regex.Match(scriptString, ScriptGetArgsRegex);
                string scriptData = "";

                if (match.Success)
                {
                    scriptData = match.Groups[1].Value;
                }

                if (scriptArgs != null)
                    scriptData = Regex.Replace(scriptData, @"{.*?}", scriptArgs);

                if (scriptString.StartsWith("$STATICIMAGE"))
                {
                    executeResult = StaticImageScript(scriptData);
                } 
                else if (scriptString.StartsWith("$DYNAMICIMAGE"))
                {
                    executeResult = DynamicPathConverter(scriptData);
                } 
                else if (scriptString.StartsWith("$LOADDYNAMIC"))
                {
                    executeResult = LoadAllTextFromFile(DynamicPathConverter(scriptData));
                }
                Logger.Instance.CreateDebugLog(nameof(UIHelper), $"Script {scriptString} execute result is {executeResult}, {scriptData}");
            }
            
            return executeResult;
        }

        public string LoadAllTextFromFile(string filepath)
        {
            return File.ReadAllText(filepath);
        }

        

        public void InitializeDatabase()
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Items (
                    Id TEXT PRIMARY KEY,
                    Directory TEXT NOT NULL,
                    UpdateCheckUrl TEXT,
                    DownloadUrl TEXT,
                    VersionControlType TEXT NOT NULL,
                    CurrentVersion TEXT NOT NULL,
                    RequiredItemIds TEXT,
                    DependentItemIds TEXT,
                    Icon TEXT,
                    Name TEXT NOT NULL,
                    FileHashes TEXT NOT NULL
                );";
            cmd.ExecuteNonQuery();
        }

        public void AddOrUpdateItem(DatabaseStoreItem item)
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Items (Id, Directory, UpdateCheckUrl, DownloadUrl, VersionControlType, CurrentVersion,
                    RequiredItemIds, DependentItemIds, Icon, Name)
                VALUES (@Id, @Directory, @UpdateCheckUrl, @DownloadUrl, @VersionControlType, @CurrentVersion,
                    @RequiredItemIds, @DependentItemIds, @Icon, @Name)
                ON CONFLICT(Id) DO UPDATE SET
                    Directory = excluded.Directory,
                    UpdateCheckUrl = excluded.UpdateCheckUrl,
                    DownloadUrl = excluded.DownloadUrl,
                    VersionControlType = excluded.VersionControlType,
                    CurrentVersion = excluded.CurrentVersion,
                    RequiredItemIds = excluded.RequiredItemIds,
                    DependentItemIds = excluded.DependentItemIds,
                    Icon = excluded.Icon,
                    Name = excluded.Name;";

            cmd.Parameters.AddWithValue("@Id", item.Id);
            cmd.Parameters.AddWithValue("@Directory", item.Directory);
            cmd.Parameters.AddWithValue("@UpdateCheckUrl", (object)item.UpdateCheckUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DownloadUrl", (object)item.DownloadUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@VersionControlType", item.VersionControlType);
            cmd.Parameters.AddWithValue("@CurrentVersion", item.CurrentVersion);
            cmd.Parameters.AddWithValue("@RequiredItemIds", item.RequiredItemIds != null ? string.Join(',', item.RequiredItemIds) : string.Empty);
            cmd.Parameters.AddWithValue("@DependentItemIds", item.DependentItemIds != null ? string.Join(',', item.DependentItemIds) : string.Empty);
            cmd.Parameters.AddWithValue("@Icon", item.IconPath);
            cmd.Parameters.AddWithValue("@Name", item.Name);

            cmd.ExecuteNonQuery();
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

            var item = new DatabaseStoreItem
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Directory = reader.GetString(reader.GetOrdinal("Directory")),
                UpdateCheckUrl = reader.IsDBNull(reader.GetOrdinal("UpdateCheckUrl"))
                    ? null : reader.GetString(reader.GetOrdinal("UpdateCheckUrl")),
                DownloadUrl = reader.IsDBNull(reader.GetOrdinal("DownloadUrl"))
                    ? null : reader.GetString(reader.GetOrdinal("DownloadUrl")),
                VersionControlType = reader.GetString(reader.GetOrdinal("VersionControlType")),
                CurrentVersion = reader.GetString(reader.GetOrdinal("CurrentVersion")),
                RequiredItemIds = reader.GetString(reader.GetOrdinal("RequiredItemIds"))
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                DependentItemIds = reader.GetString(reader.GetOrdinal("DependentItemIds"))
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                IconPath = reader.GetString(reader.GetOrdinal("Icon")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
            };

            return item;
        }
        public void DeleteItemById(string id)
        {
            using var connection = new SqliteConnection(LocalDatabaseConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Items WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);

            cmd.ExecuteNonQuery();
        }
        public bool IsItemInstalled(string id)
        {
            if (GetItemById(id) == null) return false; return true;
        }

        private void CreateDownloadManagerAndConnectHandlers(string operationId)
        {
            DownloadManager = new(operationId);

            DownloadManager.DownloadSpeedChanged += (speed) =>
            {
                ItemDownloadSpeedChanged?.Invoke(Tuple.Create(operationId, speed));
            };
            DownloadManager.ProgressChanged += (progress) =>
            {
                ItemDownloadProgressChanged?.Invoke(Tuple.Create(operationId, progress));
            };
            DownloadManager.TimeRemainingChanged += (time) =>
            {
                ItemTimeRemainingChanged?.Invoke(Tuple.Create(operationId, time));
            };
            DownloadManager.StageChanged += (stage) =>
            {
                ItemDownloadStageChanged?.Invoke(Tuple.Create(operationId, stage));
            };
            DownloadManager.ErrorHappens += (data) =>
            {
                string errorCode = data.Item1;
                string errorMessage = data.Item2;
                ItemInstallingErrorHappens?.Invoke(Tuple.Create(operationId, errorCode));
            };
        }

        private void DeleteDownloadManager(string operationId)
        {
            if (DownloadManager.OperationId == operationId)
                return;
            DownloadManager?.Dispose();
            DownloadManager = null;
        }

        private async Task<string> GetVersionDownloadLink(
            string repoUrl, string targetFileOrFileType, string version=null
        )
        {
            string link;
            try
            {
                var uri = new Uri(repoUrl);
                var parts = uri.AbsolutePath.Trim('/').Split('/');
                if (parts.Length < 2)
                    throw new ArgumentException("Invalid GitHub repository URL.", nameof(repoUrl));
                var owner = parts[0];
                var repo = parts[1];

                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("CDPIStore", "0.0.0.0"));
                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("token", GitHubApiToken);

                string apiUrl = version == null
                    ? $"https://api.github.com/repos/{owner}/{repo}/releases/latest"
                    : $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{version}";

                var response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var root = doc.RootElement;

                var tagName = root.GetProperty("tag_name").GetString();

                var assets = root.GetProperty("assets").EnumerateArray();
                var matches = assets
                    .Select(a => new
                    {
                        Name = a.GetProperty("name").GetString(),
                        Url = a.GetProperty("browser_download_url").GetString()
                    })
                    .Where(a =>
                        string.Equals(a.Name, targetFileOrFileType, StringComparison.OrdinalIgnoreCase)
                        || a.Name.EndsWith(targetFileOrFileType, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();

                if (matches.Count == 0)
                    throw new UriFormatException($"Url {repoUrl} isn't correct github url.");

                if (matches.Count > 1)
                {
                    SelectFileNeeded?.Invoke(Tuple.Create(repoUrl, tagName, matches.Select(m => m.Name).ToList()));
                    return "ERR_TOO_MANY_VARIANTS";
                }

                link = matches[0].Url;

                return link;
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }

        }

        private static string HandleException(Exception ex)
        {
            var codeObj = ErrorHelper.MapExceptionToCode(ex, out uint? hr);
            var code = codeObj.ToString();
            Logger.Instance.CreateErrorLog(nameof(ErrorHelper), $"{code} - {ex}");
            if (hr != null)
            {
                string hrHex = $"0x{hr.Value:X8}";
                return $"ERR_GET_URL_{code} ({hrHex})";
            }
            else
            {
                return $"ERR_GET_URL_{code}";
            }
        }

        public async Task InstallItem(string id, string version=null)
        {
            NowProcessItemActions?.Invoke(id);
            RepoCategoryItem item = GetItemInfoFromStoreId(id);

            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string itemFolder = Path.Combine(localAppData, StoreDirName, StoreItemsDirName, item.store_id);

            try
            {
                if (Path.Exists(itemFolder))
                {
                    Directory.Delete(itemFolder);
                }
                Directory.CreateDirectory(itemFolder);
            }
            catch (Exception ex)
            {
                ItemInstallingErrorHappens?.Invoke(Tuple.Create("", HandleException(ex)));
                return;
            }

            string downloadUrl = "";
            string filetype = item.filetype;
            
            if (item.version_control == "git_only_last")
            {
                downloadUrl = item.download_link;
            } 
            else if (item.version_control == "git")
            {
                downloadUrl = await GetVersionDownloadLink(item.version_control_link, item.filetype, version:version);
            }

            if (downloadUrl.StartsWith("ERR"))
            {
                if (downloadUrl == "ERR_TOO_MANY_VARIANTS")
                {
                    Logger.Instance.CreateWarningLog(nameof(StoreHelper), "TOO_MANY_VARIANTS exception happens.");
                }
                else
                {
                    ItemInstallingErrorHappens?.Invoke(Tuple.Create("", downloadUrl));
                    Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"{downloadUrl} exception happens.");
                }
                ItemActionsStopped?.Invoke(id);
                return;
            }

            await DownloadManager.DownloadAndExtractAsync(
                downloadUrl,
                itemFolder,
                extractArchive: item.filetype == "archive",
                extractSkipFiletypes: [".bat", ".cmd", ".vbs"],
                extractRootFolder: item.archive_root_folder,
                executableFileName: item.target_executable_file
            );

            ItemActionsStopped?.Invoke(id);
        }
        public async void InstallItemFromUrl(string id, string downloadUrl)
        {
            NowProcessItemActions?.Invoke(id);

            RepoCategoryItem item = GetItemInfoFromStoreId(id);

            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string itemFolder = Path.Combine(localAppData, StoreDirName, StoreItemsDirName, item.store_id);

            try
            {
                if (Path.Exists(itemFolder)) {
                    Directory.Delete(itemFolder);
                }
                Directory.CreateDirectory(itemFolder);
            }
            catch (Exception ex) 
            {
                ItemInstallingErrorHappens?.Invoke(Tuple.Create("", HandleException(ex)));
                return;
            }

            await DownloadManager.DownloadAndExtractAsync(
                downloadUrl,
                itemFolder,
                extractArchive: item.filetype == "archive",
                extractSkipFiletypes: [".bat", ".cmd", ".vbs"],
                extractRootFolder: item.archive_root_folder,
                executableFileName: item.target_executable_file
            );

            ItemActionsStopped?.Invoke(id);
        }
    }
}
