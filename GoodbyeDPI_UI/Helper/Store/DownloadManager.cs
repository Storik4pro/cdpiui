using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Shell;
using Windows.Media.Protection.PlayReady;
using static CDPI_UI.Helper.ErrorsHelper;

namespace CDPI_UI.Helper
{
    public class AsyncOperationException : System.Exception
    {
        public AsyncOperationException() : base() { }
        public AsyncOperationException(string message) : base(message) { }
        public AsyncOperationException(string message, System.Exception inner) : base(message, inner) { }

        protected AsyncOperationException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }
    public class DownloadManager : IDisposable
    {
        private readonly HttpClient _client;
        private readonly string TempDirectory;

        private const string AppTempDirectory = "TempFiles";
        private const string DownloadManagerDirectory = "Downloads";

        private CancellationTokenSource source;
        private CancellationToken cancellationToken;

        public readonly string OperationId;

        public DownloadManager(string operationId, CancellationTokenSource cancellationTokenSource, HttpClient client = null)
        {
            source = cancellationTokenSource;
            cancellationToken = source.Token;

            OperationId = operationId;
            _client = client ?? new HttpClient();

            string localAppData = StateHelper.GetDataDirectory();
            TempDirectory = Path.Combine(localAppData, AppTempDirectory, DownloadManagerDirectory);
        }

        public event Action<double> DownloadSpeedChanged;
        public event Action<double> ProgressChanged;
        public event Action<TimeSpan> TimeRemainingChanged;
        public event Action<string> StageChanged; // Downloading, Extracting, Completed, ErrorHappens
        public event Action<Tuple<string, string>> ErrorHappens;

