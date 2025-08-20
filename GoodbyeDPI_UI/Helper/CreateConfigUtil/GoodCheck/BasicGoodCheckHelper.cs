using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoodbyeDPI_UI.Helper.CreateConfigUtil.GoodCheck
{
    public class GoodCheckStrategiesList
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
    }
    public class BasicGoodCheckHelper
    {
        
        public BasicGoodCheckHelper() 
        { 
        
        }

        public static List<GoodCheckStrategiesList> GetAvailableStrategiesLists(string componentId)
        {
            List<GoodCheckStrategiesList> strategiesLists = [];

            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
            string targetFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreItemsDirName, "ASGKOI001", "StrategiesGoGo", StateHelper.Instance.ComponentIdPairs[componentId]);

            if (!Directory.Exists(targetFolder))
                return [];

            try
            {
                var files = Directory.EnumerateFiles(targetFolder, "*.txt", SearchOption.AllDirectories);

                foreach (var file in files) 
                {
                    GoodCheckStrategiesList strategiesList = new() 
                    {
                        Name = Path.GetFileName(file).Replace(".txt", ""),
                        FilePath = file,
                    };
                    strategiesLists.Add(strategiesList);
                }
            }
            catch 
            {
                // pass
            }

            return strategiesLists;
        }
    }
}
