using CDPIUI.Helper.Basic;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core
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

        // Enums

        

        

        // Store

        public const string StoreRepo = "Storik4pro/CDPIUI-Store";
        public const string GitLabStoreRepo = "Storik4/CDPIUI-Store";
        public const string StoreDirName = "Store";
        public const string StoreItemsDirName = "Items";
        public const string StoreRepoDirName = "Repo";
        public const string StoreRepoCache = "Cache";
        public const string StoreLocalDirName = "Local";

        public readonly Dictionary<string, string> FileTypes = new();

        // Local

        public const string SettingsDir = "Settings";

        // Repository

        public const string ApplicationCheckUpdatesUrl = "https://github.com/Storik4pro/cdpiui";
        public const string ApplicationGitLabCheckUpdatesUrl = "https://gitlab.com/Storik4/CDPI-UI";

        // Template

        public const string TemplateDir = "Template";
        public const string TemplateSettingsDir = "Settings";

        // Components

        public Dictionary<string, string> ComponentIdPairs = new();
        public static List<string> GoodCheckSupportedComponents = ["CSZTBN012", "CSGIVS036", "CSBIHA024"];

        public bool isCheckedComponentsUpdateComplete = false;
        public string lastComponentsUpdateError = "";

        

        public string Version;

        private StateHelper()
        {
            FileTypes.Add("archive", ".zip");
            FileTypes.Add("configPack", ".cdpiconfigpack");
            FileTypes.Add("signedZip", ".cdpisignedpack");
            FileTypes.Add("WIN32application", ".exe");
            FileTypes.Add("CDPIUIUpdateItem", ".cdpipatch");
            FileTypes.Add("msi", ".msi");
            FileTypes.Add("UPDmsi", ".msi");
            FileTypes.Add("elmsi", ".exe");
            FileTypes.Add("lst", ".lst");
            FileTypes.Add("raw", ".txt");

            string exePath = Assembly.GetEntryAssembly()?.Location
                 ?? Assembly.GetExecutingAssembly()?.Location
                 ?? Process.GetCurrentProcess().MainModule?.FileName;
            var fvi = FileVersionInfo.GetVersionInfo(exePath);
            Version = fvi.FileVersion;
            string productVersion = fvi.ProductVersion; 
            Console.WriteLine($"FileVersion: {Version}, ProductVersion: {productVersion}");

            ComponentIdPairs.Add("ASGKOI001", "GoodCheck");
            ComponentIdPairs.Add("CSZTBN012", "Zapret");
            ComponentIdPairs.Add("CSGIVS036", "GoodbyeDPI");
            ComponentIdPairs.Add("CSBIHA024", "ByeDPI");
            ComponentIdPairs.Add("CSSIXC048", "SpoofDPI");
            ComponentIdPairs.Add("CSNIG9025", "NoDPI");
            ComponentIdPairs.Add("CSTYFL050", "TgWsProxy");
        }

        public string FindKeyByValue(string value)
        {
            return ComponentIdPairs.FirstOrDefault(kvp => kvp.Value == value).Key;
        }
    }
}
