using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unidecode.NET;
using Windows.Devices.Printers;

namespace GoodbyeDPI_UI.Helper.Static
{
    public static class Utils
    {
        static Utils()
        {

        }

        public static string FormatSpeed(double speedInBytes)
        {
            string[] suffixes = { "байт/с", "КБ/с", "МБ/с", "ГБ/с", "ТБ/с" };

            int order = speedInBytes > 0
                ? Math.Min((int)Math.Floor(Math.Log(speedInBytes, 1024)), suffixes.Length - 1)
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
            if (min > 60)
            {
                double hours = min / 60;
                if (hours < 1.5)
                    return "Около часа";
                else if (hours >= 1.5 && hours <= 2.5)
                    return $"Около двух часов";
                else
                    return "Более трех часов (0_0)";
            } 
            else
            {
                if (min > 1)
                    return $"{min:F0} мин.";
                else if (min == 1)
                    return "Одна минута";
                else
                    return "Считанные секунды";
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
    }
}

