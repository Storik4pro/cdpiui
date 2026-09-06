namespace CDPIUI.Core.Store.Queue
{
    internal class QueueManagerService : IQueueManagerService
    {
        private readonly Queue<QueueItemModel> Queue = new();
        private readonly object _queueLock = new();

        public event Action? QueueUpdated;
        public event Action? CurrentItemRemovedFromQueue;
        public event Action<QueueItemModel>? ProcessItem;

        public QueueItemModel? CurrentDownloadingItem { get; set; }

        public void AddItemToQueue(string itemId, string? version = null, bool cleanDirectoryBeforeInstalling = false, string? packFile = null) // PUBLIC
        {
            RemoveItemFromDownloadFailureList(itemId);
            if (GetOperationIdFromItemId(itemId) != null) return;

            var opId = Guid.NewGuid().ToString();
            var qi = new QueueItemModel(itemId, opId, version, cleanDirectoryBeforeInstalling, packFilePath: packFile);

            lock (_queueLock)
            {
                Queue.Enqueue(qi);
                TryProcessNext();
            }
            QueueUpdated?.Invoke();
        }

        public bool RemoveItemFromQueue(string itemId)
        {
            var items = Queue.ToList();
            var removed = items.RemoveAll(i => i.ItemId == itemId) > 0;
            if (removed)
            {
                Queue.Clear();
                foreach (var i in items) Queue.Enqueue(i);
                QueueUpdated?.Invoke();
            }

            var currentItem = CurrentDownloadingItem;
            if (currentItem != null && currentItem.ItemId == itemId)
            {
                if (currentItem.Status != "CANC")
                {
                    // A cancellation callback can finish this item and advance the queue.
                    currentItem.Status = "CANC";
                    currentItem.DownloadStage = "CANC";
                    CurrentItemRemovedFromQueue?.Invoke();
                    QueueUpdated?.Invoke();
                }
                return true;
            }
            return removed;
        }

        public Queue<QueueItemModel> GetQueue() 
        {
            return Queue;
        }

        public string GetCurrentQueueOperationId() 
        {
            return CurrentDownloadingItem != null ? CurrentDownloadingItem.OperationId : string.Empty;
        }

        public string? GetOperationIdFromItemId(string storeId) 
        {
            if (CurrentDownloadingItem != null && CurrentDownloadingItem.ItemId == storeId)
            {
                return CurrentDownloadingItem.OperationId;
            }
            foreach (var item in Queue)
            {
                if (item.ItemId == storeId)
                    return item.OperationId;
            }
            return null;
        }

        public string? GetItemIdFromOperationId(string operationId)
        {
            if (CurrentDownloadingItem != null && CurrentDownloadingItem.OperationId == operationId)
                return CurrentDownloadingItem.ItemId;

            foreach (var item in Queue)
            {
                if (item.OperationId == operationId)
                    return item.ItemId;
            }
            return null;
        }

        public QueueItemModel? GetQueueItemFromOperationId(string operationId)
        {
            if (CurrentDownloadingItem != null && CurrentDownloadingItem.OperationId == operationId)
                return CurrentDownloadingItem;

            foreach (var item in Queue)
            {
                if (item.OperationId == operationId)
                    return item;
            }
            return null;
        }

        public void TryProcessNext()
        {
            QueueUpdated?.Invoke();
            if (Queue.Count == 0)
            {
                return;
            }

            if (CurrentDownloadingItem == null)
            {

                var next = Queue.Dequeue();
                CurrentDownloadingItem = next;

                ProcessItem?.Invoke(next);
                QueueUpdated?.Invoke();
            }
        }


        private readonly List<QueueItemModel> FailedToInstallItems = new();
        private readonly object _failedToInstallItemsLock = new();

        public event Action? ErrorListUpdated;

        public void RemoveItemFromDownloadFailureList(string itemId)
        {
            lock (_failedToInstallItemsLock)
            {
                var extstItem = FailedToInstallItems.FirstOrDefault(x => x.ItemId == itemId);
                if (extstItem != null)
                {
                    FailedToInstallItems.Remove(extstItem);
                    ErrorListUpdated?.Invoke();
                }
            }
        }
        public List<QueueItemModel> GetFailedToInstallItems() 
        {
            return FailedToInstallItems;
        }

        public void AddItemToDownloadFailureList(string itemId, string operationId, string? version, string errorCode)
        {
            lock (_failedToInstallItemsLock)
            {
                var extstItem = FailedToInstallItems.FirstOrDefault(x => x.ItemId == itemId);
                if (extstItem != null) FailedToInstallItems.Remove(extstItem);
                FailedToInstallItems.Add(new(itemId, operationId, version, false)
                {
                    ErrorCode = errorCode
                });
            }
            ErrorListUpdated?.Invoke();
        }
    }
}
