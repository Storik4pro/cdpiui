using GoodbyeDPI_UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GoodbyeDPI_UI.Helper
{
    internal class StateHelper
    {
        private static StateHelper _instance;
        private static readonly object _lock = new object();

        public static StateHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new StateHelper();
                    return _instance;
                }
            }
        }

        public string workDirectory = Directory.GetCurrentDirectory();

        // Store

        public const string StoreRepo = "Storik4pro/CDPIUI-Store";
        public const string StoreDirName = "Store";
        public const string StoreItemsDirName = "Items";
        public const string StoreRepoDirName = "Repo";
        public const string StoreRepoCache = "Cache";
        public const string StoreLocalDirName = "Local";

        public readonly Dictionary<string, string> FileTypes = new();

        // Local

        public const string LocalUserItemsId = "LocalUserStorage";
        public const string LocalUserItemSiteListsFolder = "List";
        public const string LocalUserItemBinsFolder = "Bin";
        public const string LocalUserItemLocFolder = "Loc";

        // Components

        public Dictionary<string, string> ComponentIdPairs = new();

        public bool isCheckedComponentsUpdateComplete = false;
        public string lastComponentsUpdateError = "";

        public string Version;

        private StateHelper()
        {
            FileTypes.Add("archive", ".zip");

            string exePath = Assembly.GetEntryAssembly()?.Location
                 ?? Assembly.GetExecutingAssembly()?.Location
                 ?? Process.GetCurrentProcess().MainModule?.FileName;
            var fvi = FileVersionInfo.GetVersionInfo(exePath);
            Version = fvi.FileVersion;
            string productVersion = fvi.ProductVersion; 
            Console.WriteLine($"FileVersion: {Version}, ProductVersion: {productVersion}");

            ComponentIdPairs.Add("ASGKOI001", "GoodCheck");
            ComponentIdPairs.Add("CSZTBN012", "Zapret");
        }

        public string FindKeyByValue(string value)
        {
            return ComponentIdPairs.FirstOrDefault(kvp => kvp.Value == value).Key;
        }
    }
}
