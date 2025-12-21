using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unidecode.NET;
using Windows.Devices.Printers;
using WinUI3Localizer;

namespace CDPI_UI.Helper.Static
{
    public static class Utils
    {
        static Utils()
        {

        }

        public static string GetValueFromCommmandLineParameter(string commmandLineParameter)
        {
            string[] arguments = Environment.GetCommandLineArgs();

            for (int i = 0; i < arguments.Length; i++)
            {
                string[] parValPair = arguments[i].Split('=');
                if (parValPair.Length > 1)
                {
                    return parValPair[1];
                }
                else
                {
                    if (arguments.Length >= i+1 && !arguments[i+1].StartsWith("--"))
                    {
                        return arguments[i+1];
                    }
                }
            }
            return string.Empty;
        }

        public static string FormatSpeed(double speedInBytes)
        {
            List<string> suffixes = [];

            ILocalizer localizer = Localizer.Get();

            suffixes.Add(localizer.GetLocalizedString("/UIHelper/BytesPs"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/KiloBytesPs"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/MegaBytesPs"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/GBPs"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/TBPs"));

            int order = speedInBytes > 0
                ? Math.Min((int)Math.Floor(Math.Log(speedInBytes, 1024)), suffixes.Count - 1)
                : 0;

            double adjustedSpeed = speedInBytes / Math.Pow(1024, order);

            return $"{adjustedSpeed:0.##} {suffixes[order]}";
        }

        public static string SerializeTuples(List<Tuple<string, string>>? list)
        {
            string result = list == null ? null : string.Join(';', list.Select(t => $"{t.Item1}:{t.Item2}"));
            Logger.Instance.CreateDebugLog(nameof(Utils), result);
            return result;
        }

        public static List<Tuple<string, string>> DeserializeTuples(string data)
        {
            return data.Split(';', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => {
                           var parts = s.Split(':');
                           return Tuple.Create(parts[0], parts.Length > 1 ? parts[1] : "");
                       })
                       .ToList();
        }

        public static string StaticImageScript(string data)
        {
            return $"ms-appx:///Assets/{data}";
        }

        public static string DynamicPathConverter(string data)
        {
            string localAppData = StateHelper.GetDataDirectory();
            string targetFolder = Path.Combine(
                localAppData, StateHelper.StoreDirName, StateHelper.StoreRepoCache, StateHelper.StoreRepoDirName, data);
            return targetFolder;
        }

        public static string LoadAllTextFromFile(string filepath)
        {
            return File.ReadAllText(filepath);
        }

        public static T LoadJson<T>(string filepath)
        {
            string json = File.ReadAllText(filepath);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static string FirstCharToUpper(this string input) =>
        input switch
        {
            null => throw new ArgumentNullException(nameof(input)),
            "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
            _ => string.Concat(input[0].ToString().ToUpper(), input.AsSpan(1))
        };

        public static void OpenFileInDefaultApp(string filePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Path.GetFullPath(filePath),
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(Utils), $"Cannot open file with path \"{filePath}\" Because exception happens: {ex}");
            } 
        }

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

        private static string TransliterateAndSanitize(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            string transl = input.Unidecode();
            transl = Regex.Replace(transl, @"\s+", "_");
            transl = Regex.Replace(transl, "_+", "_").Trim('_');

            if (transl.Length > 200)
                transl = transl.Substring(0, 200);

            return transl;
        }

        public static string ConvertMinutesToPrettyText(double min)
        {
            ILocalizer localizer = Localizer.Get();
            if (min > 60)
            {
                double hours = min / 60;
                if (hours < 1.5)
                    return localizer.GetLocalizedString("/UIHelper/Hour");
                else if (hours >= 1.5 && hours <= 2.5)
                    return localizer.GetLocalizedString("/UIHelper/TwoHour");
                else
                    return localizer.GetLocalizedString("/UIHelper/MoreThanThreeHours");
            } 
            else
            {
                if (min > 1)
                    return $"{min:F0} {localizer.GetLocalizedString("/UIHelper/Min")}";
                else if (min == 1)
                    return localizer.GetLocalizedString("/UIHelper/OneMinute");
                else
                    return localizer.GetLocalizedString("/UIHelper/Sec");
            }
        }

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

        public static string GetStoreLikeLocale()
        {
            var localizer = WinUI3Localizer.Localizer.Get();
            switch (localizer.GetCurrentLanguage())
            {
                case "ru":
                    return "RU";
                case "en-US":
                    return "EN";
            }
            return "EN";
        }

        public static async Task ExtractZip(
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
                        string destLines = File.ReadAllText(destinationPath);
                        string tmpFile = Path.Combine(destinationDir, $"__TEMPFILE.txt");
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
        }

        public static List<string> Tokens = new() { "-p", "--port", "-i", "--ip", "-addr" };

        public static string ReplaseIp(string args)
        {
            string[] splittedArgs = args.Split(' ');
            string finalArgs = string.Empty;
            for (int i = 0; i < splittedArgs.Length; i++)
            {
                var spA = splittedArgs[i].Split("=");
                string token = spA[0];
                string value = spA.Length > 1 ? spA[1] + " " : string.Empty;
                if (Tokens.Contains(token))
                {
                    if (splittedArgs[i].Contains('='))
                    {
                        continue;
                    }
                    else
                    {
                        i++;
                        continue;
                    }
                }
                if (!string.IsNullOrEmpty(value))
                {
                    finalArgs += $"{token}={value}";
                }
                else
                {
                    finalArgs += $"{token} ";
                }

            }
            return finalArgs;
        }

        public static string NormalizeComponentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            return name.Replace("dpi", "DPI", StringComparison.OrdinalIgnoreCase).FirstCharToUpper();
        }

#if SINGLEFILE
        public static bool IsApplicationBuildAsSingleFile = true;
        public static bool IsApplicationBuildAsMsi = false;
#elif MSIFILE
        public static bool IsApplicationBuildAsSingleFile = false;
        public static bool IsApplicationBuildAsMsi = true;
#elif Release
        public static bool IsApplicationBuildAsSingleFile = true;
        public static bool IsApplicationBuildAsMsi = false;
#else 
        public static bool IsApplicationBuildAsSingleFile = false;
        public static bool IsApplicationBuildAsMsi = false;
#endif
    }
}

