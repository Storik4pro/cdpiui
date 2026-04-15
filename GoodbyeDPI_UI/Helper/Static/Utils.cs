using CDPI_UI.Common;
using CDPI_UI.Controls.Dialogs.ComponentSettings;
using CDPI_UI.DataModel;
using CDPI_UI.Helper.Items;
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
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unidecode.NET;
using Windows.Devices.Printers;
using WinUI3Localizer;
using static CDPI_UI.Common.CertificateCheck;

namespace CDPI_UI.Helper.Static
{
    public static partial class Utils
    {
        static Utils()
        {

        }

        public class LogicalComparer : IComparer<ConfigItem>
        {
            public int Compare(ConfigItem x, ConfigItem y)
            {
                return StrCmpLogicalW(x.name.Normalize(), y.name.Normalize());
            }
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            private static extern int StrCmpLogicalW(string s1, string s2);
        }

        public static string GetValueFromCommmandLineParameter(string commmandLineParameter)
        {
            string[] arguments = Environment.GetCommandLineArgs();

            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].StartsWith(commmandLineParameter))
                {
                    string[] parValPair = arguments[i].Split('=');
                    if (parValPair.Length > 1)
                    {
                        return parValPair[1];
                    }
                    else
                    {
                        if (arguments.Length >= i + 1 && !arguments[i + 1].StartsWith("--"))
                        {
                            return arguments[i + 1];
                        }
                    }
                }
            }
            return string.Empty;
        }

        public static string FormatSize(long sizeInBytes)
        {
            List<string> suffixes = [];

            ILocalizer localizer = Localizer.Get();

            suffixes.Add(localizer.GetLocalizedString("/UIHelper/Bytes"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/KiloBytes"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/MegaBytes"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/GB"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/TB"));

            int order = sizeInBytes > 0
                ? Math.Min((int)Math.Floor(Math.Log(sizeInBytes, 1024)), suffixes.Count - 1)
                : 0;

            double adjustedSize = sizeInBytes / Math.Pow(1024, order);

            return $"{adjustedSize:0.#} {suffixes[order]}";
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

        public static string DynamicPathConverter(string data, string args = "")
        {
            if (!string.IsNullOrEmpty(args))
            {
                return Path.Combine(args, data);
            }
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

        public static void OpenFolderInExplorer(string dir)
        {
            try
            {
                Process.Start("explorer.exe", $"\"{dir.Replace("/", "\\")}\"");
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(Utils), $"Cannot open path \"{dir}\" Because exception happens: {ex}");
            }
        }

        public static void RunApp(string executable, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo(executable, arguments)
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(Utils), $"Cannot open application '{executable}' with arguments '{arguments}', because exception happens: {ex.Message}");
            }
        }

        public static void OpenFile(string file)
        {
            int openMode = SettingsManager.Instance.GetValue<int>("FILEOPENACTIONS", "mode");
            string appPath = SettingsManager.Instance.GetValue<string>("FILEOPENACTIONS", "applicationPath");
            if (openMode == (int)TextFileOpenModes.UserChoose && File.Exists(appPath))
            {
                Utils.RunApp(appPath, $"\"{file}\"");
            }
            else
            {
                Utils.OpenFileInDefaultApp(file);
            }
        }

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

        public static async Task CopyFileAsync(string sourcePath, string destinationPath)
        {
            using Stream source = File.OpenRead(sourcePath);
            using Stream destination = File.Create(destinationPath);
            await source.CopyToAsync(destination);
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

        public static string CalculateSHA256(string filename)
        {
            using (var md5 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "");
                }
            }
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

        public static async Task<long> GetDirectorySize(string directory)
        {
            long dirSize = 0;
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(directory);
                dirSize = await Task.Run(() => dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length));
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(Utils), $"Unable to calculate size for \"{directory}\". {ex.Message}");
            }
            return dirSize;
        }

        public static async Task ExtractZip(
            string zipFilePath,
            string zipFolderToUnpack,
            string extractTo,
            IEnumerable<string> filesToSkip = null,
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
                    if (!Directory.Exists(destinationDir))
                        Directory.CreateDirectory(destinationDir);

                    if (!isCatalogCheckRequired)
                    {
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
                CatalogCheckResult catalogCheckResult = await CheckCatalog(Path.Combine(extractTo, "catalog.cat"), extractTo);
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

        public static List<string> Tokens = new() { "-p", "--port", "-i", "--ip", "-addr", "--host" };

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

        // https://stackoverflow.com/a/40361205
        readonly static Uri SomeBaseUri = new Uri("https://canbeanything");
        public static string GetFileNameFromUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                uri = new Uri(SomeBaseUri, url);

            return Path.GetFileName(uri.LocalPath);
        }

        public static bool IsOsSupportedNewGlyph()
        {
            Debug.WriteLine(Environment.OSVersion.ToString());
            var version1 = Environment.OSVersion.Version;
            string v2 = "10.0.22000.194";

            var version2 = new Version(v2);
            if (version1 >= version2) return true;
            return false;
        }

        public static bool IsVersionCorrect(string version)
        {
            return Version.TryParse(version, out var _);
        }

        public static bool IsIdCorrect(string id)
        {
            return CheckIdRegex().IsMatch(id);
        }

        public static string GenerateNewId()
        {
            return Guid.NewGuid().ToString().Replace("{", "").Replace("}", "");
        }

        static Random random = new Random();
        public static string GetRandomHexNumber(int digits)
        {
            byte[] buffer = new byte[digits / 2];
            random.NextBytes(buffer);
            string result = String.Concat(buffer.Select(x => x.ToString("X2")).ToArray());
            if (digits % 2 == 0)
                return result;
            return result + random.Next(16).ToString("X");
        }

        public static int CompareVersionStrings(string oldVersion, string newVersion)
        {
            if (oldVersion == "%CURRENT%") return 0;

            if (oldVersion.StartsWith('v')) oldVersion = oldVersion[1..];
            if (newVersion.StartsWith('v')) newVersion = newVersion[1..];

            if (oldVersion.Contains("rc")) oldVersion = oldVersion.Replace("rc", "-rc");
            if (newVersion.Contains("rc")) newVersion = newVersion.Replace("rc", "-rc");

            if (Semver.SemVersion.TryParse(oldVersion, out var oldSemVersion) && Semver.SemVersion.TryParse(newVersion, out var newSemVersion))
            {
                return Semver.SemVersion.ComparePrecedence(oldSemVersion, newSemVersion);
            }
            else if (Version.TryParse(oldVersion, out var oldVerVersion) &&  Version.TryParse(newVersion,out var newVerVersion))
            {
                if (oldVerVersion < newVerVersion) return -1;
                else if (oldVerVersion > newVerVersion) return 1;
                else return 0;
            }

            Logger.Instance.CreateErrorLog(nameof(Utils), $"Cannot compare {oldVersion} and {newVersion}.");
            return 0;
        }

        [GeneratedRegex(@"^[a-zA-Z0-9\-]+$")]
        private static partial Regex CheckIdRegex();

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

