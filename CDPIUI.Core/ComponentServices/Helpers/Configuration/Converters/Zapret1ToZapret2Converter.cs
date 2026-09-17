using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// Converts an already expanded winws/nfqws1 command line to a winws2 command line.
/// Converts only command line - config files convertion isn't supported
/// </summary>
public sealed class Zapret1ToZapret2Converter
{
    private const string CompatibilityFunctionName = "cdpi_z1_desync";

    private const string CompatibilityLua =
        "cdpi_z1_zero64=string.rep(string.char(0),64); " +
        "cdpi_z1_zero256=string.rep(string.char(0),256); " +
        "cdpi_z1_funcs={fake=fake,multisplit=multisplit,multidisorder=multidisorder," +
        "multidisorder_legacy=multidisorder_legacy,fakedsplit=fakedsplit," +
        "fakeddisorder=fakeddisorder,hostfakesplit=hostfakesplit,syndata=syndata," +
        "synack=synack,rst=rst,send=send,drop=drop,udplen=udplen,dht_dn=dht_dn}; " +
        "function cdpi_z1_desync(ctx,d) " +
        "if d.arg.z1_skip_nosni and d.l7payload=='tls_client_hello' and " +
        "(not d.track or not d.track.hostname) then return end; " +
        "if d.arg.z1_func=='fake' then " +
        "local b=d.arg[d.l7payload]; " +
        "if not b then b=d.dis.udp and d.arg.unknown_udp or d.arg.unknown end; " +
        "if not b then return end; d.arg.blob=b end; " +
        "return cdpi_z1_funcs[d.arg.z1_func](ctx,d) " +
        "end";

