using CDPIUI.Core.Communication;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Repository;
using CDPIUI.Shared;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System.Diagnostics;

namespace CDPIUI.Core.Features
{

    public class ApplicationUpdate
    {
        private static ApplicationUpdate? _instance;
        private static readonly object _lock = new object();

        public static ApplicationUpdate Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new ApplicationUpdate();
                    return _instance;
                }
            }
        }
        public Action? ErrorHappens;
        public Action? CheckForUpdatesStarted;
        public Action? CheckForUpdatesCompleted;

        public bool IsUpdateAvailable { get; private set; } = false;
        public bool ErrorHappened { get; private set; } = false;
        public string ErrorInfo { get; private set; } = string.Empty;

        public string ServerVersion { get; private set; } = string.Empty;

        private ApplicationUpdate()
        {
            StoreHelper.Instance.NowProcessItemActions += NowProcessItemActions;
            StoreHelper.Instance.ItemActionsStopped += ItemActionsStopped;
            StoreHelper.Instance.ItemInstallingErrorHappens += ItemInstallingErrorHappens;
        }

        private void NowProcessItemActions(string itemId)
        {
            if (itemId == SharedConstants.ApplicationStoreId)
            {
                ErrorHappened = false;
                ErrorInfo = string.Empty;
            }
        }

        private void ItemActionsStopped(string itemId)
        {
            if (itemId == SharedConstants.ApplicationStoreId)
            {
                if (!ErrorHappened)
                {
                    string filePath = Path.Combine(Directories.StoreItemsDirectory,
                        SharedConstants.ApplicationStoreId, 
                        State.IsApplicationBuildAsSingleFile ? "patch.cdpipatch" : "patch.msi");

                    if (File.Exists(filePath))
                    {
                        _ = PipeHelper.SendUpdatePacket(Shared.Pipe.Models.UpdateMessageIds.BeginApplicationUpdate, filePath);
                    }
                    else
                    {
                        ErrorHappened = true;
                        ErrorInfo = "ERR_FILE_NOT_FOUND";
                        ErrorHappens?.Invoke();
                    }

                }
            }
        }

        private void ItemInstallingErrorHappens(Tuple<string, ErrorModel> tuple)
        {
            string operationId = tuple.Item1;
            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) == 
                SharedConstants.ApplicationStoreId)
            {
                ErrorHappened = true;
                ErrorInfo = tuple.Item2.ErrorCode;
                ErrorHappens?.Invoke();
            }
        }

        public async Task<bool> CheckForUpdates(bool notify = false)
        {
            CheckForUpdatesStarted?.Invoke();
            IsUpdateAvailable = false;
            ErrorHappened = false;
            ErrorInfo = string.Empty;

            var _data = await StoreHelper.Instance.GetLastVersionAndVersionNotes(
                StoreHelper.Instance.VersionControl == SupportedVersionControls.GitHub ? 
                SharedConstants.ApplicationCheckUpdatesUrl :
                SharedConstants.ApplicationGitLabCheckUpdatesUrl
                );

            if (!_data.Success ||  _data.ErrorHappens) 
            {
                ErrorHappened = true;
                ErrorInfo = _data.Error.ErrorCode;
                ErrorHappens?.Invoke();
                return false;
            }

            Version serverVersion = new(_data.Result.ReleaseTag!);
            Version currentVersion = new(ApplicationInfo.Version);

            if (serverVersion > currentVersion)
            {
                ServerVersion = _data.Result.ReleaseTag!;
                IsUpdateAvailable = true;
                if (notify) await PipeHelper.SendUpdatePacket(
                    Shared.Pipe.Models.UpdateMessageIds.UpdatesAreAvailable);

            }

            CheckForUpdatesCompleted?.Invoke();

            return IsUpdateAvailable;
        }

        public void InstallApplicationUpdateFromFile(string filepath)
        {
            StoreHelper.Instance.AddItemToQueue(SharedConstants.ApplicationStoreId, 
                packFile: filepath);
        }

    }
}
