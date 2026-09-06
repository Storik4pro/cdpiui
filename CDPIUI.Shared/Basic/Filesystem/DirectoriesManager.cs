using CDPIUI.Shared.Exceptions;
using CDPIUI.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CDPIUI.Shared.Basic.Filesystem
{
    public class DirectoriesManager
    {
        public static UnprocessedOperationResultModel<string> GetDataDirectory(string? procPath, bool getCurrent = false, bool forceAppData = false)
        {
            try
            {
                if (procPath == null) throw new ArgumentNullException(nameof(procPath));

                if ((DirectoryWritePermissionCheckService.HasWritePermission(Path.GetDirectoryName(procPath)).Success || getCurrent) && !forceAppData)
                    return UnprocessedOperationResultModel<string>.SuccessResult(Path.GetDirectoryName(procPath)!);
                else
                {
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var targetFolder = Path.Combine(localAppData, "CDPIUI");
                    if (!Directory.Exists(targetFolder))
                        Directory.CreateDirectory(targetFolder);
                    return UnprocessedOperationResultModel<string>.SuccessResult(targetFolder);
                }
            }
            catch (Exception ex)
            {
                return UnprocessedOperationResultModel<string>.FailureResult(ex);
            }
        }

        
    }
}
