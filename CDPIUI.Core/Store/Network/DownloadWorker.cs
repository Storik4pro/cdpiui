using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.System;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.Shared.Exceptions;
using CDPIUI.Shared.PrettyErrorConvertionService;
using CDPIUI.Shared.Extentions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CDPIUI.Core.Basic.ErrorsHelper;
using static CDPIUI.Core.Store.MSI.MsiInstallerService;
using CDPIUI.Core.Store.MSI;

namespace CDPIUI.Core.Store.Network
{
    internal class DownloadWorker : IDisposable
    {
        private readonly RequestWorker requestWorker;
        private readonly string TempDirectory = Directories.DownloadManagerDirectory;

        private readonly CancellationToken cancellationToken;

        public readonly string OperationId;
        private string? msiGUID;

        public ErrorModel? LastError { get; private set; }

        public DownloadWorker(string operationId, CancellationToken cancellationToken, HttpClient? client = null)
        {
            this.cancellationToken = cancellationToken;

            OperationId = operationId;
            requestWorker = new RequestWorker(client);
        }

        public event Action<Tuple<string, double>>? DownloadSpeedChanged;
        public event Action<Tuple<string, double>>? ProgressChanged;
        public event Action<Tuple<string, TimeSpan>>? TimeRemainingChanged;
        public event Action<Tuple<string, string>>? StageChanged; // Downloading, Extracting, Completed, ErrorHappens
        public event Action<Tuple<string, string, string>>? ErrorHappens;

        public bool IsRestartNeeded = false;

        public async Task<bool> DownloadAndExtractAsync(
            string url,
            string destinationPath,
            bool extractArchive = false,
            IEnumerable<string>? extractSkipFiletypes = null,
            string? extractRootFolder = null,
            string? executableFileName = "executableFile",
            string filetype = "",
            bool removeAfterAction = false,
            string filename = ""
        )
        {
            LastError = null;
            IsRestartNeeded = false;
            bool success = false;
            List<string> _extractedFiles = [];

            var enFiletype = filetype.ToEnum<FileExtentionTypes>(FileExtentionTypes.none);
            bool isSupportedFileExtention = enFiletype != FileExtentionTypes.none;


            string tempFileName = FileSystemService.GetNewTempFileName(nameof(DownloadWorker), FileSystemService.GetFileExtention(FileExtentionTypes.temp));
            string tempDestination = Path.Combine(TempDirectory, tempFileName);

            try
            {
                Directory.CreateDirectory(TempDirectory);

                StageChanged?.Invoke(Tuple.Create(OperationId, "Downloading"));

                Logger.Instance.CreateDebugLog(nameof(DownloadWorker), $"Uri used: {url}");

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await requestWorker.DownloadFileAsync(url, tempDestination, ReportDownloadProgress,
                        cancellationToken: cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
                {
                    StageChanged?.Invoke(Tuple.Create(OperationId, "ErrorHappens"));
                    ErrorHappens?.Invoke(Tuple.Create(OperationId, $"ERR_DOWNLOAD_{PrettyErrorCode.UNEXPECTED_STATUS_CODE}_{(int)ex.StatusCode.Value}", "Server Error"));
                    return false;
                }



                cancellationToken.ThrowIfCancellationRequested();
                if (extractArchive)
                {
                    StageChanged?.Invoke(Tuple.Create(OperationId, "Extracting"));
                    await ZipService.ExtractZip(tempDestination, extractRootFolder, destinationPath, extractSkipFiletypes, isCatalogCheckRequired: filetype == FileExtentionTypes.signedZip.ToString());
                }
                else
                {
                    if (!string.IsNullOrEmpty(executableFileName))
                        File.Copy(tempDestination, Path.Combine(destinationPath, executableFileName + (!isSupportedFileExtention ? filetype : FileSystemService.GetFileExtention(enFiletype))), true);
                    else
                    {
                        string exeName = string.IsNullOrEmpty(filename) ? GetFileNameFromUri(url) : filename;
                        if (string.IsNullOrEmpty(exeName))
                            throw new IOException();

                        string extention = !isSupportedFileExtention? filetype : FileSystemService.GetFileExtention(enFiletype);

                        File.Copy(tempDestination, Path.Combine(destinationPath, exeName + extention), true);
                    }
                }

                if (filetype == FileExtentionTypes.msi.ToString() || filetype == FileExtentionTypes.elmsi.ToString())
                {
                    StageChanged?.Invoke(Tuple.Create(OperationId, "ConnectingToService"));
                    string installerExeName = GetFileNameFromUri(url);
                    installerExeName = string.IsNullOrEmpty(installerExeName) ? "installer" : installerExeName;

                    string msiPath = Path.Combine(destinationPath, installerExeName + (!isSupportedFileExtention ? filetype : FileSystemService.GetFileExtention(enFiletype)));
                    msiGUID = Guid.NewGuid().ToString();
                    MsiInstallerService msiInstallerHelper = new(msiGUID, msiPath);
                    msiInstallerHelper.callbackAction += HandleMsiInstallerMessage;
                    MsiCallback callback = await msiInstallerHelper.Run(cancellationToken);
                    msiInstallerHelper.callbackAction -= HandleMsiInstallerMessage;

                    Logger.Instance.CreateDebugLog(nameof(DownloadWorker), "TRY");

                    if (callback.State == MsiState.ExceptionHappens)
                    {
                        success = false;
                        throw new MsiInstallException("MSI_UNKNOWN");
                    }
                    else if (callback.State == MsiState.CompleteRestartRequest)
                    {
                        IsRestartNeeded = true;
                    }

                    if (removeAfterAction)
                    {
                        File.Delete(msiPath);
                    }
                }


                cancellationToken.ThrowIfCancellationRequested();
                StageChanged?.Invoke(Tuple.Create(OperationId, "Completed"));
                success = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is a user action, not a download failure.
            }
            catch (Exception ex) 
            {
                HandleError(ex);
            }
            finally
            {
                try { if (File.Exists(tempDestination)) File.Delete(tempDestination); } catch { }

                if (!success || removeAfterAction)
                {
                    try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }

                    foreach (var file in _extractedFiles ?? Enumerable.Empty<string>())
                    {
                        try { if (File.Exists(file)) File.Delete(file); } catch { }
                    }
                }
            }
            return success;
        }

