using CDPI_UI.DataModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper
{
    public static class HardLinkHelper
    {
        public static string IsFileLinked(string itemId, string fileName)
        {
            try
            {
                string filePath = SettingsManager.Instance.GetValue<string>(["HARDLINKS", itemId, fileName], "targetFile", raiseExceptionIfNotExits: true);
                return filePath == "NaN" ? null : filePath;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<AsyncOperationResultModel> CreateSymbolicLinkForItemId(string itemId, string linkFrom, string linkTo)
        {
            try
            {
                if (File.Exists(linkTo))
                {
                    string backupFilePath = BackupFile(linkTo);
                    Debug.WriteLine($"{itemId}, {linkFrom}");
                    SettingsManager.Instance.SetValue<string>(["HARDLINKS", itemId, linkTo], "backupFile", backupFilePath);
                    File.Delete(linkTo);
                }

                File.CreateSymbolicLink(linkTo, linkFrom);
                SettingsManager.Instance.SetValue<string>(["HARDLINKS", itemId, linkTo], "targetFile", linkFrom);

                return new()
                {
                    IsSuccess = true,
                };
            }
            catch (Exception ex)
            {
                return new()
                {
                    IsSuccess = false,
                    ErrorCode = ErrorsHelper.GetPrettyErrorCode("SOFTLINK", ex),
                    ErrorMessage = ex.Message,
                };
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        public static async Task<AsyncOperationResultModel> RemoveLinkForItemId(string itemId, string linkFrom, string linkTo)
        {
            try
            {
                Debug.WriteLine($">>> {linkTo} {itemId}");
                string backupFile = SettingsManager.Instance.GetValue<string>(["HARDLINKS", itemId, linkTo], "backupFile");
                if (!File.Exists(backupFile))
                {
                    throw new FileNotFoundException($"Backup file \"{backupFile}\" not found. Hardlink remove cannot be complete.");
                }
                File.Delete(linkTo);
                File.Move(backupFile, linkTo);

                SettingsManager.Instance.SetValue<string>(["HARDLINKS", itemId, linkTo], "backupFile", "NaN");
                SettingsManager.Instance.SetValue<string>(["HARDLINKS", itemId, linkTo], "targetFile", "NaN");

                return new()
                {
                    IsSuccess = true,
                };
            }
            catch (Exception ex)
            {
                return new()
                {
                    IsSuccess = false,
                    ErrorCode = ErrorsHelper.GetPrettyErrorCode("HARDLINK", ex),
                    ErrorMessage = ex.Message,
                };
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        public static async Task<AsyncOperationResultModel> CreateHardLinkForItemId(string itemId, string linkFrom, string linkTo)
        {
            try
            {
                if (File.Exists(linkTo))
                {
                    string backupFilePath = BackupFile(linkTo);
                    Debug.WriteLine($"{itemId}, {linkFrom}");
                    SettingsManager.Instance.SetValue<string>(["HARDLINKS", itemId, linkTo], "backupFile", backupFilePath);
                    File.Delete(linkTo);
                }

                CreateHardLink(linkTo, linkFrom, IntPtr.Zero);
                SettingsManager.Instance.SetValue<string>(["HARDLINKS", itemId, linkTo], "targetFile", linkFrom);

                return new()
                {
                    IsSuccess = true,
                };
            }
            catch (Exception ex)
            {
                return new()
                {
                    IsSuccess = false,
                    ErrorCode = ErrorsHelper.GetPrettyErrorCode("HARDLINK", ex),
                    ErrorMessage = ex.Message,
                };
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        private static string BackupFile(string filePath)
        {
            string bakFilePath = $"{filePath}.bak";
            File.Copy(filePath, bakFilePath, true);
            return bakFilePath;
        }

        #region WINAPI

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLink
        (
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes
        );

        enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreateSymbolicLink(
            string lpSymlinkFileName, 
            string lpTargetFileName, 
            SymbolicLink dwFlags
        );

        

        #endregion
    }
}
