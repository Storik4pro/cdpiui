using CDPI_UI.Views.CreateConfigHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPI_UI.Helper.CreateConfigHelper
{
    public class GraphicDesignerHelper
    {
        // DisplayName; LOCID; IsTextInputEnabled; DefaultValue
        private static List<Tuple<string, string, bool, string>> GoodbyeDPIDesignerConfig = new()
        {
            Tuple.Create("-p", "P", false, string.Empty),
            Tuple.Create("-q", "Q", false, string.Empty),
            Tuple.Create("-r", "R", false, string.Empty),
            Tuple.Create("-s", "S", false, string.Empty),
            Tuple.Create("-m", "M", false, string.Empty),
            Tuple.Create("-n", "N", false, string.Empty),
            Tuple.Create("-a", "A", false, string.Empty),
            Tuple.Create("-w", "W", false, string.Empty),
            Tuple.Create("--allow-no-sni", "NoSni", false, string.Empty),
            Tuple.Create("--dns-verb", "DnsVerb", false, string.Empty),
            Tuple.Create("--wrong-chksum", "WrongChksum", false, string.Empty),
            Tuple.Create("--wrong-seq", "WrongSeq", false, string.Empty),
            Tuple.Create("--native-frag", "NativeFrag", false, string.Empty),
            Tuple.Create("--reverse-frag", "ReverseFrag", false, string.Empty),
            Tuple.Create("-f", "F", true, "2"),
            Tuple.Create("-k", "K", true, "2"),
            Tuple.Create("-e", "E", true, "2"),
            Tuple.Create("--max-payload", string.Empty, true, "1200"),
            Tuple.Create("--port", string.Empty, true, "2710"),
            Tuple.Create("--ip-id", string.Empty, true, "54321"),
            Tuple.Create("--dns-addr", string.Empty, true, "77.88.8.8"),
            Tuple.Create("--dns-port", string.Empty, true, "1253"),
            Tuple.Create("--dnsv6-addr", string.Empty, true, "2a02:6b8::feed:0ff"),
            Tuple.Create("--dnsv6-port", string.Empty, true, "1253"),
            Tuple.Create("--set-ttl", string.Empty, true, "5"),
            Tuple.Create("--auto-ttl", string.Empty, true, "1"),
            Tuple.Create("--min-ttl", string.Empty, true, "3"),
            Tuple.Create("--fake-from-hex", string.Empty, true, "160301FFFF01FFFFFF0303594F5552204144564552544953454D454E542048455245202D202431302F6D6F000000000009000000050003000000"),
            Tuple.Create("--fake-gen", string.Empty, true, "5"),
            Tuple.Create("--fake-resend", string.Empty, true, "1"),
        };
        private static List<Tuple<string, string, bool, string>> SpoofDPIDesignerConfig = new()
        {
            Tuple.Create("-debug", "Debug", false, string.Empty),
            Tuple.Create("-enable-doh", "EnableDoH", false, string.Empty),
            Tuple.Create("-dns-ipv4-only", "DNSIpv4Only", false, string.Empty),
            Tuple.Create("-silent", "Silent", false, string.Empty),
            Tuple.Create("-system-proxy", "SystemProxy", false, string.Empty),
            Tuple.Create("-dns-addr", string.Empty, true, "1.1.1.1"),
            Tuple.Create("-dns-port", string.Empty, true, "53"),
            Tuple.Create("-pattern", string.Empty, true, string.Empty),
            Tuple.Create("-timeout", "Timeout", true, "10"),
            Tuple.Create("-window-size", "WindowSize", true, "0"),
        };


        public GraphicDesignerHelper() { }

        public static void LoadGoodbyeDPIDesignerConfig(ObservableCollection<GraphicDesignerSettingItemModel> list)
        {
            LoadDesignerConfig(GoodbyeDPIDesignerConfig, list);
        }

        public static void LoadSpoofDPIDesignerConfig(ObservableCollection<GraphicDesignerSettingItemModel> list)
        {
            LoadDesignerConfig(SpoofDPIDesignerConfig, list);
        }

        private static void LoadDesignerConfig(List<Tuple<string, string, bool, string>> config, ObservableCollection<GraphicDesignerSettingItemModel> list)
        {
            ILocalizer localizer = Localizer.Get();
            list.Clear();
            foreach (var tuple in config)
            {
                list.Add(new()
                {
                    Guid = Guid.NewGuid().ToString(),
                    DisplayName = tuple.Item1,
                    Description = string.IsNullOrEmpty(tuple.Item2) ? "" : localizer.GetLocalizedString($"/GraphicDesignerDescriptions/{tuple.Item2}"),
                    EnableTextInput = tuple.Item3,
                    Value = tuple.Item4,
                    IsChecked = false,
                });
            }
        }

        public static string ConvertStringToGraphicDesignerSettings(ObservableCollection<GraphicDesignerSettingItemModel> list, string args)
        {
            foreach (var item in list)
            {
                item.IsChecked = false;
            }

            string notSupportedFlags = string.Empty;
            string[] spl = args.Split(" ");
            for (int i = 0; i < spl.Length; i++)
            {
                string token = spl[i];
                string value = string.Empty;
                if (token.Contains('='))
                {
                    value = token.Split('=')[1];
                    token = token.Split('=')[0];
                }
                else if (spl.Length-1 >= i + 1 && !spl[i + 1].StartsWith('-') && !spl[i + 1].StartsWith('/'))
                {
                    
                    value = spl[i + 1];
                    Debug.WriteLine($"{token}, {value}");
                    i++;
                }

                var item = list.FirstOrDefault(x => x.DisplayName == token);
                if (item == null)
                {
                    notSupportedFlags += $"{token} ";
                    if (!string.IsNullOrEmpty(value)) notSupportedFlags += $"{value} ";
                }
                else
                {
                    item.IsChecked = true;
                    item.Value = value;
                }
            }

            return notSupportedFlags;
        }

        public static string ConvertGraphicDesignerSettingsToString(ObservableCollection<GraphicDesignerSettingItemModel> list, string additionalArgs)
        {
            string startupString = string.Empty;
            foreach (var item in list)
            {
                if (!item.IsChecked) continue;
                startupString += $"{item.DisplayName} ";
                if (item.EnableTextInput && !string.IsNullOrEmpty(item.Value)) startupString += $"{item.Value} ";

            }
            return $"{startupString}{additionalArgs}";
        }
    }
}
