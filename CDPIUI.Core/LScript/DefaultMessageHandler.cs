using CDPIUI.Core.Data;

namespace CDPIUI.Core.LScript
{
    internal class DefaultMessageHandler()
    {
        public static string StaticImageScript(string data)
        {
            return $"ms-appx:///Assets/{data}";
        }

        public static string DynamicPathConverter(string data, string? args = "")
        {
            if (!string.IsNullOrEmpty(args))
            {
                return Path.Combine(args, data);
            }
            string targetFolder = Path.Combine(Directories.StoreRepoCacheDirectory, data);
            return targetFolder;
        }

        public static string LoadAllTextFromFile(string filepath)
        {
            return File.ReadAllText(filepath);
        }
    }
}
