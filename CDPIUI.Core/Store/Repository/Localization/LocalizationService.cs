using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;

namespace CDPIUI.Core.Store.Repository.Localization
{
    public class StoreLocalizationHelper()
    {
        /// <summary>
        /// Get store-like localization key from default localization key
        /// </summary>
        /// <returns>Store-like localization key</returns>
        public static string GetStoreLikeLocale()
        {
            return ApplicationInfo.Instance.Localization switch
            {
                "ru" => "RU",
                "en-US" => "EN",
                _ => "EN",
            };
        }
    }

    internal class LocalizationService : ILocalizationService
    {
        public string? LocalizationName;
        public Dictionary<string, string>? LocalizationDict;

        public Dictionary<string, string> StoreLocalizationPaths = [];

        public string GetLocalizedStoreItemName(string name, string langCode)
        {
            string localizedString = $"slocale:{name}";

            string localRepoFolder = Directories.StoreRepoCacheDirectory;

            if (name.Contains(' '))
                return name;

            try
            {
                if (LocalizationName != langCode)
                {
                    string locFilePath;
                    if (!StoreLocalizationPaths.TryGetValue(langCode, out string value))
                    {
                        locFilePath = Path.Combine(localRepoFolder, StoreLocalizationPaths["EN"]);
                    }
                    else
                    {
                        locFilePath = Path.Combine(localRepoFolder, value);
                    }

                    using (StreamReader r = new StreamReader(locFilePath))
                    {
                        string json = r.ReadToEnd();
                        Dictionary<string, string> localizationDict = JSONConvertor.DeserializeObject<Dictionary<string, string>>(json);

                        LocalizationName = langCode;
                        LocalizationDict = localizationDict;
                    }

                }
                localizedString = LocalizationDict[name];

            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(LocalizationService), $"Cannot get localization for {name} [{langCode}]");
            }

            return localizedString;
        }
    }
}
