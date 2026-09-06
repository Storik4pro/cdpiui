using CDPIUI.Core.Store.ViewModels;

namespace CDPIUI.Core.Store.Repository
{
    internal interface IRepositoryLoaderService
    {
        /// <summary>
        /// Current version control.
        /// *Version control updates ONLY when application startup.
        /// </summary>
        SupportedVersionControls VersionControl { get; }
        /// <summary>
        /// Loaded store database
        /// </summary>
        List<RepoCategoryModel>? FormattedStoreDatabase { get; }
        /// <summary>
        /// Store localization dict
        /// </summary>
        Dictionary<string, string>? StoreLocalizationPaths { get; }
        /// <summary>
        /// List of all items available in Store
        /// </summary>
        List<RepoItemModel> ItemsList { get; }

        /// <summary>
        /// Ready-to-use kits available in Store.
        /// </summary>
        List<ReadyKitModel> ReadyKits { get; }

        /// <summary>
        /// Is version control available
        /// </summary>
        /// <param name="versionControl">Version control</param>
        /// <returns>true of version control available, otherwise false</returns>
        static abstract Task<bool> TryLoadDatabaseForVersionControl(SupportedVersionControls versionControl);

        /// <summary>
        /// Load all store database
        /// </summary>
        /// <param name="forseSync">Forse update</param>
        /// <param name="versionControl">Custom version control</param>
        /// <returns>true, if operation successfull, otherwise false</returns>
        Task<bool> LoadAllStoreDatabase(bool forseSync, SupportedVersionControls versionControl);

        /// <summary>
        /// Get item from item id
        /// </summary>
        /// <param name="storeId">Item id</param>
        /// <returns>Item model if exist, otherwise null</returns>
        RepoItemModel? GetItemInfoFromStoreId(string storeId);

        /// <summary>
        /// Gets a ready-to-use kit by its Store ID.
        /// </summary>
        ReadyKitModel? GetReadyKitFromStoreId(string storeId);

        /// <summary>
        /// Gets category from category id
        /// </summary>
        /// <param name="storeId">Category id</param>
        /// <returns>Category model if exist, otherwise null</returns>
        RepoCategoryModel? GetCategoryFromStoreId(string storeId);

        /// <summary>
        /// Clear repo cache
        /// </summary>
        static abstract void ClearRepoCache();
    }
}
