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
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var targetFolder = Path.Combine(localAppData, "CDPIUI");
                string localAppDataFile = Path.Combine(targetFolder, "Settings", "Settings.xml");

                if (File.Exists(localAppDataFile))
                {
                    return localAppDataFile;
                }

                return filePath; 
            }
        }

        public static void StartUpdate(string targetFile)
        {
            RunHelper.Run(Path.Combine(GetDataDirectory(), "Update.exe"), $"--directory-to-zip \"{targetFile}\" --destination-directory \"{GetDataDirectory()}\"");
        }
    }
}
