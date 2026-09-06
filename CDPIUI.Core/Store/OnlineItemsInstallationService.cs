using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Network;
using CDPIUI.Core.Store.Network.Models;
using CDPIUI.Core.Store.Queue;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Extentions;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System.Collections.Generic;
using System.Diagnostics;

namespace CDPIUI.Core.Store
{
    internal enum ItemsVersionControlTypes
    {
        git_only_last,
        git,
        several_repos,
        subscription,
    }

    internal class OnlineItemsInstallationService
    {
        public DownloadWorker? DownloadWorker { get; private set; }
        public required APIWorker APIWorker { get; init; }

        public event Action<Tuple<string, double>>? DownloadSpeedChanged;
        public event Action<Tuple<string, double>>? DownloadProgressChanged;
        public event Action<Tuple<string, TimeSpan>>? TimeRemainingChanged;
        public event Action<Tuple<string, string>>? DownloadStageChanged;

        public event Action<Tuple<string, ErrorModel>>? DownloadWorkerErrorHappens;

        public async Task<OperationResultModel<ILinkModel>> DownloadAndInstallItemFromOnlineStore (
            QueueItemModel qi,
            RepoItemModel item,
            CancellationTokenSource cancellationTokenSource
            )
        {
            CreateDownloadWorker(qi.OperationId, cancellationTokenSource);

            var result = await InstallationWorker(qi, item);

            return result;
        }

        internal void CompleteWork()
        {
            DeleteDownloadWorker();
        }

        public async Task<bool> LaunchBeforeInstallActions(string? actions, string destDir)
        {
            if (string.IsNullOrEmpty(actions)) return true;
            string[] _sActions = actions.Split(';');
            foreach (var _sAction in _sActions)
            {
                if (_sAction.StartsWith("DOWNLOAD="))
                {
                    var _p = _sAction.Substring(9).Split("$SEPARATOR");
                    if (_p.Length < 2) continue;
                    string url = _p[0];
                    string filename = _p[1];

                    if (DownloadWorker == null)
                    {
                        throw new NullReferenceException(nameof(DownloadWorker));
                    }

                    string extention = Path.GetExtension(FileSystemService.GetFileNameFromUrl(url));
                    bool result = 
                        await DownloadWorker.DownloadAndExtractAsync(
                            url, 
                            destDir, 
                            extractArchive: url.EndsWith(".zip"), 
                            null, 
                            null, 
                            string.Empty, 
                            extention, 
                            false, 
                            filename);
                    if (!result) return false;
                }
            }
            return true;
        }


        private async Task<OperationResultModel<ILinkModel>> InstallationWorker (
            QueueItemModel qi, 
            RepoItemModel item
            )
        {
            string id = qi.ItemId;
            string version = qi.Version;

            bool restartFlag = false;

            string itemFolder = Path.Combine(Directories.StoreItemsDirectory, id);

            string downloadUrl = "";
            string tag = "";

            var linksResult = await GetDownloadLinksForItem(item, version);

            if (!linksResult.Success || linksResult.ErrorHappens)
            {
                linksResult.Error.Object = id;
                Logger.Instance.CreateErrorLog(nameof(OnlineItemsInstallationService), $"{linksResult.Error.ErrorCode} exception happens.");
                linksResult.Error.ErrorCode = $"ERR_ONLINE_DOWNLOAD_{linksResult.Error.ErrorCode}";
                return OperationResultModel<ILinkModel>.FailureResult(linksResult.Error);
            }

            qi.Status = "WORK";

            if (DownloadWorker == null || linksResult.Result == null)
                return OperationResultModel<ILinkModel>
                    .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.NULL_REFERENCE));

