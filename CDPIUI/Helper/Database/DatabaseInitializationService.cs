using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.Basic;
using CDPIUI.Shared;
using System;
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
            RenameInstalledZapret();

            if (DatabaseHelper.Instance.IsItemInstalled(
                    HardcodedItemIds.ComponentIds[Components.Zapret2]) ||
                DatabaseHelper.Instance.IsItemInstalled(SharedConstants.ConvertedZapretStoreItemId))
            {
                try
                {
                    Zapret2LegacyConfigService.EnsureStorageRegistered(ApplicationInfo.Version);
                }
                catch (Exception ex)
                {
                    Logger.Instance.CreateWarningLog(
                        nameof(DatabaseInitializationService),
                        $"Cannot initialize the converted Zapret config storage: {ex}");
                }
            }

            var result = DatabaseHelper.Instance.RestorePaths();

            if (!result.Success)
            {
                foreach (var item  in result.Result)
                {
                    TryManualRestore(item);
                }
            }
        }

        private static void RenameInstalledZapret()
        {
            const string legacyDisplayName = "Zapret Legacy";

            if (SettingsManager.Instance.GetValueOrDefault(
                "SYSTEM",
                "zapretRenamed",
                defaultValue: false))
            {
                return;
            }

            try
            {
                string zapretId = HardcodedItemIds.ComponentIds[Components.Zapret];
                var zapretItem = DatabaseHelper.Instance.GetItemById(zapretId);
                if (zapretItem == null)
                {
                    return;
                }

                zapretItem.ShortName = legacyDisplayName;

                DatabaseHelper.Instance.AddOrUpdateItem(zapretItem);

                SettingsManager.Instance.SetValue("SYSTEM", "zapretRenamed", true);
            }
            catch { }
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
