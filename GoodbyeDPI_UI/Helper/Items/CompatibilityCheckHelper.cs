using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper.Items
{
    public class CompatibilityCheckHelper
    {
        private static CompatibilityCheckHelper _instance;
        private static readonly object _lock = new();
        public static CompatibilityCheckHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new CompatibilityCheckHelper();
                    return _instance;
                }
            }
        }
        public CompatibilityCheckHelper() { }

        public bool isCheckActive = false;
        public async Task BeginCheck()
        {
            if (isCheckActive) return;
            isCheckActive = true;
            try
            {
                List<string> outdatedComponents = [];
                var components = ComponentItemsLoaderHelper.Instance.GetComponentHelpers();

                foreach (var component in components)
                {
                    ConfigHelper configHelper = component.GetConfigHelper();

                    foreach (var config in configHelper.GetConfigItems())
                    {
                        if (config.target != null && config.target.Count == 2)
                        {
                            var databaseItem = DatabaseHelper.Instance.GetItemById(config.target[0]);

                            string curV = databaseItem.CurrentVersion;
                            if (databaseItem.CurrentVersion.Split(".").Length <= 3)
                            {
                                curV += ".0";
                            }
                            string serV = config.target[1];
                            if (config.target[1].Split(".").Length <= 3)
                            {
                                serV += ".0";
                            }

                            Version requiredVersion = new Version(config.target[1].Replace("v", ""));
                            Version installedVersion = new Version(databaseItem.CurrentVersion.Replace("v", ""));

                            if (requiredVersion > installedVersion)
                            {
                                if (!outdatedComponents.Contains(config.target[0]))
                                {
                                    outdatedComponents.Add(config.target[0]);
                                }
                            }
                        }
                    }
                }

                foreach (var component in outdatedComponents)
                {
                    await PipeClient.Instance.SendMessage($"NOTIFY:CCA({StateHelper.Instance.ComponentIdPairs.FirstOrDefault(x => x.Key == component).Value})");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(CompatibilityCheckHelper), $"Cannot begin check: {ex.Message}");
            }
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Contains("--exit-after-action")) Process.GetCurrentProcess().Kill(); // FIX: Possible issue when process take too more time

            isCheckActive = false;
        }
    }

}
