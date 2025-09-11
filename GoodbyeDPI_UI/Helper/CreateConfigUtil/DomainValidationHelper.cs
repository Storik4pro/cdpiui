using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CDPI_UI.Helper.CreateConfigUtil
{
    public static class DomainValidationHelper
    {
        public enum CheckMode
        {
            None,
            Quick,
            Slow
        }

        public static async Task<List<string>> GetSupportedTxtFiles(
            string rootDirectory, 
            CheckMode mode = CheckMode.Slow, 
            int maxConcurrency = 8, 
            CancellationToken cancellationToken = default)
        {
            var supported = new List<string>();

            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                Logger.Instance.CreateWarningLog(nameof(DomainValidationHelper), $"Directory not found: {rootDirectory}");
                return [];
            }

            var files = Directory.EnumerateFiles(rootDirectory, "*.txt", SearchOption.AllDirectories);

            using var sem = new SemaphoreSlim(Math.Max(1, maxConcurrency));
            var tasks = new List<Task>();

            foreach (var file in files)
            {
                await sem.WaitAsync(cancellationToken).ConfigureAwait(false);

                var t = Task.Run(async () =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        bool isSupported = await IsFileSupportedAsync(file, mode, cancellationToken).ConfigureAwait(false);

                        if (isSupported)
                        {
                            lock (supported)
                            {
                                supported.Add(file);
                            }
                        }
                    }
                    finally
                    {
                        sem.Release();
                    }
                }, cancellationToken);

                tasks.Add(t);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return supported;
        }

        public static async Task<bool> IsFileCorrectSiteList(string filePath, CheckMode mode, CancellationToken cancellationToken = default)
        {
            return await IsFileSupportedAsync(filePath, mode, cancellationToken);
        }

        private static async Task<bool> IsFileSupportedAsync(string filePath, CheckMode mode, CancellationToken cancellationToken)
        {
            if (mode == CheckMode.None)
            {
                try
                {
                    using var fs = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 4096,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);

                    var buffer = new byte[1];
                    int read = await fs.ReadAsync(buffer, 0, 0, cancellationToken).ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            const int quickLimit = 10;
            int nonEmptyChecked = 0;
            bool validFile = true;

            try
            {
                using var fs = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 8192,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                using var reader = new StreamReader(fs);

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string raw = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (raw == null)
                        break;

                    string line = raw.Trim();

                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    if (mode == CheckMode.Quick)
                    {
                        nonEmptyChecked++;
                    }

                    if (Regex.IsMatch(line, @"\s"))
                    {
                        validFile = false;
                        break;
                    }

                    if (line.Contains("/"))
                    {
                        validFile = false;
                        break;
                    }

                    if (IPAddress.TryParse(line, out _))
                    {
                        validFile = false;
                        break;
                    }

                    if (mode == CheckMode.Quick && nonEmptyChecked >= quickLimit)
                    {
                        break;
                    }
                }
            }
            catch
            {
                Logger.Instance.CreateInfoLog(nameof(DomainValidationHelper), $"File {filePath} is not supported sitelist.");
                return false;
            }

            return validFile;
        }

    }
}
