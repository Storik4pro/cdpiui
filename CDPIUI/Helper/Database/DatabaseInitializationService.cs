using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.Basic;
using CDPIUI.Shared;
using System.Collections.Generic;

namespace CDPIUI.Helper.Database
{
    public class DatabaseInitializationService
    {
        /// <summary>
        /// Try to restore application database. Raises critical exception on failure
        /// </summary>
        public static void QuickRestore()
        {
            RegisterCustomUserItem();

            var result = DatabaseHelper.Instance.RestorePaths();

            if (!result.Success)
            {
                foreach (var item  in result.Result)
                {
                    TryManualRestore(item);
                }
            }
        }

        private static void TryManualRestore(DatabaseStoreItem item)
        {
            if (item.Id == SharedConstants.LocalUserItemsId || item.Id == SharedConstants.ApplicationStoreId)
            {
                RegisterCustomUserItem(manual: true);
            }
            else
            {
                Logger.Instance.CreateWarningLog(
                    nameof(DatabaseHelper), 
                    $"Item {item.Id} is damaged and cannot be restore. Please, reinstall it"
                    );
            }
        }

        private static void RegisterCustomUserItem(bool manual = false)
        {
            Dictionary<string, string> localLoc = new()
            {
                { "EN", $"{SharedConstants.LocalUserItemLocFolder}/strings.json" }
            };

            var result = DatabaseHelper.Instance.RegisterUserCustomItem(
                ApplicationInfo.Version, DefaultConfigItem(localLoc), manual);

            if (!result.Success && result.ErrorHappens)
            {
                ErrorShowingHelper.RaiseCriticalException(nameof(DatabaseHelper), result.Error);
            }

        }

        private static ConfigInitItem DefaultConfigItem(Dictionary<string, string> localeDirs) => new()
        {
            toggleListAvailable = [],
            localized_strings_directory = localeDirs
        };
    }
}
