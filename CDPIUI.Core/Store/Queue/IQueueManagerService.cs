
namespace CDPIUI.Core.Store.Queue
{
    internal interface IQueueManagerService
    {
        /// <summary>
        /// Queue was updated.
        /// </summary>
        event Action? QueueUpdated;

        /// <summary>
        /// Add item to download queue
        /// </summary>
        /// <param name="itemId">Item id</param>
        /// <param name="version">Item version (null if latest)</param>
        /// <param name="cleanDirectoryBeforeInstalling">Clean files after instaliing (if msi-like)</param>
        /// <param name="packFile">Pack file (null if online download</param>
        void AddItemToQueue(string itemId, string? version = null, bool cleanDirectoryBeforeInstalling = false, string? packFile = null);

        /// <summary>
        /// Cancel operation for item id
        /// </summary>
        /// <param name="itemId">item id</param>
        /// <returns>true if item will be removed, otherwise false</returns>
        bool RemoveItemFromQueue(string itemId);

        /// <summary>
        /// Get current operation id
        /// </summary>
        /// <returns>Operation id. null if no operation runned</returns>
        string GetCurrentQueueOperationId();

        /// <summary>
        /// Get item id from operation id
        /// </summary>
        /// <param name="operationId">Operation id</param>
        /// <returns>Item id if exist, otherwise null</returns>
        string? GetItemIdFromOperationId(string operationId);

        /// <summary>
        /// Get operation id from item id
        /// </summary>
        /// <param name="storeId">Item id</param>
        /// <returns>Operation id if exist, otherwise null</returns>
        string? GetOperationIdFromItemId(string storeId);

        /// <summary>
        /// Gets all queue
        /// </summary>
        /// <returns>List of items in queue</returns>
        Queue<QueueItemModel> GetQueue();

        /// <summary>
        /// Get QueueItem from operation id
        /// </summary>
        /// <param name="operationId">Operation id</param>
        /// <returns>Model if exist, otherwise null</returns>
        QueueItemModel? GetQueueItemFromOperationId(string operationId);

        /// <summary>
        /// Get all failure items
        /// </summary>
        /// <returns>List of faulure items</returns>
        List<QueueItemModel> GetFailedToInstallItems();

        /// <summary>
        /// Remove item from failure list
        /// </summary>
        /// <param name="itemId">item id</param>
        void RemoveItemFromDownloadFailureList(string itemId);
        
    }
}