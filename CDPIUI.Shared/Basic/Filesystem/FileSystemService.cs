using CDPIUI.Shared.Logger;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CDPIUI.Shared.Basic.Filesystem
{
    public enum FileExtentionTypes
    {
        none,
        archive,
        configPack,
        signedZip,
        WIN32application,
        CDPIUIUpdateItem,
        msi,
        UPDmsi,
        elmsi,
        lst,
        raw,
        temp
    }

    public class FileSystemService
    {
        /// <summary>
        /// File extentions by <see cref="FileExtentionTypes"/> without dots
        /// </summary>
        public static Dictionary<FileExtentionTypes, string> FileExtentions = new()
        {
            { FileExtentionTypes.none, "" },
            { FileExtentionTypes.archive, "zip" },
            { FileExtentionTypes.configPack, "cdpiconfigpack" },
            { FileExtentionTypes.signedZip, "cdpisignedpack" },
            { FileExtentionTypes.WIN32application, "exe" },
            { FileExtentionTypes.CDPIUIUpdateItem, "cdpipatch" },
            { FileExtentionTypes.msi, "msi" },
            { FileExtentionTypes.UPDmsi, "msi" },
            { FileExtentionTypes.elmsi, "exe" },
            { FileExtentionTypes.lst, "lst" },
            { FileExtentionTypes.raw, "txt" },
            { FileExtentionTypes.temp, "cdpitempfile" },
        };

        /// <summary>
        /// Get file extention with dot
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>.extention</returns>
        public static string GetFileExtention(FileExtentionTypes type)
        {
            return $".{FileExtentions.FirstOrDefault(x => x.Key == type).Value}";
        }

        /// <summary>
        /// Archive-like compressed file types
        /// </summary>
        public static List<FileExtentionTypes> CompressedFileTypes =
            [FileExtentionTypes.signedZip, FileExtentionTypes.archive, FileExtentionTypes.configPack];

        
        private readonly static Uri SomeBaseUri = new("https://canbeanything");
        /// <summary>
        /// Gets file name from url
        /// https://stackoverflow.com/a/40361205
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>File name with extention if url correct, otherwise <see cref="string.Empty"/></returns>
        public static string GetFileNameFromUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                uri = new Uri(SomeBaseUri, url);

            return Path.GetFileName(uri.LocalPath);
        }

        /// <summary>
        /// Get default unique temp file name.
        /// </summary>
        /// <returns>Temp file name</returns>
        public static string GetNewTempFileName()
        {
            return GetNewTempFileName("", ".cdpitempfile");
        }

        /// <summary>
        /// Get unique temp file/folder name. Format 
        /// <code>
        /// secondsSinceEpoch_suffix(.extention)
        /// </code>
        /// </summary>
        /// <param name="suffix">Suffix of file</param>
        /// <param name="extention">Extention. Leave emty for no extention</param>
        /// <returns>Temp file/folder name</returns>
        public static string GetNewTempFileName(string suffix, string? extention = null)
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;

            if (string.IsNullOrEmpty(extention)) return $"{secondsSinceEpoch}_{suffix}";
            return $"{secondsSinceEpoch}_{suffix}.{extention}";
        }

        /// <summary>
        /// Copy file to folder asynchronically
        /// </summary>
        /// <param name="sourcePath">Source file path</param>
        /// <param name="destinationPath">Copy destination</param>
        public static async Task CopyFileAsync(string sourcePath, string destinationPath)
        {
            using Stream source = File.OpenRead(sourcePath);
            using Stream destination = File.Create(destinationPath);
            await source.CopyToAsync(destination);
        }

        /// <summary>
        /// Get file size in bytes
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <param name="logger">Logger instance</param>
        /// <returns>Bytes count</returns>
        public static long GetFileSize(string filePath, ILogger? logger = null)
        {
            long fileSize = 0;
            try
            {
                FileInfo info = new FileInfo(filePath);
                uint dummy, sectorsPerCluster, bytesPerSector;
                int result = GetDiskFreeSpaceW(info.Directory.Root.FullName, out sectorsPerCluster, out bytesPerSector, out dummy, out dummy);
                if (result == 0) throw new Win32Exception();
                uint clusterSize = sectorsPerCluster * bytesPerSector;
                uint hosize;
                uint losize = GetCompressedFileSizeW(filePath, out hosize);
                long size;
                size = (long)hosize << 32 | losize;
                return ((size + clusterSize - 1) / clusterSize) * clusterSize;
            }
            catch (Exception ex)
            {
                logger?.CreateWarningLog(nameof(FileSystemService), $"Unable to calculate size for \"{filePath}\". {ex.Message}");
            }
            return fileSize;
        }

        /// <summary>
        /// Get directory size in bytes
        /// </summary>
        /// <param name="directory">Directory path</param>
        /// <param name="logger">Logger instance</param>
        /// <returns>Bytes count</returns>
        public static async Task<long> GetDirectorySize(string directory, ILogger? logger = null)
        {
            long dirSize = 0;
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(directory);
                dirSize = await Task.Run(() => dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length));
            }
            catch (Exception ex)
            {
                logger?.CreateWarningLog(nameof(FileSystemService), $"Unable to calculate size for \"{directory}\". {ex.Message}");
            }
            return dirSize;
        }

        /// <summary>
        /// Gets folder names up to stop folder base
        /// </summary>
        /// <param name="path">Target path</param>
        /// <param name="stopFolderName">Stop base</param>
        /// <returns>New folder name</returns>
        public static string GetFolderNamesUpTo(string path, string stopFolderName)
        {
            if (File.Exists(path))
                path = Path.GetDirectoryName(path);

            var dir = new DirectoryInfo(path ?? "");
            var result = new List<string>();

            while (dir != null)
            {
                if (string.Equals(dir.Name, stopFolderName, StringComparison.OrdinalIgnoreCase))
                    break;

                result.Add(dir.Name);
                dir = dir.Parent;
            }
            result.Reverse();
            return string.Join("/", result);
        }

        /// <summary>
        /// Normalize directory
        /// </summary>
        /// <param name="dir">Target directory</param>
        /// <returns>Normalized directory</returns>
        public static string NormalizeDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return string.Empty;
            try
            {
                var full = Path.GetFullPath(dir);
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return dir.Trim().TrimEnd('\\', '/');
            }
        }

        /// <summary>
        /// Copy file to destination with unique name
        /// </summary>
        /// <param name="sourcePath">File to copy</param>
        /// <param name="destinationDir">Copy destination</param>
        /// <returns>Path to file in destination directory</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public static string CopyTxtWithUniqueName(string sourcePath, string destinationDir)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("sourcePath is null or empty", nameof(sourcePath));
            if (string.IsNullOrWhiteSpace(destinationDir))
                throw new ArgumentException("destinationDir is null or empty", nameof(destinationDir));
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source file not found", sourcePath);

            Directory.CreateDirectory(destinationDir);

            string extension = Path.GetExtension(sourcePath) ?? "";

            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string safeBase = TransliterateAndSanitize(baseName);

            if (string.IsNullOrEmpty(safeBase))
                safeBase = "file";

            string candidate = safeBase + extension;
            string destPath = Path.Combine(destinationDir, candidate);

            bool plainExists = File.Exists(Path.Combine(destinationDir, safeBase + extension));
            int maxIndex = 0;
            string pattern = "^" + Regex.Escape(safeBase) + @"_(\d+)" + Regex.Escape(extension) + "$";
            Regex rex = new Regex(pattern, RegexOptions.IgnoreCase);

            foreach (var file in Directory.EnumerateFiles(destinationDir))
            {
                string f = Path.GetFileName(file);
                var m = rex.Match(f);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int val))
                {
                    if (val > maxIndex) maxIndex = val;
                }
            }

            if (!plainExists && maxIndex == 0)
            {
                destPath = Path.Combine(destinationDir, safeBase + extension);
                if (File.Exists(destPath))
                {
                    int i = 1;
                    do
                    {
                        destPath = Path.Combine(destinationDir, $"{safeBase}_{i}{extension}");
                        i++;
                    } while (File.Exists(destPath));
                }
            }
            else
            {
                int newIndex = (maxIndex > 0) ? (maxIndex + 1) : 1;
                destPath = Path.Combine(destinationDir, $"{safeBase}_{newIndex}{extension}");
                while (File.Exists(destPath))
                {
                    newIndex++;
                    destPath = Path.Combine(destinationDir, $"{safeBase}_{newIndex}{extension}");
                }
            }

            File.Copy(sourcePath, destPath);
            return destPath;
        }

        private static string TransliterateAndSanitize(string input) // TODO: check is it work
        {
            if (string.IsNullOrEmpty(input))
                return "";

            string transl = TransliterateToAscii(input);
            transl = Regex.Replace(transl, @"\s+", "_");
            transl = Regex.Replace(transl, "_+", "_").Trim('_');

            if (transl.Length > 200)
                transl = transl.Substring(0, 200);

            return transl;
        }

        private static string TransliterateToAscii(string input)
        {
            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        #region WINAPI
        [DllImport("kernel32.dll")]
        static extern uint GetCompressedFileSizeW([In, MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
            [Out, MarshalAs(UnmanagedType.U4)] out uint lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true, PreserveSig = true)]
        static extern int GetDiskFreeSpaceW([In, MarshalAs(UnmanagedType.LPWStr)] string lpRootPathName,
           out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters,
           out uint lpTotalNumberOfClusters);
        #endregion
    }
}
