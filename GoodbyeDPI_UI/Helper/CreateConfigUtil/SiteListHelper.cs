using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper.CreateConfigUtil
{
    public class SiteListElement
    {
        public string Name { get; set; }
        public string PackName { get; set; }
        public string PackId { get; set; }
        public string Directory {  get; set; }
        public string Hash { get; set; }
    }
    public class SiteListHelper
    {
        private static SiteListHelper _instance;
        private static readonly object _lock = new object();

        public static SiteListHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new SiteListHelper();
                    return _instance;
                }
            }
        }

        private SiteListHelper() 
        {

        }

        public async Task<List<SiteListElement>> GetAllAvailableSiteListTemplatesAsync()
        {
            List<SiteListElement> templates = new List<SiteListElement>();

            List<DatabaseStoreItem> configLists = DatabaseHelper.Instance.GetItemsByType("configlist");

            var namePackIdDict = new Dictionary<(string Name, string PackId), List<SiteListElement>>();

            foreach (DatabaseStoreItem config in configLists)
            {
                string packId = config.Id;
                string packName = config.ShortName;
                string directory = config.Directory;

                List<string> filePaths = await DomainValidationHelper.GetSupportedTxtFiles(directory, DomainValidationHelper.CheckMode.Quick);

                foreach (string filePath in filePaths)
                {
                    string name = Path.GetFileName(filePath);
                    string hash = ComputeFileHash(filePath);

                    var element = new SiteListElement
                    {
                        Name = name,
                        PackName = packName,
                        PackId = packId,
                        Directory = filePath,
                        Hash = hash
                    };

                    templates.Add(element);

                    var key = (name, packId);
                    if (!namePackIdDict.TryGetValue(key, out var list))
                    {
                        list = new List<SiteListElement>();
                        namePackIdDict[key] = list;
                    }
                    list.Add(element);
                }
            }

            var uniqueTemplates = new List<SiteListElement>();
            foreach (var pair in namePackIdDict)
            {
                var list = pair.Value;
                if (list.Count > 1)
                {
                    
                    var hashGroups = list.GroupBy(x => x.Hash).ToList();
                    foreach (var group in hashGroups)
                    {
                        uniqueTemplates.Add(group.First());
                    }
                    continue;
                }
                uniqueTemplates.AddRange(list);
            }

            return uniqueTemplates;
        }

        private static string ComputeFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