        public async Task<bool> DownloadAndExtractAsync(
            string url,
            string destinationPath,
            bool extractArchive = false,
            IEnumerable<string>? extractSkipFiletypes = null,
            string? extractRootFolder = null,
            string? executableFileName = "executableFile",
            FileExtentionTypes filetype = FileExtentionTypes.temp,
            bool removeAfterAction = false,
            string filename = ""
        )
        {
            return await DownloadAndExtractAsync(url, destinationPath, extractArchive, extractSkipFiletypes, extractRootFolder, executableFileName, filetype.ToString(), removeAfterAction, filename);
        }

        private void ReportDownloadProgress(long totalRead, long totalBytes, TimeSpan elapsed)
        {
            var speed = totalRead / elapsed.TotalSeconds;
            DownloadSpeedChanged?.Invoke(Tuple.Create(OperationId, speed));

            if (totalBytes != -1)
            {
                var progress = (double)totalRead / totalBytes * 100;
                ProgressChanged?.Invoke(Tuple.Create(OperationId, progress));

                var timeRemaining = TimeSpan.FromSeconds((totalBytes - totalRead) / speed);
                TimeRemainingChanged?.Invoke(Tuple.Create(OperationId, timeRemaining));
            }
        }


        

        public void HandleMsiInstallerMessage(MsiCallback callback)
        {
            if (msiGUID == callback.operationId)
            {
                StageChanged?.Invoke(Tuple.Create(OperationId, callback.State.ToString()));
            }
        }

        private string GetFileNameFromUri(string _uri)
        {
            var uri = new Uri(_uri);

            string path = uri.AbsolutePath.TrimEnd('/');
            string fileName = Path.GetFileNameWithoutExtension(path); 
            return fileName;
        }

        private void HandleError(Exception ex)
        {
            StageChanged?.Invoke(Tuple.Create(OperationId, "ErrorHappens"));
            string errorCode = Convertor.GetPrettyErrorCode("ERR_NET_DOWNLOAD", ex);
            ErrorHappens?.Invoke(Tuple.Create(OperationId, errorCode, $"{ex}"));
            LastError = ErrorModel.OnlyErrorCode(errorCode);
        }
        public void Dispose()
        {
            requestWorker.Dispose();
        }
    }
}
