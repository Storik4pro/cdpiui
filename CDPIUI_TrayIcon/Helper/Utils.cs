using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI_TrayIcon.Helper
{
    public class Utils
    {
        public static Assembly Assembly = Assembly.GetExecutingAssembly();

        public static string GetDataDirectory()
        {
            try
            {
                var procPath = Environment.ProcessPath;
                return Path.GetDirectoryName(procPath)!;
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public static string GetSettingsFile()
        {
            string filePath = Path.Combine(GetDataDirectory(), "Settings", "Settings.xml");

            if (File.Exists(filePath))
            {
                return filePath;
            }
            else
            {
                return filePath; // TODO: find settings file in AppData
            }
        }

        public static void StartUpdate(string targetFile)
        {
            RunHelper.Run(Path.Combine(GetDataDirectory(), "Update.exe"), $"--directory-to-zip \"{targetFile}\" --destination-directory \"{GetDataDirectory()}\"");
        }
    }
}
