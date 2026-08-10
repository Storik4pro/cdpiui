using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core.Data
{
    public static class Directories
    {
        /// <summary>
        /// Application data directory (%LOCALAPPDATA% if current folder protected, otherwise CURRENT)
        /// </summary>
        public static string DataDirectory { get => DirectoriesHelper.Instance.DataDirectory; }

        /// <summary>
        /// Application current directory
        /// </summary>
        public static string CurrentDirectory { get => DirectoriesHelper.Instance.CurrentDirectory; }

        public static string SettingsDirectory { get => DataCombine("Settings"); }
        public static string SettingsFilePath { get => Path.Combine(SettingsDirectory, "Settings.xml"); }
        public static string TemplatesDirectory { get => CurrentDirCombine("Template"); }
        public static string TemplateSettingsDirectory { get => Path.Combine(TemplatesDirectory, "Settings"); }
        public static string TemplateSettingsFilePath { get => Path.Combine(TemplateSettingsDirectory, "Settings.xml"); }

        #region Store
        public static string StoreDirectory => DataCombine("Store");
        public static string StoreItemsDirectory => Path.Combine(StoreDirectory, "Items");
        public static string StoreCacheDirectory => Path.Combine(StoreDirectory, "Cache");
        public static string StoreLocalCacheDirectory => Path.Combine(StoreCacheDirectory, "Local");
        public static string StoreRepoCacheDirectory => Path.Combine(StoreCacheDirectory, "Repo");

        public static string StoreLocalUserItemDirectory => Path.Combine(StoreItemsDirectory, SharedConstants.LocalUserItemsId);
        #endregion

        #region ELUA

        public static string ELUADirectory => Path.Combine(CurrentDirectory, "ELUA");

        #endregion

        public static string TempFilesDirectory { get => DataCombine("TempFiles"); }

        #region Download Manager

        public static string DownloadManagerDirectory { get => Path.Combine(TempFilesDirectory, "Downloads"); }

        #endregion

        private static string DataCombine(params string[] paths)
        {
            return Path.Combine(DataDirectory, Path.Combine(paths));
        }
        private static string CurrentDirCombine(params string[] paths)
        {
            return Path.Combine(CurrentDirectory, Path.Combine(paths));
        }
    }

    public class DirectoriesHelper
    {
        private static DirectoriesHelper? _instance;
        private static readonly object _lock = new();

        public static DirectoriesHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new DirectoriesHelper();
                    return _instance;
                }
            }
        }

        private DirectoriesHelper() { }

        public string DataDirectory { get => TryGetDataDirectory(); }

        private string? DataDirectoryProperty;
        private string TryGetDataDirectory()
        {
            if (DataDirectoryProperty != null) return DataDirectoryProperty;
            var result = DirectoriesManager.GetDataDirectory(Environment.ProcessPath, false);

            if (result.Success)
            {
                DataDirectoryProperty = result.Result;
                return result.Result!;
            }
            else
            {
                DataDirectoryProperty = string.Empty;
                CoreEvents.Instance.InvokeCriticalCoreExceptionHappens(result.Error ?? new UnknownException());
                return string.Empty;
            }
        }

        public string CurrentDirectory { get => TryGetCurrentDirectory(); }

        private string? CurrentDirectoryProperty;
        private string TryGetCurrentDirectory()
        {
            if (CurrentDirectoryProperty != null) return CurrentDirectoryProperty;
            var result = DirectoriesManager.GetDataDirectory(Environment.ProcessPath, true);

            if (result.Success)
            {
                CurrentDirectoryProperty = result.Result;
                return result.Result!;
            }
            else
            {
                CurrentDirectoryProperty = string.Empty;
                CoreEvents.Instance.InvokeCriticalCoreExceptionHappens(result.Error ?? new UnknownException());
                return string.Empty;
            }
        }
    }
}
