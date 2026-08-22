using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using System;
using System.IO;
using WinUI3Localizer;

namespace CDPIUI.Helper.BlockCheck2;

public static class BlockCheck2HistoryStoreItemService
{
    public const string SettingsGroup = "BLOCKCHECK";
    public const string RegistrationCompletedSettingsKey = "historyStoreItemRegistrationCompleted";

    private const string StoreItemType = "CDPIUIUpdateItem";
    private static readonly string DisplayName = Localizer.Get().GetLocalizedString("RecentBlockCheck2Selections");
    private static readonly object SyncRoot = new();

    public static string StorageDirectory => Path.Combine(
        Directories.StoreItemsDirectory,
        SharedConstants.BlockCheck2HistoryStoreItemId);

    public static void RegisterOnFirstLaunch(string? applicationVersion = null)
    {
        try
        {
            if (SettingsManager.Instance.GetValueOrDefault(
                    SettingsGroup,
                    RegistrationCompletedSettingsKey,
                    defaultValue: false))
            {
                return;
            }

            EnsureRegistered(applicationVersion);
            SettingsManager.Instance.SetValue(
                SettingsGroup,
                RegistrationCompletedSettingsKey,
                true);
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(
                nameof(BlockCheck2HistoryStoreItemService),
                $"Cannot register BlockCheck2 report history in Store: {exception}");
        }
    }

    public static void EnsureRegistered(string? applicationVersion = null)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(StorageDirectory);

            string currentApplicationVersion = applicationVersion ??
                DatabaseHelper.Instance.GetItemById(SharedConstants.ApplicationStoreId)?.CurrentVersion ??
                ApplicationInfo.Version;
            DatabaseStoreItem? current = DatabaseHelper.Instance.GetItemById(
                SharedConstants.BlockCheck2HistoryStoreItemId);

            if (current != null &&
                string.Equals(current.Directory, StorageDirectory, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(current.Type, StoreItemType, StringComparison.Ordinal) &&
                string.Equals(current.VersionControlType, "local", StringComparison.Ordinal) &&
                string.Equals(current.Name, DisplayName, StringComparison.Ordinal) &&
                string.Equals(current.ShortName, DisplayName, StringComparison.Ordinal) &&
                string.Equals(current.CurrentVersion, currentApplicationVersion, StringComparison.Ordinal))
            {
                return;
            }

            DatabaseStoreItem historyItem = new()
            {
                Id = SharedConstants.BlockCheck2HistoryStoreItemId,
                Type = StoreItemType,
                Directory = StorageDirectory,
                Executable = null,
                UpdateCheckUrl = null,
                DownloadUrl = null,
                DownloadFileType = null,
                VersionControlType = "local",
                CurrentVersion = currentApplicationVersion,
                RequiredItemIds = null,
                DependentItemIds = null,
                IconPath = "$STATICIMAGE(Store/empty.png)",
                Name = DisplayName,
                ShortName = DisplayName,
                Developer = "CDPIUI",
                BackgroudColor = string.Empty,
            };

            if (!DatabaseHelper.Instance.AddOrUpdateItem(historyItem))
            {
                throw new InvalidOperationException(
                    $"Cannot register the '{SharedConstants.BlockCheck2HistoryStoreItemId}' Store item.");
            }
        }
    }
}
