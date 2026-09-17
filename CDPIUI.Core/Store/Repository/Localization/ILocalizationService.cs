namespace CDPIUI.Core.Store.Repository.Localization
{
    public interface ILocalizationService
    {
        /// <summary>
        /// Get localized store string from key
        /// </summary>
        /// <param name="name">Locale key</param>
        /// <param name="langCode">Language code</param>
        /// <returns>Localized string. If error happens return "slocale:<paramref name="name"/>"</returns>
        string GetLocalizedStoreItemName(string name, string langCode);
    }
}
