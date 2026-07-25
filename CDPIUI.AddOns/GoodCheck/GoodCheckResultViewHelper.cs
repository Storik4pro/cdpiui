using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Core.Store.Data;
using CDPIUI.Shared.Extentions;
using System.Xml.Linq;

namespace CDPIUI.AddOns.GoodCheck
{
    public class StrategyModel
    {
        public bool Flag { get; set; } = false;
        public string Strategy { get; set; } = string.Empty;
        public string Success { get; set; } = "0";
        public string All { get; set; } = "0";
        public string Failure { get; set; } = "!NOT SET";
    }

    public class GroupModel
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public List<StrategyModel> Strategies { get; set; } = new List<StrategyModel>();
    }

    public class GoodCheckStrategiesList
    {
        public string? Name { get; set; }
        public string? FilePath { get; set; }
    }
    public class GoodCheckResultViewHelper
    {
        public static readonly Dictionary<Core.Store.Data.Components, string> ComponentNormalNames = new()
        {
            { Components.ByeDPI, "ciadpi" },
            { Components.GoodbyeDPI, "gdpi" },
            { Components.Zapret, "zapret" },
        };

        public GoodCheckResultViewHelper()
        {

        }

        public static List<GoodCheckStrategiesList> GetAvailableStrategiesLists(string componentId)
        {
            List<GoodCheckStrategiesList> strategiesLists = [];

            string targetFolder = 
                Path.Combine(Directories.StoreItemsDirectory, 
                HardcodedItemIds.AddOnsIds[Core.Store.Data.AddOns.GoodCheck], 
                "StrategiesGoGo", ComponentNormalNames[HardcodedItemIds.ComponentIds.GetKeyByValue(componentId)]
                );

            if (!Directory.Exists(targetFolder))
                return [];

            try
            {
                var files = Directory.EnumerateFiles(targetFolder, "*.txt", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    GoodCheckStrategiesList strategiesList = new()
                    {
                        Name = Path.GetFileName(file).Replace(".txt", ""),
                        FilePath = file,
                    };
                    strategiesLists.Add(strategiesList);
                }
            }
            catch
            {
                // pass
            }

            return strategiesLists;
        }

        public static Tuple<List<GroupModel>, string>? LoadGroupsFromFile(string xmlPath)
        {
            try
            {
                var doc = XDocument.Load(xmlPath);
                var root = doc.Root ?? throw new InvalidOperationException("ERR_INVALID_FILE");
                string componentId = string.Empty;

                var groups = root.Elements("Group")
                    .Select(g =>
                    {
                        var nameAttr = (string)g.Attribute("Name");
                        var groupName = !string.IsNullOrWhiteSpace(nameAttr) ? nameAttr : "UNKNOWN";

                        string qFullPath = (string)g.Attribute("FullPath");
                        string fullPath = !string.IsNullOrEmpty(qFullPath) ? qFullPath : "UNKNOWN";

                        componentId = (string)g.Attribute("ComponentId");
                        if (string.IsNullOrEmpty(componentId))
                            throw new InvalidOperationException("ERR_INVALID_FILE");

                        var strategies = g.Elements("StrategyResult")
                            .Select(sr =>
                            {
                                var flagText = ((string)sr.Element("Flag") ?? "").Trim();
                                bool flag = false;
                                if (flagText == "0" || flagText == "1")
                                    flag = flagText == "1";
                                else
                                    bool.TryParse(flagText, out flag);

                                return new StrategyModel
                                {
                                    Flag = flag,
                                    Strategy = (string)sr.Element("Strategy") ?? "UNKNOWN",
                                    Success = (string)sr.Element("Success") ?? "0",
                                    All = (string)sr.Element("All") ?? "0"
                                };
                            })
                            .ToList();

                        return new GroupModel
                        {
                            Name = groupName,
                            FullPath = fullPath,
                            Strategies = strategies
                        };
                    })
                    .ToList();

                if (string.IsNullOrEmpty(componentId))
                    throw new InvalidOperationException("ERR_INVALID_FILE");

                return Tuple.Create(groups, componentId);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(GoodCheckResultViewHelper), $"Unable to load .XML file. {ex.Message}");
                return null;
            }
        }
    }
}