            foreach (var downloadLink in linksResult.Result)
            {
                DownloadLinkModel downloadLinkModel;
                if (downloadLink is APILinkModel apiLink)
                {
                    downloadLinkModel = new()
                    {
                        link = apiLink.link,
                        version = apiLink.version,
                        type = item.filetype,
                        archive_root_folder = item.archive_root_folder,
                        actions = "keep",
                        target_executable_file = item.target_executable_file,
                    };
                }
                else if (downloadLink is DownloadLinkModel m)
                {
                    downloadLinkModel = m;
                }
                else
                {
                    return OperationResultModel<ILinkModel>
                        .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.UNKNOWN));
                }

                FileExtentionTypes filetype = downloadLinkModel.type.ToEnum(FileExtentionTypes.temp);

                bool result = await DownloadWorker.DownloadAndExtractAsync(
                    downloadLinkModel.link!,
                    itemFolder,
                    extractArchive: FileSystemService.CompressedFileTypes.Contains(filetype),
                    extractSkipFiletypes: [],
                    extractRootFolder: downloadLinkModel.archive_root_folder,
                    executableFileName: downloadLinkModel.target_executable_file,
                    filetype: filetype,
                    removeAfterAction: downloadLinkModel.actions == "remove"
                );

                if (DownloadWorker.IsRestartNeeded)
                {
                    restartFlag = true;
                }

                if (!result) return OperationResultModel<ILinkModel>
                        .FailureResult(DownloadWorker.LastError);
            }

            if (restartFlag)
                Logger.Instance.CreateDebugLog(nameof(StoreHelper), "Restart requested");

            return OperationResultModel<ILinkModel>.SuccessResult(new APILinkModel()
            {
                link = downloadUrl,
                version = tag
            });
        }

        private async Task<OperationResultModel<List<ILinkModel>>> GetDownloadLinksForItem(RepoItemModel item, string version)
        {
            if (item.version_control is null)
            {
                return OperationResultModel<List<ILinkModel>>
                    .FailureResult(ErrorModel.OnlyErrorCode(PrettyErrorCode.NULL_REFERENCE));
            }

            var versionControlType = item.version_control.ToEnum<ItemsVersionControlTypes>();

            List<ILinkModel> links = [];

            switch (versionControlType)
            {
                case ItemsVersionControlTypes.git_only_last:
                    var onlyLastVesionData =
                        await APIWorker.GetDownloadLinkForVersion(
                            item.version_control_link, item.filetype, version: version, prefferedFile: item.preffered_to_download_file_name);

                    if (onlyLastVesionData.Success && onlyLastVesionData.Result != null)
                    {
                        onlyLastVesionData.Result.link = item.download_link;
                        links.Add(onlyLastVesionData.Result);
                        break;
                    }

                    return OperationResultModel<List<ILinkModel>>
                        .FailureResult(
                        onlyLastVesionData.ErrorHappens ? onlyLastVesionData.Error! : ErrorModel.OnlyErrorCode(PrettyErrorCode.UNKNOWN));

                case ItemsVersionControlTypes.git:
                    var data =
                        await APIWorker.GetDownloadLinkForVersion(
                            item.version_control_link, item.filetype, version: version, prefferedFile: item.preffered_to_download_file_name);

                    if (data.Success && data.Result != null)
                    {
                        links.Add(data.Result);
                        break;
                    }

                    return OperationResultModel<List<ILinkModel>>
                        .FailureResult(data.ErrorHappens ? data.Error! : ErrorModel.OnlyErrorCode(PrettyErrorCode.UNKNOWN));

                case ItemsVersionControlTypes.several_repos:
                    var linksResult = await APIWorker.GetDownloadLinksAsync(item.files_to_download);

                    if (linksResult.Success && linksResult.Result != null)
                    {
                        links.AddRange(linksResult.Result);
                        break;
                    }

                    return OperationResultModel<List<ILinkModel>>
                        .FailureResult(linksResult.ErrorHappens ? linksResult.Error! : ErrorModel.OnlyErrorCode(PrettyErrorCode.UNKNOWN));
                case ItemsVersionControlTypes.subscription:
                    links.Add(new APILinkModel()
                    {
                        link = item.download_link,
                        version = "0.0.0",
                    });
                    break;
            }

            return OperationResultModel<List<ILinkModel>>
                .SuccessResult(links);
        }

        internal void CreateDownloadWorker(string operationId, CancellationTokenSource cancellationTokenSource)
        {
            DeleteDownloadWorker();

            DownloadWorker = new(operationId, cancellationTokenSource.Token);
            ConnectHandlersToDownloadWorker();
        }

        private void DeleteDownloadWorker()
        {
            DisconnectHandlersFromDownloadWorker();

            DownloadWorker?.Dispose();
            DownloadWorker = null;
        }

        public void ConnectHandlersToDownloadWorker()
        {
            if (DownloadWorker == null) return;

            DownloadWorker.DownloadSpeedChanged += DownloadWorker_DownloadSpeedChanged;
            DownloadWorker.ProgressChanged += DownloadWorker_ProgressChanged;
            DownloadWorker.TimeRemainingChanged += DownloadWorker_TimeRemainingChanged;
            DownloadWorker.StageChanged += DownloadWorker_StageChanged;
            DownloadWorker.ErrorHappens += DownloadWorker_ErrorHappens;
        }

        public void DisconnectHandlersFromDownloadWorker()
        {
            if (DownloadWorker == null) return;

            DownloadWorker.DownloadSpeedChanged -= DownloadWorker_DownloadSpeedChanged;
            DownloadWorker.ProgressChanged -= DownloadWorker_ProgressChanged;
            DownloadWorker.TimeRemainingChanged -= DownloadWorker_TimeRemainingChanged;
            DownloadWorker.StageChanged -= DownloadWorker_StageChanged;
            DownloadWorker.ErrorHappens -= DownloadWorker_ErrorHappens;
        }

        #region Handlers

        private void DownloadWorker_DownloadSpeedChanged(Tuple<string, double> tuple)
        {
            DownloadSpeedChanged?.Invoke(tuple);
        }
        private void DownloadWorker_ProgressChanged(Tuple<string, double> tuple)
        {
            DownloadProgressChanged?.Invoke(tuple);
        }
        private void DownloadWorker_TimeRemainingChanged(Tuple<string, TimeSpan> tuple)
        {
            TimeRemainingChanged?.Invoke(tuple);
        }
        private void DownloadWorker_StageChanged(Tuple<string, string> tuple)
        {
            DownloadStageChanged?.Invoke(tuple);
        }

        private void DownloadWorker_ErrorHappens(Tuple<string, string, string> data)
        {
            DownloadWorkerErrorHappens?.Invoke(Tuple.Create(data.Item1, new ErrorModel() 
            { 
                ErrorCode = $"ONLINE_DOWNLOAD_{data.Item2}",
                FriendlyDescription = data.Item3,
            }));
        }

        #endregion
    }

}
