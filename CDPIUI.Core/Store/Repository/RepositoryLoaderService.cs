using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared;
using CDPIUI.Shared.Extentions;
using CDPIUI.Shared.PrettyErrorConvertionService;
using CDPIUI.Shared.Secrets;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;

namespace CDPIUI.Core.Store.Repository
{
    internal class RepositoryLoaderService: IRepositoryLoaderService
    {
        public SupportedVersionControls VersionControl { get; init; }

        private readonly List<string> SupportedCategoryTypes = ["basic_category", "second_category"];

        private static readonly string GitHubApiToken = Secret.GitHubToken;
        private static readonly string GitLabApiToken = Secret.GitLabToken;

        public event Action<ErrorModel>? InternalErrorHappens;

        public List<RepoCategoryModel>? FormattedStoreDatabase { get; private set; }

        public Dictionary<string, string>? StoreLocalizationPaths { get; private set; }

        public List<RepoItemModel> ItemsList { get; } = [];

        public List<ReadyKitModel> ReadyKits { get; } = [];

        public RepositoryLoaderService()
        {
            VersionControl = 
                SettingsManager.Instance.GetValueOrDefault<string>(
                    "STORE", 
                    "versionControlType", 
                    defaultValue: "GitHub")?
                .ToEnum(SupportedVersionControls.GitHub) ?? SupportedVersionControls.GitHub;
        }

        public string GetToken()
        {
            return VersionControl switch
            {
                SupportedVersionControls.GitHub => GitHubApiToken,
                SupportedVersionControls.GitLab => GitLabApiToken,
                _ => string.Empty,
            };
        }

