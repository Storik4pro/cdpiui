using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper.Items
{
    public class ComponentItemsLoaderHelper
    {

        private List<ComponentHelper> Components = new List<ComponentHelper>();

        private static ComponentItemsLoaderHelper _instance;
        private static readonly object _lock = new();
        public static ComponentItemsLoaderHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ComponentItemsLoaderHelper();
                    return _instance;
                }
            }
        }

        private ComponentItemsLoaderHelper() 
        {
            Init();
        }

        public void Init()
        {
            Components.Clear();
            List<DatabaseStoreItem> configItems = DatabaseHelper.Instance.GetItemsByType("component");

            foreach (DatabaseStoreItem item in configItems)
            {
                if (!Path.Exists(item.Directory) || !File.Exists(Path.Combine(item.Directory, item.Executable + ".exe")))
                    continue;

                ComponentHelper componentHelper = new(item.Id);
                Components.Add(componentHelper);
            }
        }

        public List<ComponentHelper> GetComponentHelpers()
        {
            return Components;
        }

        public ComponentHelper GetComponentHelperFromId(string id)
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
