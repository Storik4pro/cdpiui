using CDPI_UI.Helper.Static;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPI_UI.Helper
{
    public class HelpChapterItem
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string IconGlyph { get; set; }
        public string Path { get; set; }
        public List<HelpItem> Items { get; set; }
    }
    public class HelpItem
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Path { get; set; }
    }
    public class HelpParserHelper
    {
        private static Dictionary<string, string> HelpIconGlyphsPairs = new()
        {
            { "WelcomeToHelp", "\uE734" },
            { "GettingStarted", "\uEB9D" },
            { "TroubleshootingComponentExceptions", "\uEBE8" }
        };

        private static string GetGlyphForId(string id)
        {
            return HelpIconGlyphsPairs.FirstOrDefault((x) => x.Key == id, new(key:id, value:string.Empty)).Value;
        }

        private static string GetLocalizedNameFromId(string id)
        {
            ILocalizer localizer = Localizer.Get();
            string locString = localizer.GetLocalizedString($"/Help/{id}");
            return string.IsNullOrEmpty(locString) ? id : locString;
        }

        public static List<HelpChapterItem> GetHelpItemsForLanguage(string language)
        {
            List <HelpChapterItem> helpItems = [];
            string path =  Path.Combine(StateHelper.GetDataDirectory(), "Help", language);

            if (!Directory.Exists(path))
            {
                return null;
            }

            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                string id = Path.GetFileNameWithoutExtension(file);
                helpItems.Add(new HelpChapterItem
                {
                    Id = id,
                    DisplayName = GetLocalizedNameFromId(id),
                    IconGlyph = GetGlyphForId(id),
                    Path = file,
                    Items = [],
                });
            }
            string[] folders = Directory.GetDirectories(path);

            foreach (string folder in folders)
            {
                string id = Utils.GetFolderNamesUpTo(folder, language);

                List<HelpItem> helpSubItems = [];

                string[] subFiles = Directory.GetFiles(folder);
                foreach (string subFile in subFiles)
                {
                    string _id = Path.GetFileNameWithoutExtension(subFile);
                    helpSubItems.Add(new HelpItem
                    {
                        Id = _id,
                        DisplayName = GetLocalizedNameFromId(_id),
                        Path = subFile,
                    });
                }

                helpItems.Add(new HelpChapterItem
                {
                    Id = id,
                    DisplayName = GetLocalizedNameFromId(id),
                    IconGlyph = GetGlyphForId(id),
                    Path = folder,
                    Items = helpSubItems,
                });
            }

            return helpItems;
        }
    }
}
