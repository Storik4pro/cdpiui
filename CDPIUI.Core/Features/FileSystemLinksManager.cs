using CDPIUI.Core.Basic;
using CDPIUI.Shared.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CDPIUI.Core.Features
{
    public static class FileSystemLinksManager
    {
        public static string? IsFileLinked(string itemId, string fileName)
        {
            try
            {
                string filePath = SettingsManager.Instance.GetValue<string>(
                    ["HARDLINKS", itemId, fileName], "targetFile", raiseExceptionIfNotExits: true);
                return filePath == "NaN" ? null : filePath;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<OperationResultModel<EmptyResult>> CreateSymbolicLinkForItemId(
            string itemId,
            string linkFrom,
            string linkTo)
        {
            try
            {
                if (File.Exists(linkTo))
                {
                    string backupFilePath = BackupFile(linkTo);
                    Debug.WriteLine($"{itemId}, {linkFrom}");
                    SettingsManager.Instance.SetValue(
                        ["HARDLINKS", itemId, linkTo], "backupFile", backupFilePath);
                    File.Delete(linkTo);
                }

                File.CreateSymbolicLink(linkTo, linkFrom);
                SettingsManager.Instance.SetValue(
                    ["HARDLINKS", itemId, linkTo], "targetFile", linkFrom);

                return OperationResultModel<EmptyResult>.SuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>.FailureResult(new()
                {
                    ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("SOFTLINK", ex),
                    FriendlyDescription = ex.Message,
                });
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        public static async Task<OperationResultModel<EmptyResult>> RemoveLinkForItemId(
            string itemId, 
            string linkFrom, 
            string linkTo)
        {
            try
            {
                Debug.WriteLine($">>> {linkTo} {itemId}");
                string backupFile = SettingsManager.Instance.GetValue<string>(
                    ["HARDLINKS", itemId, linkTo], "backupFile");
                if (!File.Exists(backupFile))
                {
                    throw new FileNotFoundException(
                        $"Backup file \"{backupFile}\" not found. Hardlink remove cannot be complete.");
                }
                File.Delete(linkTo);
                File.Move(backupFile, linkTo);

                SettingsManager.Instance.SetValue(["HARDLINKS", itemId, linkTo], "backupFile", "NaN");
                SettingsManager.Instance.SetValue(["HARDLINKS", itemId, linkTo], "targetFile", "NaN");

                return OperationResultModel<EmptyResult>.SuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>.FailureResult(new()
                {
                    ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("SOFTLINK", ex),
                    FriendlyDescription = ex.Message,
                });
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        public static async Task<OperationResultModel<EmptyResult>> CreateHardLinkForItemId(
            string itemId,
            string linkFrom,
            string linkTo)
        {
            try
            {
                if (File.Exists(linkTo))
                {
                    string backupFilePath = BackupFile(linkTo);
                    Debug.WriteLine($"{itemId}, {linkFrom}");
                    SettingsManager.Instance.SetValue(
                        ["HARDLINKS", itemId, linkTo], "backupFile", backupFilePath);
                    File.Delete(linkTo);
                }

                CreateHardLink(linkTo, linkFrom, nint.Zero);
                SettingsManager.Instance.SetValue(
                    ["HARDLINKS", itemId, linkTo], "targetFile", linkFrom);

                return OperationResultModel<EmptyResult>.SuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>.FailureResult(new()
                {
                    ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("SOFTLINK", ex),
                    FriendlyDescription = ex.Message,
                });
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
            nint lpSecurityAttributes
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