    private static readonly HashSet<string> GlobalOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "debug", "dry-run", "version", "comment", "intercept", "ctrack-timeouts",
        "ctrack-disable", "ipcache-lifetime", "ipcache-hostname", "wf-iface", "wf-l3",
        "wf-tcp", "wf-udp", "wf-tcp-in", "wf-tcp-out", "wf-udp-in", "wf-udp-out",
        "wf-tcp-empty", "wf-icmp-in", "wf-icmp-out", "wf-ipp-in", "wf-ipp-out",
        "wf-raw-part", "wf-raw-filter", "wf-filter-lan", "wf-raw", "wf-dup-check",
        "wf-save", "ssid-filter", "nlm-filter"
    };

    private static readonly HashSet<string> ProfileOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "filter-l3", "filter-tcp", "filter-udp", "filter-icmp", "filter-ipp", "filter-l7",
        "ipset", "ipset-ip", "ipset-exclude", "ipset-exclude-ip", "hostlist",
        "hostlist-domains", "hostlist-exclude", "hostlist-exclude-domains", "hostlist-auto",
        "hostlist-auto-fail-threshold", "hostlist-auto-fail-time",
        "hostlist-auto-retrans-threshold", "hostlist-auto-retrans-reset",
        "hostlist-auto-retrans-maxseq", "hostlist-auto-incoming-maxseq",
        "hostlist-auto-udp-out", "hostlist-auto-udp-in", "hostlist-auto-debug",
        "filter-ssid", "name", "skip"
    };

    private static readonly HashSet<string> ProfileInputFileOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ipset", "ipset-exclude", "hostlist", "hostlist-exclude"
    };

    private static readonly HashSet<string> SupportedLegacyOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ip-id",
        "dup", "dup-replace", "dup-ttl", "dup-ttl6", "dup-autottl", "dup-autottl6",
        "dup-tcp-flags-set", "dup-tcp-flags-unset", "dup-fooling", "dup-ts-increment",
        "dup-badseq-increment", "dup-badack-increment", "dup-ip-id", "dup-start", "dup-cutoff",
        "dpi-desync", "dpi-desync-ttl", "dpi-desync-ttl6", "dpi-desync-autottl",
        "dpi-desync-autottl6", "dpi-desync-tcp-flags-set", "dpi-desync-tcp-flags-unset",
        "dpi-desync-fooling", "dpi-desync-repeats", "dpi-desync-skip-nosni",
        "dpi-desync-split-pos", "dpi-desync-split-seqovl",
        "dpi-desync-split-seqovl-pattern", "dpi-desync-fakedsplit-pattern",
        "dpi-desync-fakedsplit-mod", "dpi-desync-hostfakesplit-midhost",
        "dpi-desync-hostfakesplit-mod", "dpi-desync-ipfrag-pos-tcp",
        "dpi-desync-ipfrag-pos-udp", "dpi-desync-ts-increment",
        "dpi-desync-badseq-increment", "dpi-desync-badack-increment",
        "dpi-desync-any-protocol", "dpi-desync-fake-http", "dpi-desync-fake-tls",
        "dpi-desync-fake-tls-mod", "dpi-desync-fake-unknown",
        "dpi-desync-fake-syndata", "dpi-desync-fake-quic",
        "dpi-desync-fake-wireguard", "dpi-desync-fake-dht",
        "dpi-desync-fake-discord", "dpi-desync-fake-stun",
        "dpi-desync-fake-unknown-udp", "dpi-desync-udplen-increment",
        "dpi-desync-udplen-pattern", "dpi-desync-start", "dpi-desync-cutoff"
    };

    private static readonly HashSet<string> ZeroPhaseModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "synack", "syndata"
    };

    private readonly ZapretConversionOptions _options;
    private readonly List<ZapretConversionIssue> _issues = [];
    private readonly List<ZapretConversionFileReference> _referencedFiles = [];
    private readonly Dictionary<string, BlobDefinition> _blobs = new(StringComparer.Ordinal);

    public Zapret1ToZapret2Converter(ZapretConversionOptions? options = null)
    {
        _options = options ?? new ZapretConversionOptions();
    }

    public ZapretConversionResult Convert(string startupString)
    {
        _issues.Clear();
        _referencedFiles.Clear();
        _blobs.Clear();

        if (string.IsNullOrWhiteSpace(startupString))
        {
            AddError("EMPTY_COMMAND_LINE", "The Zapret1 startup string is empty.");
            return CreateResult(string.Empty);
        }

        List<CommandOption> parsed = ParseCommandLine(startupString);
        List<CommandOption> globals = [];
        List<LegacyProfile> profiles = [new LegacyProfile(1)];

        foreach (CommandOption option in parsed)
        {
            if (option.Name.Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                profiles.Add(new LegacyProfile(profiles.Count + 1));
                continue;
            }

            if (GlobalOptions.Contains(option.Name))
            {
                globals.Add(option);
                continue;
            }

            profiles[^1].Options.Add(option);
        }

        List<string> convertedProfiles = [];
        foreach (LegacyProfile profile in profiles)
        {
            convertedProfiles.Add(ConvertProfile(profile));
        }

        List<string> output = [];
        foreach (CommandOption option in globals)
        {
            string name = option.Name.ToLowerInvariant() switch
            {
                "wf-tcp" => "wf-tcp-out",
                "wf-udp" => "wf-udp-out",
                _ => option.Name
            };

            string? value = option.Value;
            if (value != null)
            {
                if (name.Equals("debug", StringComparison.OrdinalIgnoreCase))
                {
                    value = MapDirectFileValue(
                        name,
                        value,
                        ZapretConversionFileAccess.Write,
                        onlyWhenAtPrefixed: true);
                }
                else if (name.Equals("wf-raw-part", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("wf-raw-filter", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("wf-raw", StringComparison.OrdinalIgnoreCase))
                {
                    value = MapDirectFileValue(
                        name,
                        value,
                        ZapretConversionFileAccess.Read,
                        onlyWhenAtPrefixed: true);
                }
                else if (name.Equals("wf-save", StringComparison.OrdinalIgnoreCase))
                {
                    value = MapDirectFileValue(name, value, ZapretConversionFileAccess.Write);
                }
            }

            output.Add(FormatOption(name, value));
        }

        foreach (BlobDefinition blob in _blobs.Values.OrderBy(blob => blob.Name, StringComparer.Ordinal))
        {
            output.Add(FormatOption("blob", $"{blob.Name}:{blob.Expression}"));
        }

        output.Add(FormatOption("lua-init", $"@{_options.ZapretLibraryPath}"));
        output.Add(FormatOption("lua-init", $"@{_options.ZapretAntiDpiLibraryPath}"));
        output.Add(FormatOption("lua-init", CompatibilityLua));

        for (int i = 0; i < convertedProfiles.Count; i++)
        {
            if (i > 0)
            {
                output.Add("--new");
            }

            if (!string.IsNullOrWhiteSpace(convertedProfiles[i]))
            {
                output.Add(convertedProfiles[i]);
            }
        }

        return CreateResult(string.Join(' ', output.Where(value => !string.IsNullOrWhiteSpace(value))));
    }

    private string ConvertProfile(LegacyProfile profile)
    {
        List<string> output = [];
        Dictionary<string, List<string?>> legacy = new(StringComparer.OrdinalIgnoreCase);

        foreach (CommandOption option in profile.Options)
        {
            if (ProfileOptions.Contains(option.Name))
            {
                string? value = option.Value;
                if (value != null && ProfileInputFileOptions.Contains(option.Name))
                {
                    value = MapDirectFileValue(
                        option.Name,
                        value,
                        ZapretConversionFileAccess.Read);
                }
                else if (value != null && option.Name.Equals("hostlist-auto", StringComparison.OrdinalIgnoreCase))
                {
                    value = MapDirectFileValue(
                        option.Name,
                        value,
                        ZapretConversionFileAccess.ReadWrite);
                    AddWarning(
                        "HOSTLIST_AUTO_SEMANTICS_CHANGED",
                        "Zapret2 does not route a connection through an auto-hostlist profile until its hostname is known.",
                        option.Name,
                        profile.Index);
                }
                else if (value != null && option.Name.Equals("hostlist-auto-debug", StringComparison.OrdinalIgnoreCase))
                {
                    value = MapDirectFileValue(
                        option.Name,
                        value,
                        ZapretConversionFileAccess.Write);
                }

                output.Add(FormatOption(option.Name, value));
                continue;
            }

            if (!SupportedLegacyOptions.Contains(option.Name))
            {
                AddError(
                    "UNSUPPORTED_OPTION",
                    $"Zapret1 option '--{option.Name}' has no conversion rule.",
                    option.Name,
                    profile.Index);
                continue;
            }

            if (!legacy.TryGetValue(option.Name, out List<string?>? values))
            {
                values = [];
                legacy.Add(option.Name, values);
            }
            values.Add(option.Value);
        }

        AppendDuplicateAction(output, legacy, profile.Index);

        string? strategyValue = LastValue(legacy, "dpi-desync");
        if (string.IsNullOrWhiteSpace(strategyValue))
        {
            return string.Join(' ', output);
        }

        string[] modes = strategyValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (modes.Length == 0)
        {
            AddError(
                "EMPTY_STRATEGY",
                "The '--dpi-desync' option does not contain a strategy.",
                "dpi-desync",
                profile.Index);
            return string.Join(' ', output);
        }

        string desyncRange = BuildRange(
            LastValue(legacy, "dpi-desync-start"),
            LastValue(legacy, "dpi-desync-cutoff"),
            profile.Index,
            "dpi-desync");

        bool anyProtocol = IsEnabled(legacy, "dpi-desync-any-protocol");
        bool skipNoSni = !legacy.ContainsKey("dpi-desync-skip-nosni") ||
                         IsEnabled(legacy, "dpi-desync-skip-nosni");
        List<string> commonArgs = BuildCommonActionArgs(legacy, "dpi-desync", profile.Index);

        foreach (string mode in modes.Where(mode => ZeroPhaseModes.Contains(mode)))
        {
            output.Add("--payload=all");
            output.Add(FormatOption("out-range", desyncRange));
            AppendMode(output, mode, legacy, commonArgs, anyProtocol, skipNoSni, profile.Index);
        }

        string[] dataModes = modes.Where(mode => !ZeroPhaseModes.Contains(mode)).ToArray();
        if (dataModes.Length > 0)
        {
            output.Add(FormatOption("out-range", desyncRange));
            output.Add(anyProtocol ? "--payload=all" : "--payload=known");

            foreach (string mode in dataModes)
            {
                AppendMode(output, mode, legacy, commonArgs, anyProtocol, skipNoSni, profile.Index);
            }
        }

        return string.Join(' ', output);
    }

    private void AppendDuplicateAction(
        List<string> output,
        Dictionary<string, List<string?>> legacy,
        int profileIndex)
    {
        string? repeatValue = LastValue(legacy, "dup");
        if (string.IsNullOrWhiteSpace(repeatValue))
        {
            return;
        }

        string range = BuildRange(
            LastValue(legacy, "dup-start"),
            LastValue(legacy, "dup-cutoff"),
            profileIndex,
            "dup");

        List<string> args = BuildCommonActionArgs(legacy, "dup", profileIndex);
        args.Add("dir=out");
        args.Add($"repeats={repeatValue}");

        output.Add("--payload=all");
        output.Add(FormatOption("out-range", range));
        output.Add(FormatLuaDesync("send", args));

        if (IsEnabled(legacy, "dup-replace"))
        {
            output.Add("--lua-desync=drop:dir=out");
        }
    }

    private void AppendMode(
        List<string> output,
        string mode,
        Dictionary<string, List<string?>> legacy,
        List<string> commonArgs,
        bool anyProtocol,
        bool skipNoSni,
        int profileIndex)
    {
        string normalizedMode = mode.ToLowerInvariant();
        bool sendsOriginalSplitParts = normalizedMode is "multisplit" or "multidisorder";
        List<string> args = sendsOriginalSplitParts
            ? commonArgs.Where(arg =>
                arg.Equals("ip_id_conn", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("ip_id=", StringComparison.OrdinalIgnoreCase)).ToList()
            : [.. commonArgs];

        if (anyProtocol &&
            !normalizedMode.Equals("fakeknown", StringComparison.OrdinalIgnoreCase) &&
            !ZeroPhaseModes.Contains(normalizedMode))
        {
            args.Add("payload=~empty");
        }

        switch (normalizedMode)
        {
            case "fake":
            case "fakeknown":
                args.InsertRange(0, BuildFakePayloadArgs(legacy, profileIndex));
                string? tlsMod = LastValue(legacy, "dpi-desync-fake-tls-mod");
                if (!string.IsNullOrWhiteSpace(tlsMod) &&
                    !tlsMod.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    args.Add($"tls_mod={tlsMod}");
                }
                output.Add(FormatLegacyLuaDesync("fake", args, skipNoSni));
                break;

            case "multisplit":
                AppendSplitArgs(args, legacy, "multisplit", profileIndex);
                RemoveArg(args, "repeats");
                output.Add(FormatLegacyLuaDesync("multisplit", args, skipNoSni));
                break;

            case "multidisorder":
                AppendSplitArgs(args, legacy, "multidisorder", profileIndex);
                RemoveArg(args, "repeats");
                output.Add(FormatLegacyLuaDesync(
                    _options.PreferLegacyMultidisorder ? "multidisorder_legacy" : "multidisorder",
                    args,
                    skipNoSni));
                break;

            case "fakedsplit":
            case "fakeddisorder":
                AppendSplitArgs(args, legacy, normalizedMode, profileIndex);
                AppendFakeSplitArgs(args, legacy, profileIndex);
                output.Add(FormatLegacyLuaDesync(normalizedMode, args, skipNoSni));
                break;

            case "hostfakesplit":
                AppendHostFakeSplitArgs(args, legacy, profileIndex);
                output.Add(FormatLegacyLuaDesync("hostfakesplit", args, skipNoSni));
                break;

            case "syndata":
                string? syndata = LastValue(legacy, "dpi-desync-fake-syndata");
                if (!string.IsNullOrWhiteSpace(syndata))
                {
                    args.Insert(0, $"blob={GetBlobValue(syndata, profileIndex, "dpi-desync-fake-syndata")}");
                }
                output.Add(FormatLegacyLuaDesync("syndata", args, skipNoSni));
                break;

            case "synack":
                output.Add(FormatLegacyLuaDesync("synack", args, skipNoSni));
                break;

            case "rst":
                output.Add(FormatLegacyLuaDesync("rst", args, skipNoSni));
                break;

            case "rstack":
                args.Add("rstack");
                output.Add(FormatLegacyLuaDesync("rst", args, skipNoSni));
                break;

            case "ipfrag2":
                args.Add("ipfrag");
                AddValueArg(args, "ipfrag_pos_tcp", LastValue(legacy, "dpi-desync-ipfrag-pos-tcp"));
                AddValueArg(args, "ipfrag_pos_udp", LastValue(legacy, "dpi-desync-ipfrag-pos-udp"));
                output.Add(FormatLegacyLuaDesync("send", args, skipNoSni));
                output.Add(FormatLegacyLuaDesync("drop", [], skipNoSni));
                break;

            case "udplen":
                AddValueArg(args, "increment", LastValue(legacy, "dpi-desync-udplen-increment"));
                string? pattern = LastValue(legacy, "dpi-desync-udplen-pattern");
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    args.Add($"pattern={GetBlobValue(pattern, profileIndex, "dpi-desync-udplen-pattern")}");
                }
                output.Add(FormatLegacyLuaDesync("udplen", args, skipNoSni));
                break;

            case "tamper":
                output.Add(FormatLegacyLuaDesync("dht_dn", args, skipNoSni));
                break;

            default:
                AddError(
                    "UNSUPPORTED_STRATEGY",
                    $"Zapret1 strategy '{mode}' has no Zapret2 conversion rule.",
                    "dpi-desync",
                    profileIndex);
                break;
        }
    }

    private List<string> BuildFakePayloadArgs(
        Dictionary<string, List<string?>> legacy,
        int profileIndex)
    {
        const string zero64 = "cdpi_z1_zero64";
        const string zero256 = "cdpi_z1_zero256";

        string http = GetBlobValueOrDefault(
            LastValue(legacy, "dpi-desync-fake-http"),
            "fake_default_http",
            profileIndex,
            "dpi-desync-fake-http");
        string tls = GetBlobValueOrDefault(
            LastValue(legacy, "dpi-desync-fake-tls"),
            "fake_default_tls",
            profileIndex,
            "dpi-desync-fake-tls");
        string quic = GetBlobValueOrDefault(
            LastValue(legacy, "dpi-desync-fake-quic"),
            "fake_default_quic",
            profileIndex,
            "dpi-desync-fake-quic");
        string wireguard = GetBlobValueOrDefault(
            LastValue(legacy, "dpi-desync-fake-wireguard"),
            zero64,
            profileIndex,
            "dpi-desync-fake-wireguard");
        string dht = GetBlobValueOrDefault(
            LastValue(legacy, "dpi-desync-fake-dht"),
            zero64,
            profileIndex,
            "dpi-desync-fake-dht");
        string discord = GetBlobValueOrDefault(
            LastValue(legacy, "dpi-desync-fake-discord"),
            zero64,
            profileIndex,
            "dpi-desync-fake-discord");
        string stun = GetBlobValueOrDefault(
            LastValue(legacy, "dpi-desync-fake-stun"),
            zero64,
            profileIndex,
            "dpi-desync-fake-stun");
        string unknown = GetBlobValueOrDefault(
            LastValue(legacy, "dpi-desync-fake-unknown"),
            zero256,
            profileIndex,
            "dpi-desync-fake-unknown");
        string unknownUdp = GetBlobValueOrDefault(
            LastValue(legacy, "dpi-desync-fake-unknown-udp"),
            zero64,
            profileIndex,
            "dpi-desync-fake-unknown-udp");

        return
        [
            $"http_req={http}",
            $"tls_client_hello={tls}",
            $"quic_initial={quic}",
            $"wireguard_initiation={wireguard}",
            $"wireguard_response={wireguard}",
            $"wireguard_cookie={wireguard}",
            $"wireguard_keepalive={wireguard}",
            $"dht={dht}",
            $"utp_bt_handshake={zero64}",
            $"discord_ip_discovery={discord}",
            $"stun={stun}",
            $"unknown={unknown}",
            $"unknown_udp={unknownUdp}"
        ];
    }

    private void AppendSplitArgs(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        string mode,
        int profileIndex)
    {
        AddValueArg(args, "pos", LastValue(legacy, "dpi-desync-split-pos"));
        AddValueArg(args, "seqovl", LastValue(legacy, "dpi-desync-split-seqovl"));

        string? pattern = LastValue(legacy, "dpi-desync-split-seqovl-pattern");
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            args.Add($"seqovl_pattern={GetBlobValue(pattern, profileIndex, "dpi-desync-split-seqovl-pattern")}");
        }

        if (mode.Equals("multisplit", StringComparison.OrdinalIgnoreCase))
        {
            string? seqovl = LastValue(legacy, "dpi-desync-split-seqovl");
            if (!string.IsNullOrWhiteSpace(seqovl) && !int.TryParse(seqovl, out _))
            {
                AddError(
                    "MULTISPLIT_MARKER_SEQOVL",
                    "Zapret2 multisplit accepts only a numeric seqovl value.",
                    "dpi-desync-split-seqovl",
                    profileIndex);
            }
        }
    }

    private void AppendFakeSplitArgs(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        int profileIndex)
    {
        string? pattern = LastValue(legacy, "dpi-desync-fakedsplit-pattern");
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            args.Add($"pattern={GetBlobValue(pattern, profileIndex, "dpi-desync-fakedsplit-pattern")}");
        }

        string? modifiers = LastValue(legacy, "dpi-desync-fakedsplit-mod");
        if (string.IsNullOrWhiteSpace(modifiers) || modifiers.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (string modifier in SplitCsv(modifiers))
        {
            if (!modifier.StartsWith("altorder=", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(modifier["altorder=".Length..], out int altOrder))
            {
                AddError(
                    "UNSUPPORTED_FAKEDSPLIT_MODIFIER",
                    $"Unsupported fakedsplit modifier '{modifier}'.",
                    "dpi-desync-fakedsplit-mod",
                    profileIndex);
                continue;
            }

            int packetMode = altOrder & 7;
            switch (packetMode)
            {
                case 0:
                    break;
                case 1:
                    args.Add("nofake1");
                    break;
                case 2:
                    args.Add("nofake1");
                    args.Add("nofake2");
                    break;
                case 3:
                    args.Add("nofake1");
                    args.Add("nofake2");
                    args.Add("nofake4");
                    break;
                default:
                    AddError(
                        "UNSUPPORTED_FAKEDSPLIT_ALTORDER",
                        $"Unsupported fakedsplit altorder '{altOrder}'.",
                        "dpi-desync-fakedsplit-mod",
                        profileIndex);
                    break;
            }

            if ((altOrder & 24) != 0)
            {
                AddWarning(
                    "PARTIAL_FAKEDSPLIT_ALTORDER",
                    $"The replay-only part of fakedsplit altorder '{altOrder}' has no exact Zapret2 equivalent.",
                    "dpi-desync-fakedsplit-mod",
                    profileIndex);
            }
        }
    }

    private void AppendHostFakeSplitArgs(
        List<string> args,
        Dictionary<string, List<string?>> legacy,
        int profileIndex)
    {
        AddValueArg(args, "midhost", LastValue(legacy, "dpi-desync-hostfakesplit-midhost"));

        string? modifiers = LastValue(legacy, "dpi-desync-hostfakesplit-mod");
        if (string.IsNullOrWhiteSpace(modifiers) || modifiers.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (string modifier in SplitCsv(modifiers))
        {
            if (modifier.StartsWith("host=", StringComparison.OrdinalIgnoreCase))
            {
                args.Add(modifier);
            }
            else if (modifier.Equals("altorder=1", StringComparison.OrdinalIgnoreCase))
            {
                AddWarning(
                    "HOSTFAKESPLIT_ALTORDER_APPROXIMATION",
                    "hostfakesplit altorder=1 has no exact Zapret2 equivalent and was converted to the standard order.",
                    "dpi-desync-hostfakesplit-mod",
                    profileIndex);
            }
            else if (!modifier.Equals("altorder=0", StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    "UNSUPPORTED_HOSTFAKESPLIT_MODIFIER",
                    $"Unsupported hostfakesplit modifier '{modifier}'.",
                    "dpi-desync-hostfakesplit-mod",
                    profileIndex);
            }
        }
    }

    private List<string> BuildCommonActionArgs(
        Dictionary<string, List<string?>> legacy,
        string prefix,
        int profileIndex)
    {
        List<string> args = [];

        string optionPrefix = prefix.Equals("dup", StringComparison.OrdinalIgnoreCase)
            ? "dup"
            : "dpi-desync";

        string? ttl = LastValue(legacy, $"{optionPrefix}-ttl");
        string? ttl6 = LastValue(legacy, $"{optionPrefix}-ttl6") ?? ttl;
        AddValueArg(args, "ip_ttl", ttl);
        AddValueArg(args, "ip6_ttl", ttl6);

        string defaultAutoTtl = optionPrefix.Equals("dup", StringComparison.OrdinalIgnoreCase)
            ? "+1:3-64"
            : "1:3-20";
        string? autoTtl = OptionalValue(legacy, $"{optionPrefix}-autottl", defaultAutoTtl);
        string? autoTtl6 = legacy.ContainsKey($"{optionPrefix}-autottl6")
            ? OptionalValue(legacy, $"{optionPrefix}-autottl6", autoTtl ?? defaultAutoTtl)
            : autoTtl;
        string defaultAutoTtlRange = optionPrefix.Equals("dup", StringComparison.OrdinalIgnoreCase)
            ? "3-64"
            : "3-20";
        AddAutoTtlArg(args, "ip_autottl", autoTtl, defaultAutoTtlRange);
        AddAutoTtlArg(args, "ip6_autottl", autoTtl6, defaultAutoTtlRange);

        string? setFlags = LastValue(legacy, $"{optionPrefix}-tcp-flags-set");
        string? unsetFlags = LastValue(legacy, $"{optionPrefix}-tcp-flags-unset");
        AddValueArg(args, "tcp_flags_set", NormalizeTcpFlags(setFlags));
        AddValueArg(args, "tcp_flags_unset", NormalizeTcpFlags(unsetFlags));

        string? ipId = prefix.Equals("dup", StringComparison.OrdinalIgnoreCase)
            ? LastValue(legacy, "dup-ip-id") ?? LastValue(legacy, "ip-id")
            : LastValue(legacy, "ip-id");
        if (!string.IsNullOrWhiteSpace(ipId))
        {
            if (ipId.Equals("same", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("ip_id=none");
            }
            else if (ipId.Equals("seqgroup", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("ip_id=seq");
                args.Add("ip_id_conn");
            }
            else
            {
                args.Add($"ip_id={ipId}");
            }
        }

        string? fooling = LastValue(legacy, $"{optionPrefix}-fooling");
        AppendFoolingArgs(args, fooling, legacy, optionPrefix, profileIndex);

        if (!prefix.Equals("dup", StringComparison.OrdinalIgnoreCase))
        {
            AddValueArg(args, "repeats", LastValue(legacy, "dpi-desync-repeats"));
        }

        return args;
    }

    private void AppendFoolingArgs(
        List<string> args,
        string? fooling,
        Dictionary<string, List<string?>> legacy,
        string optionPrefix,
        int profileIndex)
    {
        if (string.IsNullOrWhiteSpace(fooling))
        {
            return;
        }

        foreach (string mode in SplitCsv(fooling))
        {
            switch (mode.ToLowerInvariant())
            {
                case "none":
                    break;
                case "md5sig":
                    args.Add("tcp_md5");
                    break;
                case "badsum":
                    args.Add("badsum");
                    break;
                case "datanoack":
                    args.Add("tcp_flags_unset=ack");
                    break;
                case "ts":
                    args.Add($"tcp_ts={LastValue(legacy, $"{optionPrefix}-ts-increment") ?? "-600000"}");
                    break;
                case "badseq":
                    string sequence = LastValue(legacy, $"{optionPrefix}-badseq-increment") ?? "-10000";
                    if (!IsZero(sequence))
                    {
                        args.Add($"tcp_seq={sequence}");
                    }
                    else
                    {
                        args.Add($"tcp_ack={LastValue(legacy, $"{optionPrefix}-badack-increment") ?? "-66000"}");
                        args.Add("tcp_ts_up");
                    }
                    break;
                case "hopbyhop":
                    args.Add("ip6_hopbyhop");
                    break;
                case "hopbyhop2":
                    args.Add("ip6_hopbyhop");
                    args.Add("ip6_hopbyhop2");
                    break;
                default:
                    AddError(
                        "UNSUPPORTED_FOOLING",
                        $"Zapret1 fooling mode '{mode}' has no conversion rule.",
                        $"{optionPrefix}-fooling",
                        profileIndex);
                    break;
            }
        }
    }

    private string GetBlobValueOrDefault(
        string? value,
        string defaultValue,
        int profileIndex,
        string optionName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : GetBlobValue(value, profileIndex, optionName);
    }

    private string GetBlobValue(string value, int profileIndex, string optionName)
    {
        string normalized = value.Trim().Trim('"');
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (normalized == "!")
        {
            return "fake_default_tls";
        }

        if (normalized.StartsWith("!", StringComparison.Ordinal))
        {
            AddWarning(
                "TLS_FAKE_OFFSET_IGNORED",
                $"The standard TLS fake offset '{normalized}' has no direct Zapret2 equivalent.",
                optionName,
                profileIndex);
            return "fake_default_tls";
        }

        ParseFileExpression(normalized, out string sourcePath, out long offset);
        string convertedPath = _options.FilePathMapper?.Invoke(sourcePath) ?? sourcePath;
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{offset}:{convertedPath}");

        if (_blobs.TryGetValue(key, out BlobDefinition? existing))
        {
            return existing.Name;
        }

        string blobName = "z1_" + global::System.Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12].ToLowerInvariant();
        string prefix = offset > 0 ? $"+{offset.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        string expression = $"{prefix}@{convertedPath}";

        _blobs.Add(key, new BlobDefinition(blobName, expression));
        _referencedFiles.Add(new ZapretConversionFileReference(
            optionName,
            sourcePath,
            convertedPath,
            offset,
            ZapretConversionFileAccess.Read,
            blobName));

        return blobName;
    }

    private string MapDirectFileValue(
        string optionName,
        string value,
        ZapretConversionFileAccess access,
        bool onlyWhenAtPrefixed = false)
    {
        string normalized = value.Trim().Trim('"');
        bool atPrefixed = normalized.StartsWith('@');
        if (onlyWhenAtPrefixed && !atPrefixed)
        {
            return value;
        }

        string sourcePath = atPrefixed ? normalized[1..] : normalized;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return value;
        }

        string convertedPath = _options.FilePathMapper?.Invoke(sourcePath) ?? sourcePath;
        if (!_referencedFiles.Any(file =>
                file.OptionName.Equals(optionName, StringComparison.OrdinalIgnoreCase) &&
                file.SourcePath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase) &&
                file.ConvertedPath.Equals(convertedPath, StringComparison.OrdinalIgnoreCase) &&
                file.Offset == 0 &&
                file.Access == access))
        {
            _referencedFiles.Add(new ZapretConversionFileReference(
                optionName,
                sourcePath,
                convertedPath,
                0,
                access));
        }

        return atPrefixed ? $"@{convertedPath}" : convertedPath;
    }

    private static void ParseFileExpression(string value, out string path, out long offset)
    {
        offset = 0;
        path = value;

        int atIndex = value.IndexOf('@');
        if (atIndex >= 0)
        {
            string offsetPart = value[..atIndex];
            path = value[(atIndex + 1)..];

            if (offsetPart.StartsWith('+') &&
                long.TryParse(offsetPart[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                offset = parsed;
            }
        }

        path = path.Trim().Trim('"');
    }

    private List<CommandOption> ParseCommandLine(string commandLine)
    {
        List<string> tokens = Tokenize(commandLine, out bool unclosedQuote);
        if (unclosedQuote)
        {
            AddError("UNCLOSED_QUOTE", "The Zapret1 startup string contains an unclosed quote.");
        }

        List<CommandOption> result = [];
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            if (token.StartsWith('@'))
            {
                AddError(
                    "CONFIG_FILE_INPUT",
                    "An @config_file command line must be expanded before conversion.");
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                AddError(
                    "POSITIONAL_ARGUMENT",
                    $"Unexpected positional argument '{token}'. The converter accepts a ready command line only.");
                continue;
            }

            string body = token[2..];
            int equalsIndex = body.IndexOf('=');
            string name = equalsIndex >= 0 ? body[..equalsIndex] : body;
            string? value = equalsIndex >= 0 ? body[(equalsIndex + 1)..] : null;

            if (equalsIndex < 0 && i + 1 < tokens.Count &&
                !tokens[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = tokens[++i];
            }

            result.Add(new CommandOption(name.ToLowerInvariant(), value));
        }

        return result;
    }

    private static List<string> Tokenize(string commandLine, out bool unclosedQuote)
    {
        List<string> result = [];
        StringBuilder current = new();
        bool quoted = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char character = commandLine[i];
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        unclosedQuote = quoted;
        return result;
    }

    private string BuildRange(
        string? start,
        string? cutoff,
        int profileIndex,
        string optionPrefix)
    {
        string? normalizedStart = NormalizeRangeCounter(start);
        string? normalizedCutoff = NormalizeRangeCounter(cutoff);

        if (start != null && normalizedStart == null)
        {
            AddError(
                "INVALID_RANGE",
                $"Invalid Zapret1 range value '{start}'.",
                $"{optionPrefix}-start",
                profileIndex);
            return "a";
        }
        if (cutoff != null && normalizedCutoff == null)
        {
            AddError(
                "INVALID_RANGE",
                $"Invalid Zapret1 range value '{cutoff}'.",
                $"{optionPrefix}-cutoff",
                profileIndex);
            return "a";
        }

        if (normalizedStart != null && normalizedCutoff != null)
        {
            return $"{normalizedStart}<{normalizedCutoff}";
        }
        if (normalizedStart != null)
        {
            return $"{normalizedStart}-";
        }
        if (normalizedCutoff != null)
        {
            return $"<{normalizedCutoff}";
        }
        return "a";
    }

    private static string? NormalizeRangeCounter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            return null;
        }

        char mode = char.ToLowerInvariant(value[0]);
        if (mode is not ('n' or 'd' or 's'))
        {
            return null;
        }

        return long.TryParse(value[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? char.ToLowerInvariant(value[0]) + value[1..]
            : null;
    }

    private static string? NormalizeTcpFlags(string? value)
    {
        return value?.Replace("PSH", "PUSH", StringComparison.OrdinalIgnoreCase);
    }

    private static string? OptionalValue(
        Dictionary<string, List<string?>> options,
        string name,
        string? defaultValue)
    {
        if (!options.TryGetValue(name, out List<string?>? values) || values.Count == 0)
        {
            return null;
        }

        return values[^1] ?? defaultValue;
    }

    private static string? LastValue(Dictionary<string, List<string?>> options, string name)
    {
        return options.TryGetValue(name, out List<string?>? values) && values.Count > 0
            ? values[^1]
            : null;
    }

    private static bool IsEnabled(Dictionary<string, List<string?>> options, string name)
    {
        if (!options.TryGetValue(name, out List<string?>? values) || values.Count == 0)
        {
            return false;
        }

        string? value = values[^1];
        return value == null ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsZero(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex) &&
                   hex == 0;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) &&
               number == 0;
    }

    private static string NormalizeAutoTtl(string value, string defaultRange)
    {
        string normalized = value;
        if (normalized[0] is not ('+' or '-'))
        {
            normalized = "-" + normalized;
        }
        normalized = normalized.Replace(':', ',');
        if (!normalized.Contains(','))
        {
            normalized += $",{defaultRange}";
        }
        return normalized;
    }

    private static void AddAutoTtlArg(
        List<string> args,
        string name,
        string? value,
        string defaultRange)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "-" or "0:0-0" or "0,0-0")
        {
            return;
        }
        args.Add($"{name}={NormalizeAutoTtl(value, defaultRange)}");
    }

    private static void AddValueArg(List<string> args, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            args.Add($"{name}={value}");
        }
    }

    private static void RemoveArg(List<string> args, string name)
    {
        args.RemoveAll(arg => arg.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                              arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitCsv(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string FormatLegacyLuaDesync(
        string function,
        IEnumerable<string> args,
        bool skipNoSni)
    {
        List<string> compatibilityArgs = [$"z1_func={function}"];
        if (skipNoSni)
        {
            compatibilityArgs.Add("z1_skip_nosni=1");
        }
        compatibilityArgs.AddRange(args);
        return FormatLuaDesync(CompatibilityFunctionName, compatibilityArgs);
    }

    private static string FormatLuaDesync(string function, IEnumerable<string> args)
    {
        string[] values = args.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return values.Length == 0
            ? $"--lua-desync={function}"
            : $"--lua-desync={function}:{string.Join(':', values)}";
    }

    private static string FormatOption(string name, string? value)
    {
        if (value == null)
        {
            return $"--{name}";
        }

        bool mustQuote = value.Any(char.IsWhiteSpace) || value.Contains('"');
        string escaped = value.Replace("\"", "\\\"");
        return mustQuote
            ? $"--{name}=\"{escaped}\""
            : $"--{name}={escaped}";
    }

    private ZapretConversionResult CreateResult(string startupString)
    {
        return new ZapretConversionResult
        {
            StartupString = startupString,
            Issues = _issues.ToArray(),
            ReferencedFiles = _referencedFiles.ToArray()
        };
    }

    private void AddWarning(
        string code,
        string message,
        string? optionName = null,
        int? profileIndex = null)
    {
        _issues.Add(new ZapretConversionIssue(
            ZapretConversionIssueSeverity.Warning,
            code,
            message,
            optionName,
            profileIndex));
    }

    private void AddError(
        string code,
        string message,
        string? optionName = null,
        int? profileIndex = null)
    {
        _issues.Add(new ZapretConversionIssue(
            ZapretConversionIssueSeverity.Error,
            code,
            message,
            optionName,
            profileIndex));
    }

    private sealed record CommandOption(string Name, string? Value);

    private sealed record BlobDefinition(string Name, string Expression);

    private sealed class LegacyProfile(int index)
    {
        public int Index { get; } = index;

        public List<CommandOption> Options { get; } = [];
    }
}
