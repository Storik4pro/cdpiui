namespace CDPIUI.Core.Store.Network.Models
{
    internal class DownloadLinkModel : ILinkModel
    {
        public string? link { get; set; }
        public string? version { get; set; }
        public string? type { get; set; }
        public string? archive_root_folder { get; set; }
        public string? actions {  get; set; }
        public string? target_executable_file { get; set; }
        
    }
}
