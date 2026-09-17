using CDPIUI.Shared;
using CDPIUI.Shared.Pipe.Models;
using CDPIUI.TrayIcon.Helper.Basic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.TrayIcon.Helper
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
                _ = PipeHelper.SendApplicationPacket(ApplicationMessageIds.CloseApplicationUI);
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

        public static float GetScalingFactorForMainDisplay()
        {
            var currentDPI = Int32.Parse((string?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ThemeManager", "LastLoadedDPI", "96") ?? "96");
            var scale = 96 / (float?)currentDPI ?? 96;
            return scale;
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
                if (conptySignal) await PipeHelper.SendGrantAcessPacket(true);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(Utils), $"Cannot grant access for \"{file}\". Exception message: {ex.Message}");
            }

            if (conptySignal) await PipeHelper.SendGrantAcessPacket(false);
        }
    }
}
