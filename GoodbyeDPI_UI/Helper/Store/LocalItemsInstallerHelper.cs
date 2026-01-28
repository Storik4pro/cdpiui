using CDPI_UI.Common;
using CDPI_UI.Helper.Static;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using static CDPI_UI.Helper.ErrorsHelper;

namespace CDPI_UI.Helper.Store
{
    public class LocalItemInitModel
    {
        public string StoreId { get; set; }
        public string Type { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public List<string[]> Requirements { get; set; }
        public string ShortName { get; set; }
        public string Color { get; set; }
        public string Developer { get; set; }
        public string BeforeInstallActions { get; set; }
        public string AfterInstallActions { get; set; }
        public string ExecutableFile { get; set; }
    }

    public class LocalItemsInstallerHelper
    {
        private readonly string TempDirectory;

        private const string AppTempDirectory = "TempFiles";
        private const string DownloadManagerDirectory = "Offline";

        public Action<string> ErrorHappens;

        private static LocalItemsInstallerHelper _instance;
        private static readonly object _lock = new object();

        public static LocalItemsInstallerHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new LocalItemsInstallerHelper();
                    return _instance;
                }
            }
        }

        private LocalItemsInstallerHelper() 
        {
            string localAppData = StateHelper.GetDataDirectory();
            TempDirectory = Path.Combine(localAppData, AppTempDirectory, DownloadManagerDirectory);
        }

        public async void InstallUpdate(string filepath)
        {

        }

        public async Task<LocalItemInitModel> ImportStoreItemPackFile(string itemPackFile)
        {
            string tempFolderName = $"{EpochTime.GetIntDate(DateTime.Now)}_ap";
            string tempDestination = Path.Combine(TempDirectory, tempFolderName);

            bool isCatalogCheckRequired = Path.GetExtension(itemPackFile) == ".cdpisignedpack" ? true : false;
            List<string> filesToSkip = isCatalogCheckRequired ? [] : [".exe"];

            try
            {
                await Utils.ExtractZip(itemPackFile, "/", tempDestination, filesToSkip: filesToSkip, isCatalogCheckRequired: isCatalogCheckRequired);
                string initFilePath = Path.Combine(tempDestination, "init.json");

                if (!File.Exists(initFilePath)) 
                {
                    string error = "ERR_LOCAL_ITEM_UNSUPPORTED";
                    ErrorHappens?.Invoke(error);
                    return null;
                }
                LocalItemInitModel localItemInitModel = Utils.LoadJson<LocalItemInitModel>(initFilePath);

                Directory.Delete(tempDestination, recursive: true);

                if (!isCatalogCheckRequired) localItemInitModel.ExecutableFile = null;
                return localItemInitModel;
            }
            catch (Exception ex)
            {
                string error = HandleError(ex);
                ErrorHappens?.Invoke(error);
                return null;
            }
        }

        public async void BeginLocalItemInstalling(string itemPackFile)
        {
            string tempFolderName = $"{EpochTime.GetIntDate(DateTime.Now)}_ap";
            string tempDestination = Path.Combine(TempDirectory, tempFolderName);

            bool isCatalogCheckRequired = Path.GetExtension(itemPackFile) == ".cdpisignedpack" ? true : false;
            List<string> filesToSkip = isCatalogCheckRequired ? [] : [".exe"];

            try
            {
                await Utils.ExtractZip(itemPackFile, "/", tempDestination, filesToSkip: filesToSkip, isCatalogCheckRequired: isCatalogCheckRequired);
                string initFilePath = Path.Combine(tempDestination, "init.json");

                if (!File.Exists(initFilePath))
                {
                    string error = "ERR_LOCAL_ITEM_UNSUPPORTED";
                    ErrorHappens?.Invoke(error);
                    return;
                }
                LocalItemInitModel localItemInitModel = Utils.LoadJson<LocalItemInitModel>(initFilePath);

                StoreHelper.Instance.AddItemToQueue(localItemInitModel.StoreId, packFile: tempDestination);
                return;
            }
            catch (Exception ex)
            {
                string error = HandleError(ex);
                ErrorHappens?.Invoke(error);
                return;
            }
        }

        public async void ImportApplicationUpdatePatchFromFile(string path)
        {
            
            string tempFolderName = $"{EpochTime.GetIntDate(DateTime.Now)}_ap";
            string tempDestination = Path.Combine(TempDirectory, tempFolderName);

            bool isCatalogCheckRequired = Path.GetExtension(path) == "cdpisignedpack" ? true : false;
            List<string> filesToSkip = isCatalogCheckRequired ? [] : [".exe"];

            try
            {
                await Utils.ExtractZip(path, "/", tempDestination, filesToSkip: filesToSkip, isCatalogCheckRequired: isCatalogCheckRequired);
            }
            catch (Exception ex)
            {
                string error = HandleError(ex);
            }

        }

        private string HandleError(Exception ex)
        {
            string prettyError;

            var codeObj = ErrorHelper.MapExceptionToCode(ex, out uint? hr, out int? statusCode);
            var code = codeObj.ToString();
            string _statusCode = statusCode != null ? $"_{statusCode}" : "";
            Logger.Instance.CreateErrorLog(nameof(ErrorHelper), $"{code} - {ex}");
            if (hr != null)
            {
                string hrHex = $"0x{hr.Value:X8}";
                prettyError = $"ERR_LOCAL_ITEM_INSTALLING_{code}{statusCode} ({hrHex})";
            }
            else
            {
                prettyError = $"ERR_LOCAL_ITEM_INSTALLING_{code}{statusCode}";
            }
            return prettyError;
        }
    }
}
