using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper
{
    public class ApplicationUpdateHelper
    {
        private static ApplicationUpdateHelper _instance;
        private static readonly object _lock = new object();

        public static ApplicationUpdateHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ApplicationUpdateHelper();
                    return _instance;
                }
            }
        }
        public Action ErrorHappens;

        public bool IsUpdateAvailable { get; private set; } = false;
        public bool ErrorHappened { get; private set; } = false;
        public string ErrorInfo { get; private set; } = string.Empty;

        public string ServerVersion { get; private set; } = string.Empty;

        private ApplicationUpdateHelper()
        {
            StoreHelper.Instance.NowProcessItemActions += NowProcessItemActions;
            StoreHelper.Instance.ItemActionsStopped += ItemActionsStopped;
            StoreHelper.Instance.ItemInstallingErrorHappens += ItemInstallingErrorHappens;
        }

        private void NowProcessItemActions(string itemId)
        {
            if (itemId == StateHelper.ApplicationStoreId)
            {
                ErrorHappened = false;
                ErrorInfo = string.Empty;
            }
        }

        private void ItemActionsStopped(string itemId)
        {
            if (itemId == StateHelper.ApplicationStoreId)
            {
                if (!ErrorHappened)
                {
                    string filePath = Path.Combine(StateHelper.GetDataDirectory(), "patch.cdpipatch");

                    if (File.Exists(filePath))
                    {
                        _ = PipeClient.Instance.SendMessage($"UPDATE:BEGIN({filePath})");
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

        private void ItemInstallingErrorHappens(Tuple<string, string> tuple)
        {
            string operationId = tuple.Item1;
            if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) == StateHelper.ApplicationStoreId)
            {
                ErrorHappened = true;
                ErrorInfo = tuple.Item2;
                ErrorHappens?.Invoke();
            }
        }

        public async Task<bool> CheckForUpdates()
        {
            IsUpdateAvailable = false;
            ErrorHappened = false;
            ErrorInfo = string.Empty;

            Tuple<string, string> _data = await StoreHelper.GetLastVersionAndVersionNotes(StateHelper.ApplicationStoreId);

            if (_data.Item1.StartsWith("ERR_"))
            {
                ErrorHappened = true;
                ErrorInfo = _data.Item1;
                ErrorHappens?.Invoke();
                return false;
            }

            Version serverVersion = new(_data.Item1);
            Version currentVersion = new(StateHelper.Instance.Version);

            if (serverVersion > currentVersion)
            {
                ServerVersion = _data.Item1;
                IsUpdateAvailable = true;
            }

            return IsUpdateAvailable;
        }


    }
}
