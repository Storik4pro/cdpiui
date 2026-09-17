using CDPIUI.Core.JSON;
using System.Diagnostics;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers
{
    internal class LocaleModel
    {
        public string? LocaleName;
        public Dictionary<string, string>? keyValuePairs;
    }

    internal static class ConfigLocalizationHelper
    {

        public static string GetLocalizedConfigNameString(
            string name, 
            string langCode, 
            string directory, 
            Dictionary<string, string> locPaths, 
            LocaleModel localeHelper)
        {
            string localizedString = $"clocale:{name}";

            try
            {
                if (localeHelper.LocaleName != langCode)
                {
                    
                        InitLocaleHelper(localeHelper, locPaths, directory, langCode);
                }
                localizedString = localeHelper.keyValuePairs[name];

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cannot get locale {name}, error is {ex}");
            }


            return localizedString;
        }

        public static void InitLocaleHelper(
            LocaleModel localeHelper, 
            Dictionary<string, string> locPaths, 
            string directory, 
            string langCode)
        {
            try
            {
                string locFilePath;
                if (!locPaths.ContainsKey(langCode))
                {
                    locFilePath = Path.Combine(directory, locPaths["EN"]);
                }
                else
                {
                    locFilePath = Path.Combine(directory, locPaths[langCode]);
                }
                using (StreamReader r = new StreamReader(locFilePath))
                {
                    string json = r.ReadToEnd();
                    Dictionary<string, string> localizationDict = 
                        JSONConvertor.DeserializeObject<Dictionary<string, string>>(json);

                    localeHelper.LocaleName = langCode;
                    localeHelper.keyValuePairs = localizationDict;
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cannot init locale, error is {ex}");
            }
        }
    }
}