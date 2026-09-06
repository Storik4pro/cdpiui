using CDPIUI.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CDPIUI.Shared.Basic.Filesystem
{
    internal class DirectoryWritePermissionCheckService
    {
        public static UnprocessedOperationResultModel<EmptyResult> HasWritePermission(string directory)
        {
            var testFile = Path.Combine(directory, $".write_test_{Guid.NewGuid():N}.tmp");
            try
            {
                using (var fs = new FileStream(testFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fs.WriteByte(0x0);
                    fs.Flush();
                }

                File.Delete(testFile);
                return UnprocessedOperationResultModel<EmptyResult>.SuccessResult();
            }
            catch (UnauthorizedAccessException)
            {
                return UnprocessedOperationResultModel<EmptyResult>.UnSuccessResult();
            }
            catch (DirectoryNotFoundException)
            {
                return UnprocessedOperationResultModel<EmptyResult>.UnSuccessResult();
            }
            catch (IOException)
            {
                return UnprocessedOperationResultModel<EmptyResult>.UnSuccessResult();
            }
            catch (Exception ex)
            {
                return UnprocessedOperationResultModel<EmptyResult>.FailureResult(ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(testFile))
                        File.Delete(testFile);
                }
                catch { }
            }
        }
    }
}
