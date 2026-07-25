namespace CDPIUI.Core.Store.ViewModels
{
    public class Link
    {
        public string? name;
        public string? url;
    }

    public class FileToDownload
    {
        public string? type;
        public string? archive_root_folder;
        public string? actions;
        public string? version_control;
        public string? version_control_link;
        public string? download_link;
        public string? preffered_version;
        public string? preffered_to_download_file_name;
    }


    public class RepoItemModel
    {
        public string? store_id;
        public string? category_id;
        public string? type;
        public string? name;
        public string? short_name;
        public string? developer;
        public string? icon;
        public string? background;
        public string? stars;
        public string? small_description;
        public string? description;
        public bool display_warning;
        public string? warning_text;
        public List<Link>? links;
        public string? version_control;
        public List<FileToDownload>? files_to_download;
        public List<ItemLicenseModel>? license;
        public string? version_control_link;
        public string? download_link;
        public string? filetype;
        public string? preffered_to_download_file_name;
        public string? archive_root_folder;
        public string? target_executable_file;
        public string? before_install_actions;
        public string? after_install_actions;
        public List<string[]>? dependencies;
        public string? target_minversion;
        public string? target_maxversion;
    }
}