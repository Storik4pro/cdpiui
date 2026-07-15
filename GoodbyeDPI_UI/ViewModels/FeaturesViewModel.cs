using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.ViewModels
{

    public class FeaturesViewModel
    {
        public string DisplayName { get; set; }
        public AvailableComponentFeatures Id { get; set; }
        public Uri Image { get; set; }
    }
    public enum AvailableComponentFeatures
    {
        SetupProxy,
        AutoSelectConfig,
        CreateConfig,
        ExploreNewConfigs,
        VisitForum,
        ConnectTgWsProxy,
        ViewOutput
    }
    public static class FeaturesData
    {
        public static Dictionary<AvailableComponentFeatures, string> AvailableComponentFeatureImages = new()
        {
            { AvailableComponentFeatures.SetupProxy, "ms-appx:///Assets/Icons/Proxy.ico" },
            { AvailableComponentFeatures.AutoSelectConfig, "ms-appx:///Assets/Icons/GoodCheck.png" },
            { AvailableComponentFeatures.CreateConfig, "ms-appx:///Assets/Icons/Edit.png" },
            { AvailableComponentFeatures.ExploreNewConfigs, "ms-appx:///Assets/Icons/Store.png" },
            { AvailableComponentFeatures.VisitForum, "ms-appx:///Assets/Icons/OpenInNewWindow.png" },
            { AvailableComponentFeatures.ConnectTgWsProxy, "ms-appx:///Assets/Icons/Proxy.ico" },
            { AvailableComponentFeatures.ViewOutput, "ms-appx:///Assets/Icons/Pseudoconsole.ico" },
        };

        public static Dictionary<string, List<AvailableComponentFeatures>> AvailableFeaturesForComponent = new()
        {
            { "CSZTBN012", [AvailableComponentFeatures.AutoSelectConfig, AvailableComponentFeatures.ExploreNewConfigs, AvailableComponentFeatures.VisitForum] },
            { "CSGIVS036", [AvailableComponentFeatures.CreateConfig, AvailableComponentFeatures.ExploreNewConfigs, AvailableComponentFeatures.AutoSelectConfig] },
            { "CSBIHA024", [AvailableComponentFeatures.SetupProxy, AvailableComponentFeatures.AutoSelectConfig, AvailableComponentFeatures.CreateConfig] },
            { "CSSIXC048", [AvailableComponentFeatures.SetupProxy, AvailableComponentFeatures.CreateConfig, AvailableComponentFeatures.ExploreNewConfigs] },
            { "CSNIG9025", [AvailableComponentFeatures.SetupProxy, AvailableComponentFeatures.CreateConfig] },
            { "CSTYFL050", [AvailableComponentFeatures.VisitForum, AvailableComponentFeatures.ConnectTgWsProxy] },
        };
    }
}