        public static async Task<bool> TryLoadDatabaseForVersionControl(SupportedVersionControls versionControl)
        {
            try
            {
                string zipUrl = GetStoreUrl(versionControl);

                using HttpClient client = new();

                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CDPIUI_Components_Store", ApplicationInfo.Version));
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", versionControl == SupportedVersionControls.GitHub ? GitHubApiToken : GitLabApiToken);

                using HttpResponseMessage response = await client.GetAsync(zipUrl);
                response.EnsureSuccessStatusCode();

                string tempZipPath = Path.Combine(Path.GetTempPath(), "store_repo.tmp");
                await using (var fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }

                File.Delete(tempZipPath);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(RepositoryLoaderService), $"Error loading store database: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> LoadAllStoreDatabase(bool forseSync, SupportedVersionControls versionControl)
        {
            SupportedVersionControls usedVersionControl = versionControl == SupportedVersionControls.None ? VersionControl : versionControl;
            try
            {
                if (FormattedStoreDatabase != null && !forseSync)
                {
                    return true;
                }

                string targetFolder = Directories.StoreRepoCacheDirectory;

                TimeSpan t = DateTime.UtcNow - SettingsManager.Instance.GetValue<DateTime>("STORE", "lastSyncTime");

                if ((forseSync && t.TotalDays >= 1) || !Path.Exists(targetFolder))
                {

                    string zipUrl = GetStoreUrl(usedVersionControl);

                    using HttpClient client = new HttpClient();

                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CDPIUI_Components_Store", ApplicationInfo.Version));
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", usedVersionControl == SupportedVersionControls.GitHub ? GitHubApiToken : GitLabApiToken);

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
                InternalErrorHappens?.Invoke(new ErrorModel()
                {
                    ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("STORE_REPO", ex),
                    FriendlyDescription = $"Error loading store database: {ex.Message}",

                    Exception = ex
                });
                Logger.Instance.CreateErrorLog(nameof(RepositoryLoaderService), $"Error loading store database: {ex.Message}");
            }
            return false;
        }

        public RepoItemModel? GetItemInfoFromStoreId(string? storeId)
        {
            if (storeId == SharedConstants.LocalUserItemsId)
            {
                return null;
            }

            if (storeId == SharedConstants.ApplicationStoreId)
            {
                DatabaseStoreItem databaseStoreItem = DatabaseHelper.Instance.GetItemById(SharedConstants.ApplicationStoreId);
                RepoItemModel repoCategoryItem = new()
                {
                    store_id = storeId,
                    version_control = "git",
                    version_control_link = VersionControl == SupportedVersionControls.GitHub ? SharedConstants.ApplicationCheckUpdatesUrl : SharedConstants.ApplicationGitLabCheckUpdatesUrl,
                    filetype = State.IsApplicationBuildAsSingleFile ? "CDPIUIUpdateItem" : "UPDmsi",
                    target_executable_file = "patch",
                    developer = databaseStoreItem.Developer,
                    name = databaseStoreItem.Name,
                    short_name = databaseStoreItem.ShortName,
                    icon = databaseStoreItem.IconPath,


                };
                return repoCategoryItem;
            }

            foreach (RepoItemModel repoCategoryItem in ItemsList)
            {
                if (repoCategoryItem.store_id == storeId)
                {
                    return repoCategoryItem;
                }
            }
            return null;
        }

        public RepoCategoryModel? GetCategoryFromStoreId(string storeId)
        {
            foreach (RepoCategoryModel repoCategory in FormattedStoreDatabase)
            {
                if (repoCategory.store_id == storeId)
                {
                    return repoCategory;
                }
            }
            return null;
        }

        public ReadyKitModel? GetReadyKitFromStoreId(string storeId) =>
            ReadyKits.FirstOrDefault(kit => kit.store_id == storeId);

        public static void ClearRepoCache()
        {
            string targetFolder = Directories.StoreRepoCacheDirectory;

            if (Directory.Exists(targetFolder))
                Directory.Delete(targetFolder, recursive: true);
        }


        private List<RepoCategoryModel> GetFormattedStoreDatabase()
        {
            ItemsList?.Clear();
            ReadyKits.Clear();
            List<RepoCategoryModel> categories = new List<RepoCategoryModel>();

            string localAppData = CDPIUI.Core.Data.Directories.DataDirectory;
            string localRepoFolder = Directories.StoreRepoCacheDirectory;
            Debug.WriteLine(localRepoFolder);
            string localRepoInitFile = Path.Combine(localRepoFolder, "init.json");

            if (!File.Exists(localRepoInitFile))
                return categories;

            try
            {
                RepositoryInitializationModel repoInitData = JSONConvertor.LoadJson<RepositoryInitializationModel>(localRepoInitFile);

                StoreLocalizationPaths = repoInitData.localized_strings_directory;

                LoadReadyKits(localRepoFolder, repoInitData.kits_directory);

                List<string> categoriesAvailable = repoInitData.categories;
                Dictionary<string, string> categoriesPaths = repoInitData.categories_directory;

                List<List<string>> pathsToCheck = new List<List<string>>();

                if (categoriesAvailable != null)
                {
                    foreach (string category in categoriesAvailable)
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Checking the category: {category}");
                        if (!categoriesPaths.ContainsKey(category))
                        {
                            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the category: {category}");
                            continue;
                        }

                        pathsToCheck.Add([categoriesPaths[category], category]);
                    }
                }

                foreach (List<string> _cat in pathsToCheck)
                {
                    string categoryPath = _cat[0];
                    string categoryName = _cat[1];

                    string categoryInitPath = Path.Combine(localRepoFolder, categoryPath, "init.json");

                    if (!Path.Exists(categoryInitPath))
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the category: {categoryName}, {categoryPath}");
                        continue;
                    }

                    RepoCategoryInitializationModel repoCategoryInit = JSONConvertor.LoadJson<RepoCategoryInitializationModel>(categoryInitPath);

                    if (!SupportedCategoryTypes.Contains(repoCategoryInit.type))
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the category: {categoryName}, {categoryPath}");
                        continue;
                    }

                    List<string> items = repoCategoryInit.items;
                    Dictionary<string, string> categoryItemsPaths = repoCategoryInit.items_directories;

                    List<string> categoryItemsPathsToCheck = new List<string>();

                    foreach (string item in items)
                    {
                        if (!categoryItemsPaths.ContainsKey(item))
                        {
                            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the item: {categoryName}, {categoryPath} >>> {item}");
                            continue;
                        }

                        categoryItemsPathsToCheck.Add(categoryItemsPaths[item]);
                    }

                    List<RepoItemModel> categoryItems = new List<RepoItemModel>();

                    foreach (string categoryItemPath in categoryItemsPathsToCheck)
                    {
                        string categoryItemInitPath = Path.Combine(localRepoFolder, categoryPath, categoryItemPath, "init.json");

                        if (!Path.Exists(categoryItemInitPath))
                        {
                            Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the item: {categoryName}, {categoryPath} >>> {categoryItemInitPath}");
                            continue;
                        }

                        RepoItemModel repoCategoryItemInit = JSONConvertor.LoadJson<RepoItemModel>(categoryItemInitPath);

                        repoCategoryItemInit.category_id = repoCategoryInit.store_id;

                        ItemsList.Add(repoCategoryItemInit);
                        categoryItems.Add(repoCategoryItemInit);
                    }

                    RepoCategoryModel repoCategory = new RepoCategoryModel
                    {
                        store_id = repoCategoryInit.store_id,
                        type = repoCategoryInit.type,
                        name = categoryName,
                        items = categoryItems,
                    };
                    Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Category: {categoryName}, {categoryPath} >>> {repoCategoryInit.store_id}");

                    categories.Add(repoCategory);
                }

            }
            catch (Exception ex)
            {
                InternalErrorHappens?.Invoke(new ErrorModel()
                {
                    ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("STORE_REPO", ex),
                    FriendlyDescription = $"Cannot load Items: {ex.Message}",

                    Exception = ex
                });
            }

            return categories;
        }

