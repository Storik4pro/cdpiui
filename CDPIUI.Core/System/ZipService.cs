using CDPIUI.Core.Security;
using CDPIUI.Shared.Exceptions.Catalog;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core.System
{
    internal class ZipService
    {
        public static async Task ExtractZip(
            string zipFilePath,
            string? zipFolderToUnpack,
            string extractTo,
            IEnumerable<string>? filesToSkip = null,
            bool isCatalogCheckRequired = false
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
                    if (!Directory.Exists(destinationDir) && !string.IsNullOrEmpty(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    if (!isCatalogCheckRequired)
                    {
                        if (relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                            && File.Exists(destinationPath))
                        {
                            // This code is actually piece of shit. But, i so tired now to make something better. =(
                            if (File.Exists($"{destinationPath}.bak"))
                            {
                                destinationPath = $"{destinationPath}.bak";
                            }
                            // End of piece of shit
                            string destLines = File.ReadAllText(destinationPath);
                            string tmpFile = Path.Combine(destinationDir!, $"__TEMPFILE.txt");
                            entry.ExtractToFile(tmpFile, overwrite: true);

                            var stream = File.AppendText(destinationPath);

                            using (stream)
                            {
                                foreach (var line in File.ReadLines(tmpFile))
                                {
                                    if (!destLines.Contains(line))
                                    {
                                        await stream.WriteLineAsync(line);
                                    }
                                }
                            }
                            File.Delete(tmpFile);

                            continue;
                        }
                    }

                    if (entry.FullName.EndsWith("/"))
                    {
                        if (!Directory.Exists(destinationPath))
                            Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }

                    extractedFiles++;
                }
            }

            if (isCatalogCheckRequired)
            {
                CatalogCheckResult catalogCheckResult = await CertificateCheck.CheckCatalog(Path.Combine(extractTo, "catalog.cat"), extractTo);
                switch (catalogCheckResult)
                {
                    case CatalogCheckResult.Success:
                        return;
                    case CatalogCheckResult.FailureNoSignature:
                        throw new CatalogNoSignature("Catalog file isn't signed");
                    case CatalogCheckResult.FailureNotTrustedSignature:
                        throw new CatalogNoSignature("Signature not trusted");
                    case CatalogCheckResult.FailureNotValid:
                        throw new CatalogInvalid();
                    case CatalogCheckResult.FailureUnknown:
                        throw new CatalogInvalid("Unknown");

                }
            }
        }
    }
}
