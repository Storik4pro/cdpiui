using CDPIUI.Core.Store.Data;

namespace CDPIUI.Helper.Migration;

internal static class GoodbyeDpiComponentMapper
{
    public static MigrationComponentRequirement Map(MigrationPreset preset)
    {
        string source = (preset.Component ?? string.Empty).Trim().ToLowerInvariant();
        if (source is "unclassified" or "shared" or "")
            source = Infer(preset.CustomParameters);

        return source switch
        {
            "zapret" => new(source,
                HardcodedItemIds.ComponentIds[Components.Zapret],
                HardcodedItemIds.ComponentIds[Components.Zapret2]),
            "zapret2" => new(source,
                HardcodedItemIds.ComponentIds[Components.Zapret2],
                HardcodedItemIds.ComponentIds[Components.Zapret2]),
            "byedpi" => Direct(source, Components.ByeDPI),
            "spoofdpi" => Direct(source, Components.SpoofDPI),
            "nodpi" => Direct(source, Components.NoDPI),
            _ => Direct("goodbyedpi", Components.GoodbyeDPI)
        };
    }

    private static MigrationComponentRequirement Direct(string source, Components component)
    {
        string id = HardcodedItemIds.ComponentIds[component];
        return new(source, id, id);
    }

    private static string Infer(string? parameters)
    {
        string value = (parameters ?? string.Empty).ToLowerInvariant();
        if (value.Contains("--dpi-desync") || value.Contains("--wf-tcp") ||
            value.Contains("--filter-tcp") || value.Contains("--hostlist"))
            return "zapret";
        if (value.Contains("-ku") || value.Contains("-an") || value.Contains("-kt,") ||
            value.Contains("--disorder"))
            return "byedpi";
        if (value.Contains("-enable-doh") || value.Contains("-dns-addr") ||
            value.Contains("-window-size"))
            return "spoofdpi";
        return "goodbyedpi";
    }
}
