using CDPI_UI.Views.CreateConfigHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using WinUI3Localizer;

namespace CDPI_UI.Helper.CreateConfigHelper
{

    public class GraphicDesignerHelper
    {
        private class XML_GraphicDesignerConfigItemModel
        {
            public string ShortCommandLineParameter { get; set; }
            public string LongCommandLineParameter { get; set; }
            public string ArgumentPlaceholder { get; set; }
            public string DisplayDescription { get; set; }
            public string Type { get; set; }
            public List<EnumModel> AvailableValues { get; set; }
            public string DefaultValue { get; set; }
        }
        private class XML_GraphicDesignerConfigExclusiveGroup
        {
            public List<XML_GraphicDesignerConfigItemModel> ItemModels { get; set; }
        }
        private class XML_GraphicDesignerConfigOption<T>
        {
            public bool IsExclusiveGroup { get; set; }
            public XML_GraphicDesignerConfigItemModel Value { get; set; }
            public XML_GraphicDesignerConfigExclusiveGroup ExclusiveValue { get; set; }
        }

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

        public static void XML_LoadDesignerConfig(string filePath, string componentName, ObservableCollection<GraphicDesignerSettingItemModel> list, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> exList)
        {
            ILocalizer localizer = Localizer.Get();
            list.Clear();
            exList.Clear();
            if (!File.Exists(filePath)) return;
            
            var dox = XDocument.Load(filePath);
            List<XML_GraphicDesignerConfigItemModel> lst = dox.Root.Element("options")?.Elements("option")?.Select(d =>
                new XML_GraphicDesignerConfigItemModel
                {
                    ShortCommandLineParameter = d.Element("short")?.Value,
                    LongCommandLineParameter = d.Element("long")?.Value,
                    ArgumentPlaceholder = d.Element("argument")?.Value,
                    DisplayDescription = d.Element("description")?.Value,
                    Type = d.Element("type")?.Value,
                    AvailableValues = ConvertListToEnumModel(d.Element("values")?.Elements("value")?.Select(d => d.Value?.ToString())?.ToList()),
                    DefaultValue = d.Element("default")?.Value
                }).ToList();

            if (lst == null) return;

            foreach (XML_GraphicDesignerConfigItemModel item in lst)
            {
                string localizedString = localizer.GetLocalizedString($"/GraphicDesignerDescriptions/{item.LongCommandLineParameter}");
                list.Add(new()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Type = item.Type,
                    AvailableEnumValues = item.AvailableValues,
                    DisplayName = item.LongCommandLineParameter,
                    Description = string.IsNullOrEmpty(localizedString) ? item.DisplayDescription : localizedString,
                    EnableTextInput = item.Type == "string",
                    
                    Value = item.DefaultValue,
                    IsChecked = false,
                });
            }

            List<XML_GraphicDesignerConfigExclusiveGroup> excluziveLst = dox.Root.Element("options")?.Elements("mutually_exclusive_group")?.Select(ex =>
                new XML_GraphicDesignerConfigExclusiveGroup
                {
                    ItemModels = ex.Elements("option")?.Select(d => 
                    new XML_GraphicDesignerConfigItemModel
                    {
                        ShortCommandLineParameter = d.Element("short")?.Value,
                        LongCommandLineParameter = d.Element("long")?.Value,
                        ArgumentPlaceholder = d.Element("argument")?.Value,
                        DisplayDescription = d.Element("description")?.Value,
                        Type = d.Element("type")?.Value,
                        AvailableValues = ConvertListToEnumModel(d.Element("values")?.Elements("value")?.Select(d => d.Value?.ToString())?.ToList()),
                        DefaultValue = d.Element("default")?.Value
                    }).ToList() ?? []
                    
                }).ToList();

            foreach (var ex in excluziveLst)
            {
                ObservableCollection<GraphicDesignerSettingItemModel> l = [];
                foreach (var item in ex.ItemModels)
                {
                    string localizedString = localizer.GetLocalizedString($"/GraphicDesignerDescriptions/{item.LongCommandLineParameter}");
                    if (item.LongCommandLineParameter != "--install" && item.LongCommandLineParameter != "--uninstall")
                    {
                        l.Add(new()
                        {
                            Guid = Guid.NewGuid().ToString(),
                            Type = item.Type,
                            AvailableEnumValues = item.AvailableValues,
                            DisplayName = item.LongCommandLineParameter,
                            Description = string.IsNullOrEmpty(localizedString) ? item.DisplayDescription : localizedString,
                            EnableTextInput = item.Type == "string",

                            Value = item.DefaultValue,
                            IsChecked = false,
                        });
                    }
                }
                if (l.Count > 0)
                {
                    exList.Add(new()
                    {
                        Guid = Guid.NewGuid().ToString(),
                        DisplayName = string.Empty,
                        Items = l
                    });
                }
            }

        }

