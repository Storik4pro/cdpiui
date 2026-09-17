using CDPIUI.Core.Data;
using CDPIUI.Shared.Basic;
using System.Xml.Linq;

namespace CDPIUI.Core
{
    public class SettingsManager: XMLSettingsService
    {
        private static SettingsManager? _instance;
        private static readonly object _lock = new object();

        public static SettingsManager Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new SettingsManager();
                    return _instance;
                }
            }
        }
        private SettingsManager() : base(Directories.SettingsFilePath) { }


        protected override T GetDefaultValueForKey<T>(string group, string key)
        {
            string templatePath = Directories.TemplateSettingsFilePath;

            if (!File.Exists(templatePath)) return GetDefaultValue<T>()!;
            try
            {
                XDocument temp_xDocument = XDocument.Load(templatePath);
                return GetValue<T>(group, key, temp_xDocument.Root, raiseExceptionIfNotExits:true)!;
            }
            catch
            {
                return GetDefaultValue<T>()!;
            }
        }

        protected override T GetDefaultValueForKey<T>(IEnumerable<string> groupPath, string key)
        {
            string templatePath = Directories.TemplateSettingsFilePath;

            if (!File.Exists(templatePath)) return GetDefaultValue<T>()!;
            try
            {
                XDocument temp_xDocument = XDocument.Load(templatePath);
                return GetValue<T>(groupPath, key, temp_xDocument.Root, raiseExceptionIfNotExits: true)!;
            }
            catch
            {
                return GetDefaultValue<T>()!;
            }
        }

    }
}
