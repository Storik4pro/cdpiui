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
                            Semver.SemVersion requiredVersion = Semver.SemVersion.Parse(config.target[1].Replace("v", ""));
                            Semver.SemVersion installedVersion = Semver.SemVersion.Parse(databaseItem.CurrentVersion.Replace("v", ""));

                            if (Semver.SemVersion.ComparePrecedence(requiredVersion, installedVersion) == 1)
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
