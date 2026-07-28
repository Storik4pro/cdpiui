using CDPIUI.Core.Data;

using CDPIUI.Shared.Basic.Filesystem;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinUI3Localizer;

namespace CDPIUI.Helper.Parsers
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
    public class HelpParser
    {
        private static Dictionary<string, string> HelpIconGlyphsPairs = new()
        {
            { "WelcomeToHelp", "\uE734" },
            { "GettingStarted", "\uE7BE" },
            { "TroubleshootingComponentExceptions", "\uEBE8" },
            { "Store", "\uE719" },
            { "CreateConfigHelper", "\uE70F" },
            { "Utils", "\uE7B8" },
            { "Autoselection", "\uEB9D" },
            { "ConditionalLaunch", "\uE8F1" },
            { "Other", "\uE835" },
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
            string path =  Path.Combine(Directories.CurrentDirectory, "Help", language);

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
                string id = FileSystemService.GetFolderNamesUpTo(folder, language);

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
