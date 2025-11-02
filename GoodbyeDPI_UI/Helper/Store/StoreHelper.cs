using CDPI_UI.Helper.LScript;
using CDPI_UI.Helper.Static;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.WindowsAppSDK.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Services.Maps.LocalSearch;
using Windows.UI.WebUI;
using static CDPI_UI.Helper.ErrorsHelper;
using TimeSpan = System.TimeSpan;
using Version = System.Version;

namespace CDPI_UI.Helper
{
    public class UICategoryData
    {
        public string StoreId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }

        public ObservableCollection<UIElement> Items { get; set; }
    }

    public class ItemUpdateAvailable
    {
        public string StoreId { get; set; }
        public string CurrentVersion { get; set; }
        public string ServerVersion { get; set; }
        public string VersionInfo { get; set; }
    }

    public class License
    {
        public string name { get; set; }
        public string url { get; set; }
    }

    public class StoreHelper
    {
        private static readonly string GitHubApiToken = Secret.GitHubToken;

        private List<string> SupportedCategoryTypes = ["basic_category", "second_category"];

        private const string ScriptGetArgsRegex = @"\$.*?\((.*?)\)";

        private Dictionary<string, string> StoreLocalizationPaths;
        public List<RepoCategory> FormattedStoreDatabase;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
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

        

        public class FileToDownload
        {
            public string type;
            public string archive_root_folder;
            public string actions;
            public string version_control;
            public string version_control_link;
            public string download_link;
            public string preffered_version;
            public string preffered_to_download_file_name;
        }

        public class RepoCategoryItem
        {
            public string store_id;
            public string category_id;
            public string type;
            public string name;
            public string short_name;
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
            public List<FileToDownload> files_to_download;
            public List<License> license;
            public string version_control_link;
            public string download_link;
            public string filetype;
            public string preffered_to_download_file_name;
            public string archive_root_folder;
            public string target_executable_file;
            public string after_install_actions;
            public List<string[]> dependencies;
            public string target_minversion;
            public string target_maxversion;
        }



        // Download queue

        public class QueueItem
        {
            public string OperationId { get; }
            public string ItemId { get; }
            public string Version { get; }
            public bool CleanDirectoryBeforeInstalling { get; } = false;
            public string Status { get; set; } = "WAIT";
            public string DownloadStage { get; set; } = string.Empty;

            public QueueItem(string itemId, string operationId, string version = null, bool cleanDirectoryBeforeInstalling = false)
            {
                ItemId = itemId;
                Version = version ?? string.Empty;
                OperationId = operationId;
                CleanDirectoryBeforeInstalling = cleanDirectoryBeforeInstalling;
                Logger.Instance.CreateDebugLog(nameof(QueueItem), Version);
            }
        }
        private readonly Queue<QueueItem> _queue = new();
        private readonly object _queueLock = new();

        private QueueItem CurrentDownloadingItem;

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

        public event Action<string> ItemRemoved;

        public event Action QueueUpdated;

        public bool IsNowUpdatesChecked { get; private set; } = false;
        public bool IsExceptonHappensWhileCheckingUpdates {  get; private set; } = false;
        public List<ItemUpdateAvailable> UpdatesAvailableList { get; private set; } = [];
        public Action UpdateCheckStarted;
        public Action UpdateCheckStopped;

        private StoreHelper()
        {

        }

        public async Task<bool> LoadAllStoreDatabase(bool forseSync = true)
        {
            try
            {
                if (FormattedStoreDatabase != null && !forseSync)
                {
                    return true;
                }

                string localAppData = StateHelper.GetDataDirectory();
                string targetFolder = Path.Combine(
                    localAppData, StateHelper.StoreDirName, StateHelper.StoreRepoCache, StateHelper.StoreRepoDirName);

                TimeSpan t = DateTime.UtcNow - SettingsManager.Instance.GetValue<DateTime>("STORE", "lastSyncTime");

                if ((forseSync && t.TotalDays >= 1) || !Path.Exists(targetFolder)) {

                    string zipUrl = $"https://api.github.com/repos/{StateHelper.StoreRepo}/zipball/main";

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
                    SettingsManager.Instance.SetValue("STORE", "lastSyncTime", DateTime.UtcNow);
                }

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



        private List<RepoCategory> GetFormattedStoreDatabase()
        {
            ItemsList?.Clear();
            List<RepoCategory> categories = new List<RepoCategory>();

            string localAppData = StateHelper.GetDataDirectory();
            string localRepoFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreRepoCache, StateHelper.StoreRepoDirName);

            string localRepoInitFile = Path.Combine(localRepoFolder, "init.json");

            if (!File.Exists(localRepoInitFile))
                return categories;

            try
            {
                RepoInit repoInitData = Utils.LoadJson<RepoInit>(localRepoInitFile);

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

                    RepoCategoryInit repoCategoryInit = Utils.LoadJson<RepoCategoryInit>(categoryInitPath);

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

                        RepoCategoryItem repoCategoryItemInit = Utils.LoadJson<RepoCategoryItem>(categoryItemInitPath);

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
                Debug.WriteLine($"Cannot load Items: {ex}");
                StoreInternalErrorHappens?.Invoke($"Cannot load Items: {ex}");
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
            if (storeId == StateHelper.LocalUserItemsId)
            {
                return null;
            }

            if (storeId == StateHelper.ApplicationStoreId)
            {
                DatabaseStoreItem databaseStoreItem = DatabaseHelper.Instance.GetItemById(StateHelper.ApplicationStoreId);
                RepoCategoryItem repoCategoryItem = new()
                {
                    store_id = storeId,
                    version_control = "git",
                    version_control_link = StateHelper.ApplicationCheckUpdatesUrl,
                    filetype = Utils.IsApplicationBuildAsSingleFile? "CDPIUIUpdateItem" : "msi",
                    target_executable_file = "patch",
                    developer = databaseStoreItem.Developer,
                    name = databaseStoreItem.Name,
                    short_name = databaseStoreItem.ShortName,
                    icon = databaseStoreItem.IconPath,


                };
                return repoCategoryItem;
            }

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

            string localAppData = StateHelper.GetDataDirectory();
            string localRepoFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreRepoCache, StateHelper.StoreRepoDirName);

            if (name.Contains(" "))
                return name;

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

        public string ExecuteScript(string scriptString, string scriptArgs = null)
        {
            string executeResult = scriptString;
            try
            {
                if (scriptString != null && scriptString.StartsWith("$"))
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
                        executeResult = Static.Utils.StaticImageScript(scriptData);
                    }
                    else if (scriptString.StartsWith("$DYNAMICIMAGE"))
                    {
                        executeResult = Static.Utils.DynamicPathConverter(scriptData);
                    }
                    else if (scriptString.StartsWith("$LOADDYNAMIC"))
                    {
                        executeResult = Static.Utils.LoadAllTextFromFile(Static.Utils.DynamicPathConverter(scriptData));
                    }
                    Logger.Instance.CreateDebugLog(nameof(UIHelper), $"Script {scriptString} execute result is {executeResult}, {scriptData}");
                }
            }
            catch (Exception ex)
            {
                // pass
            }

            return executeResult;
        }

        public List<Tuple<string, string>> GetItemRequiredItemsById(string storeId)
        {
            if (!DatabaseHelper.Instance.IsItemInstalled(storeId))
                return null;

            List<Tuple<string, string>> requiredItems = [];

            DatabaseStoreItem item = DatabaseHelper.Instance.GetItemById(storeId);
            requiredItems = item.RequiredItemIds;

            return requiredItems;
        }



        // Queue

        public void AddItemToQueue(string itemId, string version, bool cleanDirectoryBeforeInstalling = false)
        {
            if (GetOperationIdFromItemId(itemId) != null) return;

            var opId = Guid.NewGuid().ToString();
            var qi = new QueueItem(itemId, opId, version, cleanDirectoryBeforeInstalling);

            lock (_queueLock)
            {
                _queue.Enqueue(qi);
                TryProcessNext();
            }
            QueueUpdated?.Invoke();
        }

        public bool RemoveItemFromQueue(string itemId)
        {
            var items = _queue.ToList();
            var removed = items.RemoveAll(i => i.ItemId == itemId) > 0;
            if (removed)
            {
                _queue.Clear();
                foreach (var i in items) _queue.Enqueue(i);
                QueueUpdated?.Invoke();
            }

            if (CurrentDownloadingItem != null && CurrentDownloadingItem.ItemId == itemId)
            {
                CurrentDownloadingItem.Status = "CANC";
                CurrentDownloadingItem.DownloadStage = "CANC";
                ItemDownloadStageChanged?.Invoke(Tuple.Create(CurrentDownloadingItem.OperationId, CurrentDownloadingItem.Status));
                DownloadManager?.Dispose();
                DownloadManager = null;
                QueueUpdated?.Invoke();
                return true;
            }
            return removed;
        }

        public Queue<QueueItem> GetQueue()
        {
            return _queue;
        }

        public string GetCurrentQueueOperationId()
        {
            return CurrentDownloadingItem != null ? CurrentDownloadingItem.OperationId : string.Empty;
        }

        public string GetOperationIdFromItemId(string storeId)
        {
            if (CurrentDownloadingItem != null && CurrentDownloadingItem.ItemId == storeId)
            {
                return CurrentDownloadingItem.OperationId;
            }
            foreach (var item in _queue)
            {
                if (item.ItemId == storeId)
                    return item.OperationId;
            }
            return null;
        }

        public string GetItemIdFromOperationId(string operationId)
        {
            if (CurrentDownloadingItem != null && CurrentDownloadingItem.OperationId == operationId)
                return CurrentDownloadingItem.ItemId;

            foreach (var item in _queue)
            {
                if (item.OperationId == operationId)
                    return item.ItemId;
            }
            return null;
        }

        public QueueItem GetQueueItemFromOperationId(string operationId)
        {
            if (CurrentDownloadingItem != null && CurrentDownloadingItem.OperationId == operationId)
                return CurrentDownloadingItem;

            foreach (var item in _queue)
            {
                if (item.OperationId == operationId)
                    return item;
            }
            return null;
        }

        private void TryProcessNext()
        {
            if (_queue.Count == 0)
                return;

            if (CurrentDownloadingItem == null)
            {

                var next = _queue.Dequeue();
                CurrentDownloadingItem = next;

                _ = ProcessAsync(next);
                QueueUpdated?.Invoke();
            }
        }

        private async Task ProcessAsync(QueueItem qi)
        {
            CreateDownloadManagerAndConnectHandlers(qi.OperationId);
            try
            {
                qi.Status = "GETR";
                qi.DownloadStage =qi.Status;
                ItemDownloadStageChanged?.Invoke(Tuple.Create(qi.OperationId, qi.Status));
                await InstallItem(qi);
                qi.Status = "END";
                qi.DownloadStage = qi.Status;
                ItemDownloadStageChanged?.Invoke(Tuple.Create(qi.OperationId, qi.Status));
            }
            catch
            {
                // pass
            }
            ItemActionsStopped?.Invoke(qi.ItemId);
            DeleteDownloadManager(qi.OperationId);

            lock (_lock)
            {
                CurrentDownloadingItem = null;
                QueueUpdated?.Invoke();
                TryProcessNext();
            }
        }

        private void CreateDownloadManagerAndConnectHandlers(string operationId)
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            DownloadManager = new(operationId, cancellationTokenSource);

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
                CurrentDownloadingItem.DownloadStage = stage;
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
            if (DownloadManager == null || DownloadManager.OperationId != operationId)
                return;
            DownloadManager?.Dispose();
            DownloadManager = null;
        }

        public static async Task<Tuple<string, string>> GetLastVersionAndVersionNotes(string repoUrl)
        {
            string notes;
            string tag;
            try
            {
                HttpResponseMessage response = await GetGithubResponse(repoUrl, null);

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var root = doc.RootElement;

                tag = root.GetProperty("tag_name").GetString();
                notes = root.GetProperty("body").GetString();

                return Tuple.Create(tag, notes);
            }
            catch (Exception ex)
            {
                return Tuple.Create<string, string>(HandleException(ex), null);
            }
        }


        private class DownloadLink
        {
            public string link;
            public string version;
            public string type;
            public string archive_root_folder;
            public bool errorHappens = false;
            public string errorCode;
            public string actions;
            public string target_executable_file;
        }
        private async Task<Tuple<string, string>> GetVersionDownloadLink(
            string repoUrl, string targetFileOrFileType, string version = null, string prefferedFile = null
        )
        {
            string link;
            string tag;
            try
            {
                HttpResponseMessage response = await GetGithubResponse(repoUrl, version);

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var root = doc.RootElement;

                tag = root.GetProperty("tag_name").GetString();

                var assets = root.GetProperty("assets").EnumerateArray();
                var matches = assets
                    .Select(a => new
                    {
                        Name = a.GetProperty("name").GetString(),
                        Url = a.GetProperty("browser_download_url").GetString()
                    })
                    .Where(a =>
                        string.Equals(a.Name, targetFileOrFileType, StringComparison.OrdinalIgnoreCase)
                        || a.Name.EndsWith(StateHelper.Instance.FileTypes[targetFileOrFileType], StringComparison.OrdinalIgnoreCase)
                    )
                    .Where(a =>
                        prefferedFile is null ||
                        a.Name.Contains(prefferedFile, StringComparison.Ordinal)
                    )
                    .ToList();

                if (matches.Count == 0)
                    return Tuple.Create<string, string>("ERR_INVALID_URL", tag);

                // FIX: Possible issue on update process.
                if (matches.Count > 1)
                {
                    SelectFileNeeded?.Invoke(Tuple.Create(repoUrl, tag, matches.Select(m => m.Name).ToList()));
                    return Tuple.Create<string, string>("ERR_TOO_MANY_VARIANTS", tag);
                }

                link = matches[0].Url;

                return Tuple.Create<string, string>(link, tag);
            }
            catch (Exception ex)
            {
                return Tuple.Create<string, string>(HandleException(ex), null);
            }

        }

        private async Task<List<DownloadLink>> GetDownloadLinksAsync(List<FileToDownload> filesToDownload) 
        {
            List<DownloadLink> links = [];
            bool errorHappens = false;
            string errorCode = string.Empty;

            foreach (var file in filesToDownload)
            {
                string downloadUrl, downloadVersion;
                if (file.version_control == "external_site_only_last")
                {
                    downloadUrl = file.download_link;
                    downloadVersion = null;
                }
                else
                {
                    Tuple<string, string> result = await GetVersionDownloadLink(file.version_control_link, file.type, file.preffered_version, file.preffered_to_download_file_name);
                    downloadUrl = result.Item1;
                    downloadVersion = result.Item2;
                    if (downloadUrl.StartsWith("ERR_"))
                    {
                        errorHappens = true;
                        errorCode = downloadUrl;
                    }
                }

                links.Add(new()
                {
                    link = downloadUrl,
                    version = downloadVersion,
                    type = file.type,
                    archive_root_folder = file.archive_root_folder,
                    errorCode = errorCode,
                    errorHappens = errorHappens,
                    actions = file.actions,
                    target_executable_file = null
                });
                if (errorHappens) break;
            }

            return links;
        }

        private static async Task<HttpResponseMessage> GetGithubResponse(string repoUrl, string version)
        {
            Debug.WriteLine(repoUrl);
            var uri = new Uri(repoUrl);
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length < 2)
                throw new ArgumentException("Invalid GitHub repository URL.", nameof(repoUrl));
            var owner = parts[0];
            var repo = parts[1];


            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("CDPIStore", StateHelper.Instance.Version));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("token", GitHubApiToken);

            string apiUrl = string.IsNullOrEmpty(version)
                ? $"https://api.github.com/repos/{owner}/{repo}/releases/latest"
                : $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{version}";

            

            var response = await client.GetAsync(apiUrl);
            Logger.Instance.CreateDebugLog(nameof(StoreHelper), version);
            Logger.Instance.CreateDebugLog(nameof(StoreHelper), apiUrl);

            response.EnsureSuccessStatusCode();
            return response;
        }

        private async Task InstallItem(QueueItem qi)
        {
            string id = qi.ItemId;
            string version = qi.Version;
            NowProcessItemActions?.Invoke(id);
            RepoCategoryItem item = GetItemInfoFromStoreId(id);

            if (item == null)
            {
                Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"Item not found exception happens.");
                ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, "ERR_ITEM_NOT_FOUND"));
                return;
            }

            List<Tuple<string, string>> requiredItems = [];
            if (DatabaseHelper.Instance.IsItemInstalled(id))
            {
                requiredItems = DatabaseHelper.Instance.GetItemById(id).RequiredItemIds;
            }

            string localAppData = StateHelper.GetDataDirectory();
            string itemFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName, item.store_id);

            try
            {
                if (Path.Exists(itemFolder) && 
                    !string.Equals(itemFolder, StateHelper.GetDataDirectory(), StringComparison.OrdinalIgnoreCase) &&
                    id != StateHelper.LocalUserItemsId && id != StateHelper.ApplicationStoreId)
                {
                    if (qi.CleanDirectoryBeforeInstalling)
                        Directory.Delete(itemFolder, recursive: true); 
                }
                Directory.CreateDirectory(itemFolder);
            }
            catch (Exception ex)
            {
                ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, HandleException(ex)));
                return;
            }

            // For testing only
            // await Task.Delay(10000);

            string downloadUrl = "";
            string tag = "";
            string filetype = item.filetype;
            List<DownloadLink> downloadLinks = [];

            if (item.version_control == "git_only_last")
            {
                var data = await GetVersionDownloadLink(item.version_control_link, item.filetype, version: version, prefferedFile: item.preffered_to_download_file_name);
                downloadUrl = item.download_link;
                tag = data.Item2;
            }
            else if (item.version_control == "git")
            {
                var data = await GetVersionDownloadLink(item.version_control_link, item.filetype, version: version, prefferedFile:item.preffered_to_download_file_name);
                downloadUrl = data.Item1;
                tag = data.Item2;
            }
            else if (item.version_control == "several_repos")
            {
                downloadLinks = await GetDownloadLinksAsync(item.files_to_download);
            }
            var errorLink = downloadLinks.FirstOrDefault(i => i.errorHappens);
            if (errorLink != null)
            {
                ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, errorLink.errorCode));
                Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"{errorLink.errorCode} exception happens.");
                return;
            }

            if (downloadUrl.StartsWith("ERR"))
            {
                if (downloadUrl == "ERR_TOO_MANY_VARIANTS")
                {
                    ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, downloadUrl));
                    Logger.Instance.CreateWarningLog(nameof(StoreHelper), "TOO_MANY_VARIANTS exception happens."); // TODO: Fix
                }
                else
                {
                    ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, downloadUrl));
                    Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"{downloadUrl} exception happens.");
                }
                return;
            }

            qi.Status = "WORK";

            if (DownloadManager == null)
                return;

            if (item.version_control == "several_repos")
            {
                bool restartFlag = false;
                foreach (var link in downloadLinks)
                {
                    bool result = await DownloadManager.DownloadAndExtractAsync(
                        link.link,
                        itemFolder,
                        extractArchive: link.type == "archive",
                        extractSkipFiletypes: [],
                        extractRootFolder: link.archive_root_folder,
                        executableFileName: link.target_executable_file,
                        filetype: link.type,
                        removeAfterAction: link.actions == "remove"
                    );

                    if (DownloadManager.IsRestartNeeded)
                    {
                        restartFlag = true;
                    }
                    if (!result)
                    {
                        return;
                    }
                }

                if (restartFlag) 
                    Logger.Instance.CreateDebugLog(nameof(StoreHelper), "Restart requested");
            }
            else
            {
                bool result = await DownloadManager.DownloadAndExtractAsync(
                    downloadUrl,
                    itemFolder,
                    extractArchive: item.filetype == "archive",
                    extractSkipFiletypes: [".bat", ".cmd", ".vbs"],
                    extractRootFolder: item.archive_root_folder,
                    executableFileName: item.target_executable_file,
                    filetype: item.filetype
                );
                if (!result) return;
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            if (id == StateHelper.ApplicationStoreId)
            {
                if (!Utils.IsApplicationBuildAsSingleFile) return;

                await GetPatchReadyToInstall(Path.Combine(itemFolder, "patch.cdpipatch"), qi.OperationId);
                return;
            }

            try
            {
                List<Tuple<string, string>> _dependencies = new List<Tuple<string, string>>();

                foreach (string[] dependency in item.dependencies)
                {
                    _dependencies.Add(Tuple.Create(dependency[0], dependency[1]));
                }

                DatabaseStoreItem databaseStoreItem = new DatabaseStoreItem()
                {
                    Id = qi.ItemId,
                    Type = item.type,
                    Name = item.name,
                    ShortName = item.short_name,
                    CurrentVersion = tag,
                    Directory = itemFolder,
                    Executable = item.target_executable_file,
                    DownloadUrl = downloadUrl,
                    DownloadFileType = item.filetype,
                    IconPath = item.icon,
                    UpdateCheckUrl = item.version_control_link,
                    VersionControlType = item.version_control,
                    DependentItemIds = _dependencies,
                    RequiredItemIds = requiredItems,
                    Developer = item.developer,
                    BackgroudColor = item.background,
                };

                foreach (var dependency in item.dependencies)
                {
                    if (!DatabaseHelper.Instance.IsItemInstalled(dependency[0]))
                        continue;

                    DatabaseStoreItem dependencyItem = DatabaseHelper.Instance.GetItemById(dependency[0]);
                    dependencyItem.RequiredItemIds.Add(Tuple.Create(databaseStoreItem.Id, dependency[1]));
                    DatabaseHelper.Instance.AddOrUpdateItem(dependencyItem);
                }

                // TODO: add RequiredItemIds to new installed item from store (foreach)

                DatabaseHelper.Instance.AddOrUpdateItem(databaseStoreItem);
                LScriptLangHelper.RunScript(item.after_install_actions);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(StoreHelper), $"{ex} exception happens.");
                ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, HandleException(ex)));
            }
        }

        private async Task<bool> GetPatchReadyToInstall(string filePath, string operationId)
        {
            string appItemDirectory = Path.Combine(StateHelper.GetDataDirectory(), StateHelper.StoreDirName, StateHelper.StoreItemsDirName, StateHelper.ApplicationStoreId);
            string patchesDir = Path.Combine(appItemDirectory, "Patches");
            string finalDir = Path.Combine(appItemDirectory, "CDPIUI");

            string makePatchFolder = Path.Combine(patchesDir, Path.GetFileNameWithoutExtension(filePath));
            await Utils.ExtractZip(filePath, "/", makePatchFolder);

            string requirementsFile = Path.Combine(makePatchFolder, "requirements.json");
            var requirements = Utils.LoadJson<PatchRequirements>(requirementsFile);

            List<string> patches = [];

            foreach (var requirement in requirements.Requirements)
            {
                TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                int secondsSinceEpoch = (int)t.TotalSeconds;
                string _dir = Path.Combine(makePatchFolder, secondsSinceEpoch.ToString());

                patches.Add(_dir);

                bool result = await DownloadManager.DownloadAndExtractAsync(
                    requirement,
                    _dir,
                    extractArchive: true,
                    extractSkipFiletypes: [],
                    extractRootFolder: "CDPIUI/",
                    executableFileName: "",
                    filetype: "archive"
                );

                if (!result)
                {
                    return false;
                }
            }

            patches.Reverse();

            try
            {
                // string[] dirs = Directory.GetDirectories(patchesDir, "*", SearchOption.TopDirectoryOnly);
                foreach (string dir in patches)
                {
                    Directory.Move(dir, finalDir); // TEST 
                }
                Directory.Move(Path.Combine(makePatchFolder, "CDPIUI"), finalDir);

                string finalPatchFileName = Path.Combine(appItemDirectory, "patch.cdpipatch");

                if (File.Exists(finalPatchFileName)) {
                    File.Delete(finalPatchFileName);
                }
                ZipFile.CreateFromDirectory(makePatchFolder, finalPatchFileName);
            }
            catch (Exception ex)
            {
                ItemInstallingErrorHappens?.Invoke(Tuple.Create(operationId, HandleException(ex)));
                return false;
            }

            return true;
        }

        public async void CheckUpdates()
        {
            if (IsNowUpdatesChecked) return;
            IsNowUpdatesChecked = true;
            UpdatesAvailableList.Clear();
            UpdateCheckStarted?.Invoke();

            List<DatabaseStoreItem> storeItems = DatabaseHelper.Instance.GetAllInstalledItems();
            bool exceptionHappens = false;

            foreach (var item in storeItems)
            {
                if (item.Id == StateHelper.LocalUserItemsId) continue;

                string repoUrl = item.UpdateCheckUrl;
                string downloadUrl = item.DownloadUrl;
                string versionControlType = item.VersionControlType;
                string directory = item.Directory;

                var versionData = await GetLastVersionAndVersionNotes(repoUrl);

                if (versionData.Item1.StartsWith("ERR"))
                {
                    Logger.Instance.CreateWarningLog(
                        $"{nameof(StoreHelper)}/{nameof(CheckUpdates)}", 
                        $"Cannot check updates for {item.Id}, with version control type {item.VersionControlType}. Uri used to check {repoUrl} " +
                        $"Exception information: {versionData.Item1}");
                    exceptionHappens = true;
                    continue;
                }

                try
                {
                    var currentVersion = new Version(item.CurrentVersion.Replace("v", ""));
                    var serverVersion = new Version(versionData.Item1.Replace("v", ""));

                    Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"{serverVersion}, {currentVersion}");

                    if (serverVersion > currentVersion)
                    {
                        UpdatesAvailableList.Add(new()
                        {
                            StoreId = item.Id,
                            CurrentVersion = item.CurrentVersion,
                            ServerVersion = versionData.Item1,
                            VersionInfo = versionData.Item2,
                        });
                    }
                }
                catch 
                {
                    Logger.Instance.CreateWarningLog(
                        $"{nameof(StoreHelper)}/{nameof(CheckUpdates)}",
                        $"Cannot compare versions {item.CurrentVersion}&&{versionData.Item1} for {item.Id}");
                    exceptionHappens = true;
                    continue;
                }
            }

            IsNowUpdatesChecked = false;
            IsExceptonHappensWhileCheckingUpdates = exceptionHappens;
            
            UpdateCheckStopped?.Invoke();
        }

        public void RemoveItem(string itemId)
        {
            _ = ProcessManager.Instance.StopService();
            var item = DatabaseHelper.Instance.GetItemById(itemId);
            
            try
            {
                if (item != null && Path.Exists(item.Directory)) {
                    Directory.Delete(item.Directory, recursive: true);
                }
                
            }
            catch { }
            DatabaseHelper.Instance.DeleteItemById(itemId);

            ItemRemoved?.Invoke(itemId);
        }

        private static string HandleException(Exception ex)
        {
            var codeObj = ErrorHelper.MapExceptionToCode(ex, out uint? hr);
            var code = codeObj.ToString();
            Logger.Instance.CreateErrorLog(nameof(ErrorHelper), $"{code} - {ex}");
            if (hr != null)
            {
                string hrHex = $"0x{hr.Value:X8}";
                return $"ERR_ITEM_INSTALLING_{code} ({hrHex})";
            }
            else
            {
                return $"ERR_ITEM_INSTALLING_{code}";
            }
        }
    }
}