using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using GoodbyeDPI_UI.Helper;
using Newtonsoft.Json;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GoodbyeDPI_UI.ViewModels
{
    internal class ComponentSettings
    {
        public readonly string name;
        public string currentVersion;
        public string serverVersion;

        public Action<string> ErrorHappens;

        private static readonly Dictionary<string, string> COMPONENTS_URLS = new Dictionary<string, string>
        {
            { "zapret", "bol-van/zapret" },
            { "goodbyedpi", "ValdikSS/GoodbyeDPI" },
            { "spoofdpi", "xvzc/SpoofDPI" },
            { "byedpi", "hufrea/byedpi" }
        };

        private static readonly Dictionary<string, string> COMPONENTS_DIRS = new Dictionary<string, string>
        {
            { "zapret", "data/zapret" },
            { "goodbyedpi", "data/goodbyeDPI" },
            { "spoofdpi", "data/spoofdpi" },
            { "byedpi", "data/byedpi" }
        };

        public ComponentSettings(string componentName)
        {
            name = componentName;
            currentVersion = SettingsManager.Instance.GetValue<string>("COMPONENTS", $"{componentName}Version");
            serverVersion = SettingsManager.Instance.GetValue<string>("COMPONENTS", $"{componentName}ServerVersion");
        }

        public async Task<string> GetComponentDownloadUrl()
        {
            if (!COMPONENTS_URLS.TryGetValue(name, out string componentAddress))
            {
                return "ERR_INVALID_COMPONENT_NAME";
            }

            string componentUrl = $"https://api.github.com/repos/{componentAddress}/releases";

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "GoodbyeDPI_UI");

                    var response = await client.GetAsync(componentUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var releases = JsonConvert.DeserializeObject<List<Release>>(content);
                        if (releases != null && releases.Count > 0)
                        {
                            var latestRelease = releases[0];
                            string version = latestRelease.tag_name;
                            if (name == "zapret")
                            {
                                return $"https://github.com/bol-van/zapret-win-bundle/archive/refs/heads/master.zip|{version}";
                            }

                            if (name == "goodbyedpi" && version == "0.2.3rc3")
                            {
                                return $"ERR_LATEST_VERSION_ALREADY_INSTALLED|{version}";
                            }

                            string preDownloadUrl = $"https://api.github.com/repos/{componentAddress}/releases/tags/{version}";

                            string downloadUrl = null;

                            var _response = await client.GetAsync(preDownloadUrl);
                            var dataContent = await _response.Content.ReadAsStringAsync();
                            var data = JsonConvert.DeserializeObject<Release>(dataContent);

                            foreach (var asset in data.assets)
                            {
                                if (asset.name.EndsWith(".zip"))
                                {
                                    if (name == "byedpi")
                                    {
                                        if (asset.name.Contains("x86_64-w64"))
                                        {
                                            downloadUrl = asset.browser_download_url;
                                            break;
                                        }
                                        continue;
                                    }
                                    else
                                    {
                                        downloadUrl = asset.browser_download_url;
                                        break;
                                    }
                                }
                                else if (asset.name.EndsWith(".exe"))
                                {
                                    downloadUrl = asset.browser_download_url;
                                    break;
                                }
                            }

                            if (downloadUrl == null)
                            {
                                return "ERR_INVALID_URL";
                            }

                            return downloadUrl + "|" + version;
                        }
                        else
                        {
                            return "ERR_CANNOT_FIND_RELEASE";
                        }
                    }
                    else
                    {
                        return $"ERR_SERVER_STATUS_CODE_{(int)response.StatusCode}";
                    }
                }
            }
            catch (Exception ex)
            {
                return "ERR_UNKNOWN";
            }
        }

        public async Task OpenFolder()
        {
            if (!COMPONENTS_URLS.TryGetValue(name, out string componentDir))
            {
                return;
            }
            Process.Start(@$"{StateHelper.Instance.workDirectory}/{componentDir}");
            await Task.CompletedTask;
        }

        public async Task<bool> IsUpdateAvailable()
        {
            string result = await GetComponentDownloadUrl();
            if (result.Split("|").Length <= 1 || result.Split("|")[1] == currentVersion)
            {
                if (result.Contains("ERR"))
                {
                    ErrorHappens.Invoke(result.Split("|")[0]);
                }
                return false;
            } 
            else 
            { 
                serverVersion = result.Split("|")[1];
                Debug.WriteLine(serverVersion, name);
                SettingsManager.Instance.SetValue("COMPONENTS", $"{name}ServerVersion", serverVersion);
                StateHelper.Instance.lastComponentsUpdateError = "";
                return true;

            }
        }
    }

    
    public class Release
    {
        [JsonProperty("tag_name")]
        public string tag_name { get; set; }

        [JsonProperty("assets")]
        public List<Asset> assets { get; set; }
    }

    public class Asset
    {
        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("browser_download_url")]
        public string browser_download_url { get; set; }
    }
}
