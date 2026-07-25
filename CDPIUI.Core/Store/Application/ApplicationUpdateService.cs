using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.JSON;
using CDPIUI.Core.Security;
using CDPIUI.Core.Store.Network;
using CDPIUI.Core.System;
using CDPIUI.Shared;
using CDPIUI.Shared.Exceptions;
using CDPIUI.Shared.Exceptions.Catalog;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConvertionService;
using System.IO.Compression;

namespace CDPIUI.Core.Store.Application
{
    internal class ApplicationUpdateService
    {
        public async Task<OperationResultModel<EmptyResult>> GetPatchReadyToInstall(
            string filePath,
            string operationId,
            DownloadWorker? downloadWorker)
        {
            if (downloadWorker == null)
                return OperationResultModel<EmptyResult>
                    .FailureResult(HandleException(PrettyErrorCode.NULL_REFERENCE));

            string appItemDirectory = Path.Combine(Directories.StoreItemsDirectory, SharedConstants.ApplicationStoreId);

            string patchesDirectory = Path.Combine(appItemDirectory, "Patches");
            string finalDirectory = Path.Combine(appItemDirectory, "CDPIUI");

            string patchDirectory = Path.Combine(
                patchesDirectory,
                Path.GetFileNameWithoutExtension(filePath));

            try
            {
                await UnpackPatch(filePath, patchDirectory);

                var requirementsResult = LoadPatchRequirements(patchDirectory);
                if (!requirementsResult.Success)
                    return OperationResultModel<EmptyResult>.FailureResult(requirementsResult.Error);
                PatchRequirementsModel requirements = requirementsResult.Result!;

                Version currentVersion = new(ApplicationInfo.Version);
                Version patchVersion = new(requirements.version!);

                var versionResult = ValidatePatchVersion(currentVersion, patchVersion);
                if (!versionResult.Success)
                    return versionResult;

                var catalogResult = await ValidateMainCatalog(patchDirectory);
                if (!catalogResult.Success)
                    return catalogResult;

                var patchesResult = await DownloadRequiredPatches(
                    requirements,
                    currentVersion,
                    patchDirectory,
                    downloadWorker);
                if (!patchesResult.Success)
                    return OperationResultModel<EmptyResult>.FailureResult(patchesResult.Error);
                List<string> downloadedPatches = patchesResult.Result!;

                FinalizePatch(
                    downloadedPatches,
                    patchDirectory,
                    finalDirectory,
                    appItemDirectory);

                return OperationResultModel<EmptyResult>.SuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>.FailureResult(HandleException(ex));
            }
        }

        private async Task UnpackPatch(string archivePath, string patchDirectory) => 
            await ZipService.ExtractZip(archivePath, "/", patchDirectory);

        private OperationResultModel<PatchRequirementsModel> LoadPatchRequirements(string patchDirectory)
        {
            try
            {
                string requirementsFile = Path.Combine(patchDirectory, "requirements.json");
                PatchRequirementsModel requirements = JSONConvertor.LoadJson<PatchRequirementsModel>(requirementsFile);
                if (string.IsNullOrEmpty(requirements.version))
                    throw new UnknownFileFormatException();
                return OperationResultModel<PatchRequirementsModel>.SuccessResult(requirements);
            }
            catch (Exception ex)
            {
                return OperationResultModel<PatchRequirementsModel>.FailureResult(HandleException(ex));
            }
        }

        private OperationResultModel<EmptyResult> ValidatePatchVersion(Version current, Version patchVersion)
        {
            if (current >= patchVersion)
                return OperationResultModel<EmptyResult>.FailureResult(
                    HandleException(new NewestVersionAlreadyInstalledException()));
            return OperationResultModel<EmptyResult>.SuccessResult();
        }

        private async Task<OperationResultModel<EmptyResult>> ValidateMainCatalog(string patchDirectory)
        {
            try
            {
                string mainCatalogPath = Path.Combine(patchDirectory, "CDPIUI", "catalog.cat");

                await ValidateCatalog(Path.Combine(patchDirectory, "CDPIUI"));

                return OperationResultModel<EmptyResult>.SuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResultModel<EmptyResult>.FailureResult(HandleException(ex));
            }
        }

        private async Task<OperationResultModel<List<string>>> DownloadRequiredPatches(
            PatchRequirementsModel requirements,
            Version currentVersion,
            string patchDirectory,
            DownloadWorker downloadWorker)
        {
            var patches = new List<string>();
            try
            {
                foreach (string requirement in requirements.patch_urls)
                {
                    string[] parts = requirement.TrimStart('/').Split('/');
                    if (parts.Length > 2)
                    {
                        string[] reversedParts = parts.Reverse().ToArray();
                        Version patchVer = new(reversedParts[2]);
                        if (currentVersion >= patchVer)
                            continue;
                    }
                    else
                    {
                        continue; // TODO: Fix requiriments search for GitLab
                    }

                    TimeSpan epochTime = DateTime.UtcNow - new DateTime(1970, 1, 1);
                    int secondsSinceEpoch = (int)epochTime.TotalSeconds;
                    string downloadDir = Path.Combine(patchDirectory, secondsSinceEpoch.ToString());
                    patches.Add(downloadDir);

                    bool ok = await downloadWorker.DownloadAndExtractAsync(
                        requirement,
                        downloadDir,
                        extractArchive: true,
                        extractSkipFiletypes: [],
                        extractRootFolder: "CDPIUI/",
                        executableFileName: "",
                        filetype: Shared.Basic.Filesystem.FileExtentionTypes.archive);

                    if (!ok)
                        return OperationResultModel<List<string>>.UnSuccessResult();

                    await ValidateCatalog(downloadDir);
                }
            }
            catch (Exception ex)
            {
                return OperationResultModel<List<string>>.FailureResult(HandleException(ex));
            }

            patches.Reverse();
            return OperationResultModel<List<string>>.SuccessResult(patches);
        }

        private static async Task ValidateCatalog(string dir)
        {
            string catalogPath = Path.Combine(dir, "catalog.cat");
            if (!File.Exists(catalogPath))
                throw new FileNotFoundException("Catalog file not found");

            var catalogCheck = await CertificateCheck.CheckCatalog(catalogPath, dir);
            if (catalogCheck != CatalogCheckResult.Success)
                throw new CatalogInvalid();
        }

        private void FinalizePatch(List<string> patches, string patchDirectory, string finalDirectory, string appItemDirectory)
        {
            foreach (string dir in patches)
            {
                Directory.Move(dir, finalDirectory);
            }
            Directory.Move(Path.Combine(patchDirectory, "CDPIUI"), finalDirectory);

            string finalPatchFile = Path.Combine(appItemDirectory, "patch.cdpipatch");
            if (File.Exists(finalPatchFile))
                File.Delete(finalPatchFile);
            ZipFile.CreateFromDirectory(patchDirectory, finalPatchFile);
        }

        private static ErrorModel HandleException(Exception ex)
        {
            var model = ErrorsHelper.Convertor.GetPrettyErrorCode("ERR_APP_UPDATE", ex);
            return new() { ErrorCode = model };
        }
        private static ErrorModel HandleException(PrettyErrorCode errorCode)
        {
            return ErrorModel.OnlyErrorCode($"ERR_STORE_{errorCode}");
        }
    }
    
}