        private void LoadReadyKits(string repositoryFolder, string? kitsDirectory)
        {
            if (string.IsNullOrWhiteSpace(kitsDirectory))
                return;

            string kitsInitPath = Path.Combine(repositoryFolder, kitsDirectory, "init.json");
            if (!File.Exists(kitsInitPath))
                return;

            try
            {
                ReadyKitsInitializationModel? initialization =
                    JSONConvertor.LoadJson<ReadyKitsInitializationModel>(kitsInitPath);

                if (initialization?.kits == null || initialization.kits_directories == null)
                    return;

                foreach (string kitName in initialization.kits)
                {
                    if (!initialization.kits_directories.TryGetValue(kitName, out string? kitDirectory) ||
                        string.IsNullOrWhiteSpace(kitDirectory))
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the ready kit: {kitName}");
                        continue;
                    }

                    string kitInitPath = Path.Combine(repositoryFolder, kitsDirectory, kitDirectory, "init.json");
                    if (!File.Exists(kitInitPath))
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the ready kit: {kitName} >>> {kitInitPath}");
                        continue;
                    }

                    ReadyKitModel? kit = JSONConvertor.LoadJson<ReadyKitModel>(kitInitPath);
                    if (kit == null || string.IsNullOrWhiteSpace(kit.store_id) || kit.items == null || kit.items.Count == 0)
                    {
                        Logger.Instance.CreateDebugLog(nameof(RepositoryLoaderService), $"Skip the invalid ready kit: {kitName}");
                        continue;
                    }

                    ReadyKits.Add(kit);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(RepositoryLoaderService), $"Cannot load ready kits: {ex.Message}");
            }
        }

        

        private static string GetStoreUrl(SupportedVersionControls versionControl)
        {
            return string.Format(
                    VersionControlData.VersionControlsLinks[versionControl],
                    SharedConstants.StoreRepo);
        }
    }
}
