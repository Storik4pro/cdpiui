using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.Network;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Core.Store.Repository;
using static CDPIUI.Core.Basic.ErrorsHelper;
using TimeSpan = System.TimeSpan;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Shared.PrettyErrorConvertionService;
using CDPIUI.Shared;
using CDPIUI.Core.LScript;
using CDPIUI.Core.JSON;
using CDPIUI.Shared.Models;
using CDPIUI.Core.Store.Application;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Network.Models;

namespace CDPIUI.Core.Store
{
    public partial class StoreHelper : 
        IUserExperienceService, IRepositoryLoaderService, ILocalizationService, IQueueManagerService, IAPIWorker
    {
        private readonly UserExperienceService UserExperienceService;
        private readonly RepositoryLoaderService RepositoryLoaderService = new();
        private readonly LocalizationService LocalizationService = new();
        private readonly QueueManagerService QueueManagerService = new();
        private readonly APIWorker APIWorker = new();
        private readonly OnlineItemsInstallationService OnlineItemsInstallationService;

        private ApplicationUpdateService? ApplicationUpdateService;

        private const string ScriptGetArgsRegex = @"\$.*?\((.*?)\)";

        

        private CancellationTokenSource? cancellationTokenSource;
        private CancellationToken? cancellationToken;        

        private static StoreHelper? _instance;
        private static readonly object _lock = new object();

        public static StoreHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new StoreHelper();
                    return _instance;
                }
            }
        }

        public event Action<string>? StoreInternalErrorHappens;
        public event Action<Tuple<string, string, List<string>>>? SelectFileNeeded;
        public event Action<string>? NowProcessItemActions;
        public event Action<string>? ItemActionsStopped;

        public event Action<Tuple<string, ErrorModel>>? ItemInstallingErrorHappens;
        public event Action<Tuple<string, double>>? ItemDownloadSpeedChanged;
        public event Action<Tuple<string, double>>? ItemDownloadProgressChanged;
        public event Action<Tuple<string, TimeSpan>>? ItemTimeRemainingChanged;
        public event Action<Tuple<string, string>>? ItemDownloadStageChanged;

        public event Action<string>? ItemRemoved;

        public event Action? QueueUpdated;
        public event Action? ErrorListUpdated;

        public bool IsNowUpdatesChecked { get; private set; } = false;
        public bool IsExceptonHappensWhileCheckingUpdates {  get; private set; } = false;
        public List<ItemUpdateAvailableModel> UpdatesAvailableList { get; private set; } = [];

        public Action? UpdateCheckStarted;
        public Action? UpdateCheckStopped;

        private StoreHelper()
        {
            OnlineItemsInstallationService = new() { APIWorker = APIWorker };
            UserExperienceService = new(this);

            OnlineItemsInstallationService.DownloadStageChanged += OnlineItemsInstallationService_DownloadStageChanged;
            OnlineItemsInstallationService.DownloadProgressChanged += OnlineItemsInstallationService_DownloadProgressChanged;
            OnlineItemsInstallationService.TimeRemainingChanged += OnlineItemsInstallationService_TimeRemainingChanged;
            OnlineItemsInstallationService.DownloadSpeedChanged += OnlineItemsInstallationService_DownloadSpeedChanged;

            OnlineItemsInstallationService.DownloadWorkerErrorHappens += OnlineItemsInstallationService_DownloadWorkerErrorHappens;

            RepositoryLoaderService.InternalErrorHappens += RepositoryLoaderService_InternalErrorHappens;

            QueueManagerService.CurrentItemRemovedFromQueue += QueueManagerService_CurrentItemRemovedFromQueue;
            QueueManagerService.ProcessItem += QueueManagerService_ProcessItem;
            QueueManagerService.QueueUpdated += QueueUpdated; // Possible action issue. 
            QueueManagerService.ErrorListUpdated += ErrorListUpdated; // Possible action issue. 

            APIWorker.VersionControl = RepositoryLoaderService.VersionControl;
            APIWorker.Token = RepositoryLoaderService.GetToken();
        }

        private void RepositoryLoaderService_InternalErrorHappens(Shared.PrettyErrorConvertionService.ErrorModel obj)
        {
            StoreInternalErrorHappens?.Invoke(obj.FriendlyDescription ?? obj.ErrorCode);
        }

        #region API
        public async Task<OperationResultModel<ReleaseInfoModel>> GetLastVersionAndVersionNotes(string url) =>
            await APIWorker.GetLastVersionAndVersionNotes(url);

        #endregion

        #region UserExperience

        public List<Tuple<string, string>>? GetItemRequiredItemsById(string storeId) => 
            UserExperienceService.GetItemRequiredItemsById(storeId);

        public List<RepoItemModel> GetSimilarItemsForStoreId(string storeId) => 
            UserExperienceService.GetSimilarItemsForStoreId(storeId);

        #endregion

        #region Database

        public SupportedVersionControls VersionControl => RepositoryLoaderService.VersionControl;
        public List<RepoCategoryModel>? FormattedStoreDatabase => RepositoryLoaderService.FormattedStoreDatabase;
        public Dictionary<string, string>? StoreLocalizationPaths => RepositoryLoaderService.StoreLocalizationPaths;
        public List<RepoItemModel> ItemsList => RepositoryLoaderService.ItemsList;

        public static async Task<bool> TryLoadDatabaseForVersionControl(SupportedVersionControls versionControl) => 
            await RepositoryLoaderService.TryLoadDatabaseForVersionControl(versionControl);

        public async Task<bool> LoadAllStoreDatabase(bool forseSync = true, SupportedVersionControls versionControl = SupportedVersionControls.None) =>
            await RepositoryLoaderService.LoadAllStoreDatabase(forseSync, versionControl);


        public RepoItemModel? GetItemInfoFromStoreId(string? storeId) => 
            RepositoryLoaderService.GetItemInfoFromStoreId(storeId);

        public RepoCategoryModel? GetCategoryFromStoreId(string storeId) => 
            RepositoryLoaderService.GetCategoryFromStoreId(storeId);

        public static void ClearRepoCache() => 
            RepositoryLoaderService.ClearRepoCache();

        #endregion

        #region Localization
        public string GetLocalizedStoreItemName(string name, string langCode)
        {
            LocalizationService.StoreLocalizationPaths = RepositoryLoaderService.StoreLocalizationPaths?? [];
            return LocalizationService.GetLocalizedStoreItemName(name, langCode);
        }
        #endregion

        #region Failure items list
        public List<QueueItemModel> GetFailedToInstallItems() =>
            QueueManagerService.GetFailedToInstallItems();

        public void RemoveItemFromDownloadFailureList(string itemId) =>
            QueueManagerService.RemoveItemFromDownloadFailureList(itemId);

        private void AddItemToDownloadFailureList(string itemId, string operationId, string? version, string errorCode) =>
            QueueManagerService.AddItemToDownloadFailureList(itemId, operationId, version, errorCode);
        #endregion

        #region Queue
        public void AddItemToQueue(string itemId, string? version = null, bool cleanDirectoryBeforeInstalling = false, string? packFile = null) =>
            QueueManagerService.AddItemToQueue(itemId, version, cleanDirectoryBeforeInstalling, packFile);

        public bool RemoveItemFromQueue(string itemId) => 
            QueueManagerService.RemoveItemFromQueue(itemId);

        public string GetCurrentQueueOperationId() => 
            QueueManagerService.GetCurrentQueueOperationId();

        public string? GetItemIdFromOperationId(string operationId) => 
            QueueManagerService.GetItemIdFromOperationId(operationId);

        public string? GetOperationIdFromItemId(string storeId) =>
            QueueManagerService.GetItemIdFromOperationId(storeId);

        public Queue<QueueItemModel> GetQueue() =>
            QueueManagerService.GetQueue();

        public QueueItemModel? GetQueueItemFromOperationId(string operationId) =>
            QueueManagerService.GetQueueItemFromOperationId(operationId);

        private void QueueManagerService_CurrentItemRemovedFromQueue()
        {
            ItemDownloadStageChanged?.Invoke(
                Tuple.Create(QueueManagerService.CurrentDownloadingItem.OperationId, QueueManagerService.CurrentDownloadingItem.Status));
            OnlineItemsInstallationService.CancelWork();

            cancellationTokenSource?.Cancel();
        }

        private void QueueManagerService_ProcessItem(QueueItemModel obj)
        {
            _ = ProcessAsync(obj);
        }

        

        private async Task ProcessAsync(QueueItemModel qi)
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;

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

            var _item = UpdatesAvailableList.FirstOrDefault(x => x.StoreId == qi.ItemId);
            if (_item != null)
            {
                UpdatesAvailableList.Remove(_item);
            }

            lock (_lock)
            {
                QueueManagerService.CurrentDownloadingItem = null;
                QueueManagerService.TryProcessNext();
            }
        }

        #endregion      

        public void RemoveItem(string itemId)
        {
            _ = ProcessService.StopService();
            _ = ComponentTasksManager.Instance.StopTask(itemId);
            var item = DatabaseHelper.Instance.GetItemById(itemId);

            if (item == null ||
                item.Id == SharedConstants.ApplicationStoreId ||
                item.Id == SharedConstants.LocalUserItemsId)
                return;

            try
            {
                if (Path.Exists(item.Directory))
                {
                    Directory.Delete(item.Directory, recursive: true);
                }

            }
            catch { }
            DatabaseHelper.Instance.DeleteItemById(itemId);

            ItemRemoved?.Invoke(itemId);
        }

        public async Task CheckUpdates()
        {
            if (IsNowUpdatesChecked) return;
            IsNowUpdatesChecked = true;
            UpdatesAvailableList.Clear();
            UpdateCheckStarted?.Invoke();

            List<DatabaseStoreItem> storeItems = DatabaseHelper.Instance.GetAllInstalledItems();
            bool exceptionHappens = false;

            foreach (var item in storeItems)
            {
                if (item.Id == SharedConstants.LocalUserItemsId) continue;

                string downloadUrl = item.DownloadUrl;
                string versionControlType = item.VersionControlType;
                string directory = item.Directory;

                string repoUrl = GetReadyToUseRepoUrl(item.UpdateCheckUrl, item.Id);

                var versionDataCheckResult = await APIWorker.GetLastVersionAndVersionNotes(repoUrl);

                if (versionDataCheckResult.ErrorHappens || !versionDataCheckResult.Success)
                {
                    Logger.Instance.CreateWarningLog(
                        $"{nameof(StoreHelper)}/{nameof(CheckUpdates)}",
                        $"Cannot check updates for {item.Id}, with version control type {item.VersionControlType}. Uri used to check {repoUrl} " +
                        $"Exception information: {versionDataCheckResult.Error.ErrorCode}");
                    exceptionHappens = true;
                    continue;
                }

                var versionData = versionDataCheckResult.Result;

                try
                {
                    string curV = item.CurrentVersion;
                    string serV = versionData.ReleaseTag;

                    if (VersionHelper.CompareVersionStrings(curV, serV) == -1)
                    {
                        UpdatesAvailableList.Add(new()
                        {
                            StoreId = item.Id,
                            CurrentVersion = item.CurrentVersion,
                            ServerVersion = versionData.ReleaseTag,
                            VersionInfo = versionData.ReleaseNotes,
                        });
                    }
                }
                catch
                {
                    Logger.Instance.CreateWarningLog(
                        $"{nameof(StoreHelper)}/{nameof(CheckUpdates)}",
                        $"Cannot compare versions {item.CurrentVersion}&&{versionData.ReleaseNotes} for {item.Id}");
                    exceptionHappens = true;
                    continue;
                }
            }

            IsNowUpdatesChecked = false;
            IsExceptonHappensWhileCheckingUpdates = exceptionHappens;

            UpdateCheckStopped?.Invoke();
        }

        private async Task InstallItem(QueueItemModel qi)
        {
            var result = await InstallItemWorker(qi);

            if (!result.Success)
            {
                ErrorModel errorModel;
                if (!result.ErrorHappens || result.Error == null)
                {
                    errorModel = HandleException(PrettyErrorCode.UNKNOWN);
                }
                else
                {
                    errorModel = result.Error;
                }

                ItemInstallingErrorHappens?.Invoke(Tuple.Create(qi.OperationId, errorModel));
                if (string.IsNullOrEmpty(qi.PackFilePath)) AddItemToDownloadFailureList(qi.ItemId, qi.OperationId, qi.Version, errorModel.ErrorCode);

                try
                {
                    RemoveItem(qi.ItemId);
                }
                catch { }
            }
        }

        private async Task<OperationResultModel<EmptyResult>> InstallItemWorker(QueueItemModel qi)
        {
            string id = qi.ItemId;
            string version = qi.Version;
            string packFilePath = qi.PackFilePath;
            NowProcessItemActions?.Invoke(id);
            

            List<Tuple<string, string>> requiredItems = [];
            if (DatabaseHelper.Instance.IsItemInstalled(id))
            {
                requiredItems = DatabaseHelper.Instance.GetItemById(id).RequiredItemIds;
            }

            string itemFolder = Path.Combine(Directories.StoreItemsDirectory, qi.ItemId);

            var createDirResult = CreateItemDirectory(id, itemFolder, qi.CleanDirectoryBeforeInstalling);
            if (createDirResult.ErrorHappens) return createDirResult;

            // For testing only
            // await Task.Delay(10000);

            RepoItemModel item;
            string tag;
            string downloadUrl;

            if (string.IsNullOrEmpty(packFilePath))
            {
                var onlineDownloadResult = await DownloadItemOnline(id, qi);

                if (!onlineDownloadResult.Success) return onlineDownloadResult.ToEmptyResult();

                item = onlineDownloadResult.Result.Item3;
                tag = onlineDownloadResult.Result.Item1;
                downloadUrl = onlineDownloadResult.Result.Item2;
            }
            else
            {
                OnlineItemsInstallationService.CreateDownloadWorker(qi.OperationId, cancellationTokenSource!);

                var offlineCopyResult = GetReadyLocalItem(qi);

                if (!offlineCopyResult.Success) return offlineCopyResult.ToEmptyResult();

                item = offlineCopyResult.Result.Item3;
                tag = offlineCopyResult.Result.Item1;
                downloadUrl = offlineCopyResult.Result.Item2;
            }

            if (cancellationToken?.IsCancellationRequested ?? false)
                return OperationResultModel<EmptyResult>
                    .UnSuccessResult();


            if (id == SharedConstants.ApplicationStoreId)
            {
                if (State.IsApplicationBuildAsMsi) return OperationResultModel<EmptyResult>.SuccessResult();

                ApplicationUpdateService = new();
                var gettingPatchReadyResult = await ApplicationUpdateService
                    .GetPatchReadyToInstall(
                    Path.Combine(itemFolder, "patch.cdpipatch"), 
                    qi.OperationId, 
                    OnlineItemsInstallationService.DownloadWorker);

                ApplicationUpdateService = null;
                return gettingPatchReadyResult;
            }

            return await AddItemToDatabase(tag, downloadUrl, itemFolder, item, requiredItems);
        }

        private async Task<OperationResultModel<Tuple<string, string, RepoItemModel>>> DownloadItemOnline(string itemId, QueueItemModel qi)
        {
            RepoItemModel item;
            string tag;
            string downloadUrl;

            item = GetItemInfoFromStoreId(itemId);

            if (item == null)
            {
                return OperationResultModel<Tuple<string, string, RepoItemModel>>
                    .FailureResult(HandleException(PrettyErrorCode.ITEM_NOT_FOUND));
            }

            var result = await OnlineItemsInstallationService.DownloadAndInstallItemFromOnlineStore(qi, item, cancellationTokenSource);

            if (!result.Success && result.ErrorHappens)
            {
                OnlineItemsInstallationService.CancelWork();
                return OperationResultModel<Tuple<string, string, RepoItemModel>>.FailureResult(result.Error!);
            }

            if (result.Result == null || result.Result.version == null || result.Result.link == null) 
                return OperationResultModel<Tuple<string, string, RepoItemModel>>
                    .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.NULL_REFERENCE));

            tag = result.Result.version;
            downloadUrl = result.Result.link;

            return OperationResultModel<Tuple<string, string, RepoItemModel>>
                .SuccessResult(Tuple.Create(tag, downloadUrl, item));
        }
        
        private OperationResultModel<Tuple<string, string?, RepoItemModel>> GetReadyLocalItem(QueueItemModel qi)
        {
            RepoItemModel item;
            string tag;
            string downloadUrl;

            string id = qi.ItemId;
            string packFilePath = qi.PackFilePath!;

            string itemFolder = Path.Combine(Directories.StoreItemsDirectory, qi.ItemId);

            var copyResult = CopyLocalPack(id, itemFolder, packFilePath);
            if (!copyResult.Success) return OperationResultModel<Tuple<string, string?, RepoItemModel>>.FailureResult(copyResult.Error);

            if (qi.ItemId == SharedConstants.ApplicationStoreId)
            {
                item = GetItemInfoFromStoreId(qi.ItemId);
                tag = string.Empty;
                downloadUrl = null;
            }
            else
            {
                string initFilePath = Path.Combine(itemFolder, "init.json");

                if (!File.Exists(initFilePath))
                    return OperationResultModel<Tuple<string, string?, RepoItemModel>>
                        .FailureResult(HandleException(PrettyErrorCode.PACK_NOT_SUPPORTED_TYPE));

                LocalItemInitModel localItemInitModel = JSONConvertor.LoadJson<LocalItemInitModel>(initFilePath);

                item = new()
                {
                    store_id = qi.ItemId,
                    type = localItemInitModel.Type ?? "configlist",
                    name = localItemInitModel.Name ?? localItemInitModel.ShortName ?? qi.ItemId,
                    short_name = localItemInitModel.ShortName ?? qi.ItemId,
                    target_executable_file = localItemInitModel.ExecutableFile,
                    filetype = "localPack",
                    version_control = "local",
                    icon = localItemInitModel.Icon,
                    developer = localItemInitModel.Developer,
                    background = localItemInitModel.Color,
                    dependencies = localItemInitModel.Requirements,
                    before_install_actions = localItemInitModel.BeforeInstallActions,
                    after_install_actions = localItemInitModel.AfterInstallActions,
                };
                tag = localItemInitModel.Version;
                downloadUrl = null;
            }

            if (item == null)
                return OperationResultModel<Tuple<string, string?, RepoItemModel>>
                    .FailureResult(HandleException(PrettyErrorCode.NULL_REFERENCE));

            return OperationResultModel<Tuple<string, string?, RepoItemModel>>
                .SuccessResult(Tuple.Create(tag, downloadUrl, item));
        }

        private static OperationResultModel<EmptyResult> CreateItemDirectory(string itemId, string itemFolder, bool clean)
        {
            if (itemId == SharedConstants.ApplicationStoreId || itemId == SharedConstants.LocalUserItemsId)
                clean = false;

            try
            {
                if (Path.Exists(itemFolder) && !string.Equals(itemFolder, Directories.DataDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    if (clean)
                        Directory.Delete(itemFolder, recursive: true);
                }
                Directory.CreateDirectory(itemFolder);
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>
                    .FailureResult(ErrorModel.OnlyErrorCode(HandleException(ex)));
            }

            return OperationResultModel<EmptyResult>
                .SuccessResult();
        }

        private static OperationResultModel<EmptyResult> CopyLocalPack(string itemId, string itemFolder, string packFilePath)
        {
            try
            {
                if (itemId != SharedConstants.ApplicationStoreId)
                {
                    if (Directory.Exists(itemFolder)) Directory.Delete(itemFolder, recursive: true);
                    Directory.Move(packFilePath, itemFolder);
                }
                else
                {
                    File.Copy(packFilePath, Path.Combine(itemFolder, "patch.cdpipatch"), overwrite: true);
                }
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>
                    .FailureResult(ErrorModel.OnlyErrorCode(HandleException(ex)));
            }

            return OperationResultModel<EmptyResult>
                .SuccessResult();
        }
        
        private async Task<OperationResultModel<EmptyResult>> AddItemToDatabase(
            string version,
            string? downloadUrl,
            string itemFolder,
            RepoItemModel item,
            List<Tuple<string, string>>? requiredItems)
        {
            List<Tuple<string, string>> _dependencies = [];

            foreach (string[] dependency in item.dependencies ?? [])
            {
                _dependencies.Add(Tuple.Create(dependency[0], dependency[1]));
            }

            DatabaseStoreItem databaseStoreItem = new()
            {
                Id = item.store_id,
                Type = item.type,
                Name = item.name,
                ShortName = item.short_name,
                CurrentVersion = version,
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


            try
            {
                if (!await OnlineItemsInstallationService.LaunchBeforeInstallActions(
                        await LScriptCore.RunScript(item.before_install_actions),
                        itemFolder))
                    return OperationResultModel<EmptyResult>
                        .UnSuccessResult();


                AddDependencies(item.dependencies, item.store_id!);

                // TODO: add RequiredItemIds to new installed item from store (foreach)

                Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Adding item {item.name} to database...");
                DatabaseHelper.Instance.AddOrUpdateItem(databaseStoreItem);

                Dictionary<string, string> extraArgs = new()
                {
                    { "CurrentDirectory", itemFolder }
                };

                await LScriptCore.RunScript(item.after_install_actions, extraArgs: extraArgs, cancellationToken: cancellationToken);

                Logger.Instance.CreateDebugLog(nameof(StoreHelper), $"Adding item {item.name}... COMPLETE");

                return OperationResultModel<EmptyResult>.SuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>
                    .FailureResult(ErrorModel.OnlyErrorCode(HandleException(ex)));
            }
        }

        private static void AddDependencies(List<string[]> dependencies, string id)
        {
            foreach (var dependency in dependencies)
            {
                if (!DatabaseHelper.Instance.IsItemInstalled(dependency[0]))
                    continue;

                DatabaseStoreItem dependencyItem = DatabaseHelper.Instance.GetItemById(dependency[0]);
                dependencyItem?.RequiredItemIds?.Add(Tuple.Create(id, dependency[1]));
                if (dependencyItem != null) DatabaseHelper.Instance.AddOrUpdateItem(dependencyItem);
            }

        }
        
        private string? GetReadyToUseRepoUrl(string? repoUrl, string? storeId)
        {
            if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out var repoUri))
            {
                return repoUrl;
            }
            string siteName = repoUri.GetLeftPart(UriPartial.Authority);
            if (!string.IsNullOrEmpty(siteName))
            {
                if (VersionControl == SupportedVersionControls.GitHub)
                {
                    if (!siteName.Equals("github.com", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var item = GetItemInfoFromStoreId(storeId);
                        return item.version_control_link;
                    }
                }
                else if (VersionControl == SupportedVersionControls.GitLab)
                {
                    if (!siteName.Equals("gitlab.com", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var item = GetItemInfoFromStoreId(storeId);
                        return item.version_control_link;
                    }
                }
            }

            return siteName;
        }

        

        #region Handlers
        private void OnlineItemsInstallationService_DownloadWorkerErrorHappens(Tuple<string, ErrorModel> obj)
        {
            AddItemToDownloadFailureList(
                        GetItemIdFromOperationId(obj.Item1) ?? string.Empty, obj.Item1, null, obj.Item2.ErrorCode);

            ItemInstallingErrorHappens?.Invoke(Tuple.Create(obj.Item1, obj.Item2));
        }

        private void OnlineItemsInstallationService_DownloadSpeedChanged(Tuple<string, double> obj)
        {
            ItemDownloadSpeedChanged?.Invoke(obj);
        }

        private void OnlineItemsInstallationService_TimeRemainingChanged(Tuple<string, TimeSpan> obj)
        {
            ItemTimeRemainingChanged?.Invoke(obj);
        }

        private void OnlineItemsInstallationService_DownloadProgressChanged(Tuple<string, double> obj)
        {
            ItemDownloadProgressChanged?.Invoke(obj);
        }

        private void OnlineItemsInstallationService_DownloadStageChanged(Tuple<string, string> obj)
        {
            ItemDownloadStageChanged?.Invoke(obj);
        }
        #endregion

        #region Exception Handlers
        private static ErrorModel HandleException(PrettyErrorCode error)
        {
            return new() { ErrorCode = $"ERR_STORE_{error}" };
        }

        private static string HandleException(Exception ex)
        {
            string errorCode = Convertor.GetPrettyErrorCode("ERR_ITEM_INSTALLING", ex);
            return errorCode;
        }

        #endregion
    }
}
