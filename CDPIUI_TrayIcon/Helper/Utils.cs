using CDPIUI_TrayIcon.Helper.Basic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
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
            catch
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
            if (Path.GetExtension(targetFile).ToLower() == ".msi")
            {
                RunHelper.Run("msiexec.exe", $"/i \"{targetFile}\" /qn+");
                _ = PipeServer.Instance.SendMessage("MAIN:EXIT_ALL");
                NotifyHelper.Instance.Dispose();
                Application.Exit();
            }
            else
            {
                RunHelper.Run(Path.Combine(GetDataDirectory(), "Update.exe"), $"--directory-to-zip \"{targetFile}\" --destination-directory \"{GetDataDirectory()}\"");
            }
        }

        public static Bitmap? GetBitmapFromResourses(string resourseKey)
        {
            var resource = Utils.Assembly.GetManifestResourceStream(resourseKey);
            if (resource != null)
            {
                return new Bitmap(resource);
            }
            return null;
        }

        public static bool IsOsSupportedNewGlyph()
        {
            Debug.WriteLine(Environment.OSVersion.ToString());
            var version1 = Environment.OSVersion.Version;
            string v2 = "10.0.22000.194";

            var version2 = new Version(v2);
            if (version1 >= version2) return true;
            return false;
        }

        public static async void GrantAccess(string file, bool conptySignal)
        {
            try
            {
                bool exists = System.IO.Directory.Exists(file);
                if (!exists)
                {
                    DirectoryInfo di = System.IO.Directory.CreateDirectory(file);
                }
                DirectoryInfo dInfo = new DirectoryInfo(file);
                DirectorySecurity dSecurity = dInfo.GetAccessControl();
                dSecurity.AddAccessRule(
                    new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                        FileSystemRights.FullControl,
                        InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                        PropagationFlags.NoPropagateInherit,
                        AccessControlType.Allow
                        )
                    );
                dInfo.SetAccessControl(dSecurity);
                if (conptySignal) await PipeServer.Instance.SendMessage("UTILS:GRANT_ACCESS(true)");
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(Utils), $"Cannot grant access for \"{file}\". Exception message: {ex.Message}");
            }

            if (conptySignal) await PipeServer.Instance.SendMessage("UTILS:GRANT_ACCESS(false)");
        }
    }
}
