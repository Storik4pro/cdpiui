namespace CDPIUI.Core.Store.Queue
{
    public class QueueItemModel(string itemId, string operationId, string? version = null, bool cleanDirectoryBeforeInstalling = false, string? packFilePath = null)
    {
        public string OperationId { get; } = operationId;
        public string ItemId { get; } = itemId;
        public string Version { get; } = version ?? string.Empty;
        public bool CleanDirectoryBeforeInstalling { get; } = cleanDirectoryBeforeInstalling;
        public string? PackFilePath { get; } = packFilePath ?? string.Empty;
        public string Status { get; set; } = "WAIT";
        public string DownloadStage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
    }
}