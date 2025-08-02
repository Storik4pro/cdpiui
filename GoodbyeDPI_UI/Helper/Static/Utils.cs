using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            string localAppData = AppDomain.CurrentDomain.BaseDirectory;
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
    }
}

