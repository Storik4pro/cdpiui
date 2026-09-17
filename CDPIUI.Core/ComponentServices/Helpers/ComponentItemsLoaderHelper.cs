using CDPIUI.Core.Store.Database;

namespace CDPIUI.Core.ComponentServices.Helpers
{
    public class ComponentItemsLoaderHelper
    {

        private List<ComponentHelper> Components = [];

        public Action? InitRequested;

        private static ComponentItemsLoaderHelper? _instance;
        private static readonly object _lock = new();
        public static ComponentItemsLoaderHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new ComponentItemsLoaderHelper();
                    return _instance;
                }
            }
        }

        private ComponentItemsLoaderHelper()
        {
            Init();
        }

        public void Init(bool forse = true)
        {
            if (!forse && Components.Count != 0) return;
            Components.Clear();
            List<DatabaseStoreItem> configItems = 
                DatabaseHelper.Instance.GetItemsByType("component");

            foreach (DatabaseStoreItem item in configItems)
            {
                if (!Path.Exists(item.Directory) || 
                    !File.Exists(Path.Combine(item.Directory, item.Executable + ".exe")))
                    continue;

                ComponentHelper componentHelper = new(item.Id!);
                Components.Add(componentHelper);
            }
            InitRequested?.Invoke();
        }

        public List<ComponentHelper> GetComponentHelpers()
        {
            return Components;
        }

        public ComponentHelper? GetComponentHelperFromId(string id)
        {
            foreach (ComponentHelper componentHelper in Components)
            {
                if (componentHelper.Id == id)
                    return componentHelper;
            }
            return null;
        }
    }
}
