using System;
using System.Collections.Generic;

namespace CDPIUI.Helper.CreateConfigHelper;

internal static class Zapret2OptionDescriptionProvider
{
    private static readonly IReadOnlyDictionary<string, string> OptionResourceKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--debug"] = "ConfigMakerZapret2DebugDescription",
            ["--dry-run"] = "ConfigMakerZapret2DryRunDescription",
            ["--intercept"] = "ConfigMakerZapret2InterceptDescription",
            ["--reasm-disable"] = "ConfigMakerZapret2ReassemblyDescription",
            ["--blob"] = "ConfigMakerZapret2BlobDescription",
            ["--lua-init"] = "ConfigMakerZapret2LuaInitDescription",
            ["--lua-gc"] = "ConfigMakerZapret2LuaGcDescription",
            ["--new"] = "ConfigMakerZapret2NewProfileDescription",
            ["--skip"] = "ConfigMakerZapret2SkipProfileDescription",
            ["--name"] = "ConfigMakerZapret2ProfileNameDescription",
            ["--template"] = "ConfigMakerZapret2TemplateDescription",
            ["--import"] = "ConfigMakerZapret2ImportDescription",
            ["--cookie"] = "ConfigMakerZapret2CookieDescription",
            ["--filter-l3"] = "ConfigMakerZapret2FilterL3Description",
            ["--filter-tcp"] = "ConfigMakerZapret2TransportFilterDescription",
            ["--filter-udp"] = "ConfigMakerZapret2TransportFilterDescription",
            ["--filter-icmp"] = "ConfigMakerZapret2IcmpFilterDescription",
            ["--filter-ipp"] = "ConfigMakerZapret2IpProtocolFilterDescription",
            ["--filter-l7"] = "ConfigMakerZapret2FilterL7Description",
            ["--ipset"] = "ConfigMakerZapret2IpSetDescription",
            ["--ipset-ip"] = "ConfigMakerZapret2IpSetDescription",
            ["--ipset-exclude"] = "ConfigMakerZapret2IpSetExcludeDescription",
            ["--ipset-exclude-ip"] = "ConfigMakerZapret2IpSetExcludeDescription",
            ["--hostlist"] = "ConfigMakerZapret2HostListDescription",
            ["--hostlist-domains"] = "ConfigMakerZapret2HostListDescription",
            ["--hostlist-exclude"] = "ConfigMakerZapret2HostListExcludeDescription",
            ["--hostlist-exclude-domains"] = "ConfigMakerZapret2HostListExcludeDescription",
            ["--hostlist-auto"] = "ConfigMakerZapret2HostListAutoDescription",
            ["--payload"] = "ConfigMakerZapret2PayloadDescription",
            ["--out-range"] = "ConfigMakerZapret2OutRangeDescription",
            ["--in-range"] = "ConfigMakerZapret2InRangeDescription",
            ["--lua-desync"] = "ConfigMakerZapret2LuaDesyncDescription",
            ["--wf-iface"] = "ConfigMakerZapret2WinDivertInterfaceDescription",
            ["--wf-l3"] = "ConfigMakerZapret2WinDivertL3Description",
            ["--wf-tcp-in"] = "ConfigMakerZapret2WinDivertIncomingDescription",
            ["--wf-udp-in"] = "ConfigMakerZapret2WinDivertIncomingDescription",
            ["--wf-icmp-in"] = "ConfigMakerZapret2WinDivertIncomingDescription",
            ["--wf-ipp-in"] = "ConfigMakerZapret2WinDivertIncomingDescription",
            ["--wf-tcp-out"] = "ConfigMakerZapret2WinDivertOutgoingDescription",
            ["--wf-udp-out"] = "ConfigMakerZapret2WinDivertOutgoingDescription",
            ["--wf-icmp-out"] = "ConfigMakerZapret2WinDivertOutgoingDescription",
            ["--wf-ipp-out"] = "ConfigMakerZapret2WinDivertOutgoingDescription",
            ["--wf-tcp-empty"] = "ConfigMakerZapret2WinDivertTcpEmptyDescription",
            ["--wf-raw-part"] = "ConfigMakerZapret2WinDivertRawPartDescription",
            ["--wf-raw-filter"] = "ConfigMakerZapret2WinDivertRawFilterDescription",
            ["--wf-filter-lan"] = "ConfigMakerZapret2WinDivertFilterLanDescription",
            ["--wf-raw"] = "ConfigMakerZapret2WinDivertRawDescription",
            ["--wf-dup-check"] = "ConfigMakerZapret2WinDivertDuplicateDescription",
            ["--wf-save"] = "ConfigMakerZapret2WinDivertSaveDescription",
            ["--ssid-filter"] = "ConfigMakerZapret2SsidFilterDescription",
            ["--nlm-filter"] = "ConfigMakerZapret2NlmFilterDescription",
            ["--nlm-list"] = "ConfigMakerZapret2NlmListDescription",
        };

    private static readonly IReadOnlyDictionary<string, string> LuaFunctionResourceKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = "ConfigMakerZapret2LuaFakeDescription",
            ["multisplit"] = "ConfigMakerZapret2LuaMultisplitDescription",
            ["multidisorder"] = "ConfigMakerZapret2LuaMultidisorderDescription",
            ["multidisorder_legacy"] = "ConfigMakerZapret2LuaMultidisorderLegacyDescription",
            ["fakedsplit"] = "ConfigMakerZapret2LuaFakeSplitDescription",
            ["fakeddisorder"] = "ConfigMakerZapret2LuaFakeDisorderDescription",
            ["hostfakesplit"] = "ConfigMakerZapret2LuaHostFakeSplitDescription",
            ["tcpseg"] = "ConfigMakerZapret2LuaTcpSegmentDescription",
            ["oob"] = "ConfigMakerZapret2LuaOobDescription",
            ["send"] = "ConfigMakerZapret2LuaSendDescription",
            ["drop"] = "ConfigMakerZapret2LuaDropDescription",
            ["pktmod"] = "ConfigMakerZapret2LuaPacketModifyDescription",
            ["wssize"] = "ConfigMakerZapret2LuaWindowSizeDescription",
            ["syndata"] = "ConfigMakerZapret2LuaSynDataDescription",
            ["synack"] = "ConfigMakerZapret2LuaSynAckDescription",
            ["rst"] = "ConfigMakerZapret2LuaResetDescription",
            ["udplen"] = "ConfigMakerZapret2LuaUdpLengthDescription",
            ["circular"] = "ConfigMakerZapret2LuaCircularDescription",
        };

    public static string GetOptionResourceKey(string optionName)
    {
        if (OptionResourceKeys.TryGetValue(optionName ?? string.Empty, out string resourceKey))
        {
            return resourceKey;
        }
        return optionName?.StartsWith("--hostlist-auto-", StringComparison.OrdinalIgnoreCase) == true
            ? "ConfigMakerZapret2HostListAutoSettingDescription"
            : string.Empty;
    }

    public static string GetLuaFunctionResourceKey(string value)
    {
        string normalized = (value ?? string.Empty).Trim().Trim('"', '\'');
        int separatorIndex = normalized.IndexOf(':');
        string functionName = separatorIndex >= 0
            ? normalized[..separatorIndex]
            : normalized;
        return LuaFunctionResourceKeys.TryGetValue(functionName, out string resourceKey)
            ? resourceKey
            : string.Empty;
    }
}