        public async Task DownloadAndExtractAsync(
            string url,
            string destinationPath,
            bool extractArchive = false,
            IEnumerable<string> extractSkipFiletypes = null,
            string extractRootFolder = null,
            string executableFileName = "executableFile",
            string filetype = ""
        )
        {
            bool success = false;
            List<string> _extractedFiles = new List<string>();


            string tempFileName = $"{EpochTime.GetIntDate(DateTime.Now)}_dm.cdpitempfile";
            string tempDestination = Path.Combine(TempDirectory, tempFileName);

            try
            {
                Directory.CreateDirectory(TempDirectory);

                StageChanged?.Invoke("Downloading");

                var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    StageChanged?.Invoke("ErrorHappens");
                    ErrorHappens?.Invoke(Tuple.Create<string, string>($"ERR_DOWNLOAD_{PrettyErrorCode.UNEXPECTED_STATUS_CODE}_{response.StatusCode}", "Server Error"));
                    return;
                }
                response.EnsureSuccessStatusCode();
                bool _result = await DownloadFile(tempDestination, response, cancellationToken);

                if (!_result)
                {
                    throw new AsyncOperationException();
                }

                if (extractArchive)
                {
                    StageChanged?.Invoke("Extracting");
                    ExtractZip(tempDestination, extractRootFolder, destinationPath, extractSkipFiletypes);
                }
                else
                {
                    if (!string.IsNullOrEmpty(executableFileName))
                        File.Copy(tempDestination, Path.Combine(destinationPath, executableFileName + StateHelper.Instance.FileTypes.GetValueOrDefault(filetype, ".tmp")), true);
                    else
                        throw new IOException();
                }


                StageChanged?.Invoke("Completed");
                success = true;
            }
            catch (AsyncOperationException)
            {
                // pass
            }
            catch (Exception ex) 
            { 
                HandleError(ex);
            }
            finally
            {
                try { if (File.Exists(tempDestination)) File.Delete(tempDestination); } catch { }

                if (!success)
                {
                    try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }

                    foreach (var file in _extractedFiles ?? Enumerable.Empty<string>())
                    {
                        try { if (File.Exists(file)) File.Delete(file); } catch { }
                    }
                }
            }
        }

        private async Task<bool> DownloadFile(string tempDestination, HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempDestination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            var stopwatch = Stopwatch.StartNew();
            var lastUpdate = stopwatch.Elapsed;

            try
            {
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                    totalRead += read;

                    var now = stopwatch.Elapsed;
                    var interval = now - lastUpdate;
                    if (interval.TotalSeconds >= 1 || totalRead == totalBytes)
                    {
                        var speed = totalRead / now.TotalSeconds;
                        DownloadSpeedChanged?.Invoke(speed);

                        if (canReportProgress)
                        {
                            var progress = (double)totalRead / totalBytes * 100;
                            ProgressChanged?.Invoke(progress);

                            var timeRemaining = TimeSpan.FromSeconds((totalBytes - totalRead) / speed);
                            TimeRemainingChanged?.Invoke(timeRemaining);
                        }

                        lastUpdate = now;
                    }
                }
                return true;
            }
            catch (Exception ex) 
            {
                HandleError(ex);
            }
            return false;

        }


        private static void ExtractZip(
            string zipFilePath,
            string zipFolderToUnpack,
            string extractTo,
            IEnumerable<string> filesToSkip = null
        )
        {
            filesToSkip = filesToSkip ?? Enumerable.Empty<string>();

            if (!Directory.Exists(extractTo))
                Directory.CreateDirectory(extractTo);

            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                var entries = archive.Entries;
                int totalFiles = entries.Count;
                int extractedFiles = 0;

                if (zipFolderToUnpack == "/")
                    zipFolderToUnpack = string.Empty;
                else if (zipFolderToUnpack.EndsWith("/"))
                    zipFolderToUnpack = zipFolderToUnpack.TrimEnd('/');

                var patternSegments = string.IsNullOrEmpty(zipFolderToUnpack)
                                        ? Array.Empty<string>()
                                        : zipFolderToUnpack.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var entry in entries)
                {
                    var entryPath = entry.FullName.Replace('\\', '/').TrimStart('/');

                    var entrySegments = entryPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                    bool isMatch = true;
                    if (patternSegments.Length > 0)
                    {
                        if (entrySegments.Length < patternSegments.Length)
                        {
                            isMatch = false;
                        }
                        else
                        {
                            for (int i = 0; i < patternSegments.Length; i++)
                            {
                                var pat = patternSegments[i];
                                var seg = entrySegments[i];

                                if (pat == "$ANY")
                                {
                                    continue;
                                }

                                if (!string.Equals(pat, seg, StringComparison.OrdinalIgnoreCase))
                                {
                                    isMatch = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (!isMatch)
                        continue;

                    var relativeSegments = entrySegments.Skip(patternSegments.Length).ToArray();
                    var relativePath = string.Join("/", relativeSegments).TrimStart('/');

                    if (string.IsNullOrEmpty(relativePath))
                        continue;

                    if (filesToSkip.Any(skip => relativePath.Contains(skip)))
                        continue;

                    var destinationPath = Path.Combine(extractTo, relativePath);
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    if (relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(destinationPath))
                    {
                        continue;
                    }

                    if (entry.FullName.EndsWith("/"))
                    {
                        if (!Directory.Exists(destinationPath))
                            Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        entry.ExtractToFile(destinationPath, overwrite: false);
                    }

                    extractedFiles++;
                }
            }
        }

        private void HandleError(Exception ex)
        {
            StageChanged?.Invoke("ErrorHappens");
            var codeObj = ErrorHelper.MapExceptionToCode(ex, out uint? hr);
            var code = codeObj.ToString();
            Logger.Instance.CreateErrorLog(nameof(ErrorHelper), $"{code} - {ex}");
            if (hr != null)
            {
                string hrHex = $"0x{hr.Value:X8}";
                ErrorHappens?.Invoke(Tuple.Create<string, string>($"ERR_NET_DOWNLOAD_{code} ({hrHex})", $"{ex}"));
            }
            else
            {
                ErrorHappens?.Invoke(Tuple.Create<string, string>($"ERR_NET_DOWNLOAD_{code}", $"{ex}"));
            }
        }
        public void Dispose()
        {
            source?.Cancel();
            source?.Dispose();
            _client.Dispose();
        }
    }
}
