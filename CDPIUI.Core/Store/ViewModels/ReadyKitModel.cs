namespace CDPIUI.Core.Store.ViewModels
{
    public class ReadyKitModel
    {
        public string? store_id;
        public string? name;
        public string? short_name;
        public string? icon;
        public string? background;
        public string? small_description;
        public string? description;
        public bool recomended;
        public bool recommended;
        public List<string>? items;

        public bool IsRecommended => recomended || recommended;
    }
}
