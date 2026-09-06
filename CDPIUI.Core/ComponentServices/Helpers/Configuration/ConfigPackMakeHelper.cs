using CDPIUI.Core.Basic;
using CDPIUI.Core.JSON;
using CDPIUI.Core.LScript;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConvertionService;
using CDPIUI.Shared.Extentions;
using System.IO.Compression;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration
{
    public class ConfigPackMakeHelper
    {
        public class ConfigPackInitModel : LocalItemInitModel
        {
            public List<string> toggleListAvailable { get; set; } = [];
            public Dictionary<string, string> localized_strings_directory { get; set; } = [];
        }

        public static async Task<OperationResultModel<EmptyResult>> CreateConfigPack(
            ConfigPackInitModel modelTemplate, 
            List<ConfigItem> configItems, 
            string outputDir, 
            bool autoImport = false)
        {
            string tempDir = Path.Combine(
                Data.Directories.DataDirectory, "TempFiles", nameof(ConfigPackMakeHelper), SharedUtils.GenerateNewId());

            var workResult = await CreateConfigPack_Work(modelTemplate, configItems, outputDir, tempDir, autoImport);

            RemoveTempFiles(tempDir);

            return workResult;
        }

        private static async Task<OperationResultModel<EmptyResult>> CreateConfigPack_Work(
            ConfigPackInitModel modelTemplate, 
            List<ConfigItem> configItems, 
            string outputDir, 
            string tempDir, 
            bool autoImport = false)
        {
            List<string[]> requirements = [];

            configItems.RemoveAll(x => x.MarkAsRemoved);

            Directory.CreateDirectory(tempDir);

            var copyFilesResult = await CopyUsedFilesToDirecrory(configItems, tempDir);
            if (!(bool)copyFilesResult?.Success!) return copyFilesResult;

            var copyConfigsResult = await CopyConfigsToDirectory(configItems, tempDir);
            if (!(bool)copyConfigsResult?.Success!) { return copyConfigsResult.ToEmptyResult(); }

            var locFilesCopyResult = await CopyLocFilesToDirectory(modelTemplate, configItems, tempDir);
            if (!(bool)locFilesCopyResult?.Success!) { return locFilesCopyResult.ToEmptyResult(); }

            modelTemplate = locFilesCopyResult?.Result ?? modelTemplate;

            if (copyConfigsResult.Result is List<Tuple<string, string>> reqList)
            {
                foreach (var req in reqList)
                {
                    requirements.Add([req.Item1, req.Item2]);
                }
            }

            var model = FillInitModel(modelTemplate, requirements, tempDir);

            string jsonString = JSONConvertor.SerializeObject(model);

            try
            {
                File.WriteAllText(Path.Combine(tempDir, "init.json"), jsonString);
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>.FailureResult(
                    new ErrorModel()
                    {
                        ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("CONFIG_PACK_CREATE_I", ex),
                        FriendlyDescription = ex.Message,
                    });
            }

            if (autoImport)
            {
                try
                {
                    await Task.Run(() => Directory.Move(tempDir, outputDir));
                    ImportToDatabase(modelTemplate, outputDir);
                }
                catch (Exception ex)
                {
                    return OperationResultModel<EmptyResult>.FailureResult(
                        new ErrorModel()
                        {
                            ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("CONFIG_PACK_CREATE_AI", ex),
                            FriendlyDescription = ex.Message,
                        });
                }
                return OperationResultModel<EmptyResult>.SuccessResult();
            }

            var createArchiveResult = await CreateArchive(tempDir, outputDir);

            RemoveTempFiles(tempDir);

            if (!createArchiveResult.Success) { return createArchiveResult; }

            return OperationResultModel<EmptyResult>.SuccessResult();
        }

        private static void ImportToDatabase(ConfigPackInitModel modelTemplate, string folder)
        {
            DatabaseHelper.Instance.AddOrUpdateItem(new()
            {
                Id = modelTemplate.StoreId,
                Type = "configlist",
                Directory = folder,
                Executable = null,
                UpdateCheckUrl = null,
                DownloadUrl = null,
                DownloadFileType = null,
                VersionControlType = "local",
                CurrentVersion = ApplicationInfo.Version,
                RequiredItemIds = null,
                DependentItemIds = null,
                IconPath = "$STATICIMAGE(Store/empty.png)",
                Name = modelTemplate.Name,
                ShortName = modelTemplate.ShortName,
                Developer = modelTemplate.Developer,
                BackgroudColor = ""
            });
        }

        private static void RemoveTempFiles(string directory)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(ConfigPackMakeHelper), $"Cannot remove temp directory during {ex.Message} exception.");
            }
        }

        private static async Task<OperationResultModel<EmptyResult>> CreateArchive(string inputDir, string outputDir)
        {
            try
            {
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(Path.GetDirectoryName(outputDir)!);

                if (File.Exists(outputDir))
                    File.Delete(outputDir);

                ZipFile.CreateFromDirectory(inputDir, outputDir, CompressionLevel.Optimal, false);
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>.FailureResult(
                    new ErrorModel() { 
                        ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("CONFIG_PACK_CREATE", ex), 
                        FriendlyDescription = ex.Message 
                    });
            }

            await Task.CompletedTask;

            return OperationResultModel<EmptyResult>.SuccessResult();
        }



        private static ConfigPackInitModel FillInitModel(
            ConfigPackInitModel modelTemplate, 
            List<string[]> requirements, 
            string targetDirectory)
        {
            modelTemplate.Requirements ??= requirements;
            modelTemplate.Name ??= modelTemplate.ShortName;

            modelTemplate.BeforeInstallActions ??= string.Empty;
            modelTemplate.AfterInstallActions ??= $"$RUNSCRIPT(apply_config, {modelTemplate.StoreId})";

            modelTemplate.Type = "configlist";

            if (!string.IsNullOrEmpty(modelTemplate.Icon)) 
                modelTemplate.Icon = CopyIcon(modelTemplate.Icon, targetDirectory);


            return modelTemplate;
        }

        private static string CopyIcon(string icon, string targetDirectory)
        {
            if (icon.StartsWith('$')) return icon;

            try
            {
                File.Copy(icon, Path.Combine(targetDirectory, Path.GetFileName(icon)), true);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(ConfigPackMakeHelper), $"Cannot copy icon {ex.Message}");
            }

            return $"$DYNAMICIMAGE({Path.GetFileName(icon)})";
        }

        private static async Task<OperationResultModel<ConfigPackInitModel>> CopyLocFilesToDirectory(
            ConfigPackInitModel modelTemplate, 
            List<ConfigItem> items, 
            string targetDirectory)
        {
            List<Tuple<ConfigInitItem, string>> initItems = [];
            foreach (var item in items)
            {
                string dir = ConfigurationService.GetItemFolderFromPackId(item.packId!);
                string initFile = Path.Combine(dir, "init.json");

                if (!File.Exists(initFile)) continue;

                ConfigInitItem configInitItem = JSONConvertor.LoadJson<ConfigInitItem>(initFile);
                var data = Tuple.Create(configInitItem, dir);
                if (!initItems.Contains(data!)) initItems.Add(data!);
            }

            try
            {
                foreach (var item in initItems)
                {
                    if (item.Item1?.localized_strings_directory == null) continue;
                    foreach (var locFile in item.Item1.localized_strings_directory)
                    {
                        if (!modelTemplate.localized_strings_directory.ContainsKey(locFile.Key))
                            modelTemplate.localized_strings_directory.Add(locFile.Key, locFile.Value);

                        string targetDir = Path.Combine(targetDirectory, locFile.Value);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetDir)!);
                        if (File.Exists(targetDir))
                        {
                            var oldLocalizationDict = JSONConvertor.LoadJson<Dictionary<string, string>>(targetDir);
                            var newLocalizationDict = JSONConvertor.LoadJson<Dictionary<string, string>>(
                                Path.Combine(item.Item2, locFile.Value));

                            if (oldLocalizationDict == null || newLocalizationDict == null) continue;
                            oldLocalizationDict.AddRange(newLocalizationDict);

                            string jsonString = JSONConvertor.SerializeObject(oldLocalizationDict);
                            File.WriteAllText(targetDir, jsonString);
                        }
                        else
                        {
                            await FileSystemService.CopyFileAsync(Path.Combine(item.Item2, locFile.Value), targetDir);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return OperationResultModel<ConfigPackInitModel>.FailureResult(
                    new() { 
                        ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("CONFIG_PACK_COPY_L", ex), 
                        FriendlyDescription = ex.Message 
                    });
            }

            return OperationResultModel<ConfigPackInitModel>.SuccessResult(modelTemplate);
        }

        private static async Task<OperationResultModel<List<Tuple<string, string>>>> CopyConfigsToDirectory(
            List<ConfigItem> items, 
            string targetDirectory)
        {
            List<Tuple<string, string>> requirements = [];
            try
            {
                foreach (var item in items)
                {
                    string targetFile = Path.Combine(targetDirectory, item.file_name!);

                    string jsonString = JSONConvertor.SerializeObject(item);
                    File.WriteAllText(targetFile, jsonString);

                    if (requirements.FirstOrDefault(x => x.Item1 == item.target[0]) == null)
                    {
                        requirements.Add(Tuple.Create(item.target[0], item.target[1]));
                    }
                }
            }
            catch (Exception ex)
            {
                return OperationResultModel<List<Tuple<string, string>>>.FailureResult(
                    new() {
                        ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("CONFIG_PACK_COPY_C", ex),
                        FriendlyDescription = ex.Message,
                    });
            }

            await Task.CompletedTask;

            return OperationResultModel<List<Tuple<string, string>>>.SuccessResult(requirements);
        }

        private static async Task<OperationResultModel<EmptyResult>> CopyUsedFilesToDirecrory(
            List<ConfigItem> itemsToAdd, 
            string targetDirectory)
        {
            List<Tuple<string, string>> usedFiles = [];

            foreach (var item in itemsToAdd)
            {
                foreach (string file in ConfigurationService.GetUsedFilesFromConfigItem(item))
                {
                    Tuple<string, string> pair = 
                        Tuple.Create(
                            item.packId!, 
                            Autocorrector.FindAutoCorrectPath(
                                file, 
                                item, 
                                LScriptCore.ExecuteScript("$GETCURRENTDIR()", callItemId: item.packId)));
                    if (!usedFiles.Contains(pair))
                        usedFiles.Add(pair);
                }
            }

            try
            {
                foreach (Tuple<string, string> idFilePair in usedFiles)
                {
                    if (File.Exists(idFilePair.Item2))
                    {
                        string dir = Path.GetRelativePath(
                            ConfigurationService.GetItemFolderFromPackId(idFilePair.Item1), idFilePair.Item2);

                        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(targetDirectory, dir))!);
                        await FileSystemService.CopyFileAsync(idFilePair.Item2, Path.Combine(targetDirectory, dir));
                    }
                }
            }
            catch (Exception ex)
            {
                OperationResultModel<EmptyResult>.FailureResult(
                    new ErrorModel()
                    {
                        ErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("CONFIG_PACK_COPY_F", ex),
                        FriendlyDescription = ex.Message
                    });
            }

            return OperationResultModel<EmptyResult>.SuccessResult();
        }
    }
}