        private static List<EnumModel> ConvertListToEnumModel(List<string> list)
        {
            ILocalizer localizer = Localizer.Get();
            List<EnumModel> m = [];
            if (list != null)
            {
                foreach (string val in list)
                {
                    m.Add(new()
                    {
                        DisplayName = string.IsNullOrEmpty(localizer.GetLocalizedString(val)) ? val : localizer.GetLocalizedString(val),
                        ActualValue = val,
                    });
                }
            }
            return m;
        }
        public static void LoadGoodbyeDPIDesignerConfig(ObservableCollection<GraphicDesignerSettingItemModel> list, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> exList)
        {
            exList.Clear();
            LoadDesignerConfig(GoodbyeDPIDesignerConfig, list);
        }

        public static void LoadSpoofDPIDesignerConfig(ObservableCollection<GraphicDesignerSettingItemModel> list, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> exList)
        {
            exList.Clear();
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
                    Type = tuple.Item3 ? "string" : "flag",
                    Value = tuple.Item4,
                    IsChecked = false,
                });
            }
        }

        public static string ConvertStringToGraphicDesignerSettings(ObservableCollection<GraphicDesignerSettingItemModel> list, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> exList, string args)
        {
            foreach (var item in list)
            {
                item.IsChecked = false;
            }
            foreach (var _exItem in exList)
            {
                foreach (var item in _exItem.Items)
                {
                    item.IsChecked = false;
                }
            }

            string notSupportedFlags = string.Empty;
            var tokenPattern = @"(?<=\s|^)(?:(?:(?:--|/)[^\s]*=""[^""]*"")|""[^""]*""|[^ ]+)+";
            var tokens = Regex.Matches(args, tokenPattern)
                              .Cast<Match>()
                              .Select(m => m.Value.Trim('"'))
                              .ToList();

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                string value = string.Empty;
                if (token.Contains('='))
                {
                    value = token.Split('=')[1];
                    token = token.Split('=')[0];
                }
                else if (tokens.Count-1 >= i + 1 && !tokens[i + 1].StartsWith('-') && !tokens[i + 1].StartsWith('/'))
                {
                    
                    value = tokens[i + 1];
                    
                    i++;
                }               

                var item = list.FirstOrDefault(x => x.DisplayName == token);
                if (item == null)
                {
                    bool flag = false;
                    foreach (var _exModel in exList)
                    {
                        item = _exModel.Items.FirstOrDefault(x => x.DisplayName == token);
                        if (item != null)
                        {
                            item.IsChecked = true;
                            if (value.StartsWith("\"")) value += "\"";
                            item.Value = value;
                            _exModel.SelectedItemGuid = item.Guid;
                            flag = true;
                            break;
                        }
                    }
                    if (!flag)
                    {
                        notSupportedFlags += $"{token} ";
                        if (!string.IsNullOrEmpty(value)) notSupportedFlags += $"{value} ";
                    }
                }
                else
                {
                    item.IsChecked = true;
                    if (value.StartsWith("\"")) value += "\"";
                    item.Value = value;
                }
            }

            return notSupportedFlags;
        }

        private static readonly List<string> TextSupportedValues = ["string", "integer", "file_path", "enum"];

        

        public static string ConvertGraphicDesignerSettingsToString(ObservableCollection<GraphicDesignerSettingItemModel> list, ObservableCollection<GraphicDesignerExclusiveSettingItemModel> exList, string additionalArgs)
        {
            string startupString = string.Empty;
            foreach (var item in list)
            {
                (bool flowControl, startupString) = ProcessItemActions(startupString, item);
                if (!flowControl)
                {
                    continue;
                }
            }
            foreach (var itemGroup in exList)
            {
                var item = itemGroup.Items.FirstOrDefault(x => x.Guid == itemGroup.SelectedItemGuid);
                if (item != null)
                {
                    (bool flowControl, startupString) = ProcessItemActions(startupString, item);
                    if (!flowControl)
                    {
                        continue;
                    }
                }
            }
            return $"{startupString}{additionalArgs}";
        }

        private static (bool flowControl, string value) ProcessItemActions(string startupString, GraphicDesignerSettingItemModel item)
        {
            if (!item.IsChecked && item.Type != "enum") return (flowControl: false, value: startupString);
            startupString += $"{item.DisplayName}";
            if ((item.EnableTextInput || TextSupportedValues.Contains(item.Type)) && !string.IsNullOrEmpty(item.Value)) startupString += $"={item.Value}";
            startupString += " ";
            return (flowControl: true, value: startupString);
        }

    }
}
