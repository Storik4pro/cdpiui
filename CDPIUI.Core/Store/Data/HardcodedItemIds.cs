namespace CDPIUI.Core.Store.Data
{
    public enum AddOns
    {
        GoodCheck,
    }
    public enum Components
    {
        Zapret,
        GoodbyeDPI,
        ByeDPI,
        SpoofDPI,
        NoDPI,
        TgWsProxy,
    }
    public class HardcodedItemIds
    {
        public static readonly Dictionary<AddOns, string> AddOnsIds = new()
        {
            { AddOns.GoodCheck, "ASGKOI001" },
        };

        public static readonly Dictionary<Components, string> ComponentIds = new()
        {
            { Components.Zapret, "CSZTBN012" },
            { Components.GoodbyeDPI, "CSGIVS036" },
            { Components.ByeDPI, "CSBIHA024" },
            { Components.SpoofDPI, "CSSIXC048" },
            { Components.NoDPI, "CSNIG9025" },
            { Components.TgWsProxy, "CSTYFL050" },
        };

        public static readonly List<Components> GoodCheckSupportedComponents = new()
        {
            { Components.GoodbyeDPI },
            { Components.ByeDPI },
            { Components.Zapret },
        };
    }
}
