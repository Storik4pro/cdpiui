namespace CDPIUI.Core.Store.Database
{
    public class DatabaseStoreItem
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? Directory { get; set; }
        public string? Executable { get; set; }
        public string? DownloadFileType { get; set; }
        public string? UpdateCheckUrl { get; set; }
        public string? DownloadUrl { get; set; }
        public string? VersionControlType { get; set; }
        public string? CurrentVersion { get; set; }
        public List<Tuple<string, string>>? RequiredItemIds { get; set; }
        public List<Tuple<string, string>>? DependentItemIds { get; set; }
        public string? IconPath { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public string? Developer {  get; set; }
        public string? BackgroudColor { get; set; }
    }
}
