using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Catalog;

internal static class BuiltInStrategyCatalogExpander
{
    private static readonly BlockCheckProtocol[] HttpProtocols = [BlockCheckProtocol.Http];
    private static readonly BlockCheckProtocol[] TlsProtocols =
        [BlockCheckProtocol.Tls12, BlockCheckProtocol.Tls13];
    private static readonly BlockCheckProtocol[] QuicProtocols = [BlockCheckProtocol.Quic];

    private static readonly FoolingDefinition[] TcpFoolings =
    [
        Fooling("md5", "tcp_md5", BlockCheckScanTier.Quick, Flag("tcp_md5")),
        Fooling("badsum", "badsum", BlockCheckScanTier.Quick, Flag("badsum")),
        Fooling("seq-neg3000", "tcp_seq=-3000", BlockCheckScanTier.Balanced, Value("tcp_seq", "-3000")),
        Fooling("seq-pos1000000", "tcp_seq=1000000", BlockCheckScanTier.Balanced, Value("tcp_seq", "1000000")),
        Fooling(
            "ack-neg66000-ts-up",
            "tcp_ack=-66000 + tcp_ts_up",
            BlockCheckScanTier.Balanced,
            Value("tcp_ack", "-66000"),
            Flag("tcp_ts_up")),
        Fooling("ts-neg1000", "tcp_ts=-1000", BlockCheckScanTier.Balanced, Value("tcp_ts", "-1000")),
        Fooling(
            "flags-unset-ack",
            "tcp_flags_unset=ACK",
            BlockCheckScanTier.Exhaustive,
            Value("tcp_flags_unset", "ACK")),
        Fooling(
            "flags-set-syn",
            "tcp_flags_set=SYN",
            BlockCheckScanTier.Exhaustive,
            Value("tcp_flags_set", "SYN")),
    ];

    public static StrategyCatalog Expand(StrategyCatalog seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        List<StrategyDefinition> strategies = [.. seed.Strategies];
        HashSet<string> ids = strategies
            .Select(strategy => strategy.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (StrategyDefinition strategy in GenerateStandardStrategies())
        {
            if (ids.Add(strategy.Id))
            {
                strategies.Add(strategy);
            }
        }

        return new StrategyCatalog
        {
            SchemaVersion = seed.SchemaVersion,
            CatalogVersion = "2026.08.11-standard",
            Source = seed.Source,
            Strategies = strategies,
        };
    }

    private static IEnumerable<StrategyDefinition> GenerateStandardStrategies()
    {
        foreach (StrategyDefinition strategy in GenerateMultiStrategies())
        {
            yield return strategy;
        }

        foreach (StrategyDefinition strategy in GenerateFakeStrategies())
        {
            yield return strategy;
        }

        foreach (StrategyDefinition strategy in GenerateFakedStrategies())
        {
            yield return strategy;
        }

        foreach (StrategyDefinition strategy in GenerateHostFakeStrategies())
        {
            yield return strategy;
        }

        foreach (StrategyDefinition strategy in GenerateCombinedStrategies())
        {
            yield return strategy;
        }

        foreach (StrategyDefinition strategy in GenerateQuicStrategies())
        {
            yield return strategy;
        }
    }

    private static IEnumerable<StrategyDefinition> GenerateMultiStrategies()
    {
        (string Id, string Value, BlockCheckScanTier Tier)[] httpPositions =
        [
            ("method2", "method+2", BlockCheckScanTier.Quick),
            ("midsld", "midsld", BlockCheckScanTier.Quick),
            ("method2-midsld", "method+2,midsld", BlockCheckScanTier.Balanced),
        ];
        foreach ((string id, string value, BlockCheckScanTier tier) in httpPositions)
        {
            foreach (string function in new[] { "multisplit", "multidisorder" })
            {
                yield return SplitStrategy("http", HttpProtocols, "http_req", function, id, value, tier);
            }
        }

        (string Id, string Value, BlockCheckScanTier Tier)[] tlsPositions =
        [
            ("2", "2", BlockCheckScanTier.Quick),
            ("1", "1", BlockCheckScanTier.Quick),
            ("sniext1", "sniext+1", BlockCheckScanTier.Quick),
            ("sniext4", "sniext+4", BlockCheckScanTier.Balanced),
            ("host1", "host+1", BlockCheckScanTier.Balanced),
            ("midsld", "midsld", BlockCheckScanTier.Quick),
            ("1-midsld", "1,midsld", BlockCheckScanTier.Balanced),
            ("1-midsld-1220", "1,midsld,1220", BlockCheckScanTier.Exhaustive),
            (
                "full-sni-host",
                "1,sniext+1,host+1,midsld-2,midsld,midsld+2,endhost-1",
                BlockCheckScanTier.Exhaustive),
        ];
        foreach ((string id, string value, BlockCheckScanTier tier) in tlsPositions)
        {
            foreach (string function in new[] { "multisplit", "multidisorder" })
            {
                yield return SplitStrategy("tls", TlsProtocols, "tls_client_hello", function, id, value, tier);
            }
        }
    }

    private static IEnumerable<StrategyDefinition> GenerateFakeStrategies()
    {
        foreach ((string prefix, IReadOnlyList<BlockCheckProtocol> protocols, string payload, string blob) in
                 new[]
                 {
                     ("http", (IReadOnlyList<BlockCheckProtocol>)HttpProtocols, "http_req", "fake_default_http"),
                     ("tls", (IReadOnlyList<BlockCheckProtocol>)TlsProtocols, "tls_client_hello", "fake_default_tls"),
                 })
        {
            foreach (FoolingDefinition fooling in TcpFoolings)
            {
                yield return Strategy(
                    $"{prefix}-fake-{fooling.Id}",
                    $"{prefix.ToUpperInvariant()} fake {fooling.DisplayName}",
                    "fake",
                    protocols,
                    fooling.Tier,
                    1.6d + (int)fooling.Tier * 0.25d,
                    [Action("fake", payload, [Value("blob", blob), .. fooling.Arguments, Value("repeats", "1")])]);
            }
        }
    }

    private static IEnumerable<StrategyDefinition> GenerateFakedStrategies()
    {
        (string Prefix, IReadOnlyList<BlockCheckProtocol> Protocols, string Payload, string[] Positions)[] groups =
        [
            ("http", HttpProtocols, "http_req", ["method+2", "midsld"]),
            ("tls", TlsProtocols, "tls_client_hello", ["1", "sniext+1", "host+1", "midsld"]),
        ];
        foreach ((string prefix, IReadOnlyList<BlockCheckProtocol> protocols, string payload, string[] positions) in groups)
        {
            foreach (string function in new[] { "fakedsplit", "fakeddisorder" })
            {
                foreach (string position in positions)
                {
                    string positionId = PositionId(position);
                    yield return Strategy(
                        $"{prefix}-{function}-{positionId}-md5",
                        $"{prefix.ToUpperInvariant()} {function} {position} tcp_md5",
                        "faked",
                        protocols,
                        position == "1" || position == "midsld"
                            ? BlockCheckScanTier.Balanced
                            : BlockCheckScanTier.Exhaustive,
                        2.1d,
                        [Action(function, payload, [Value("pos", position), Flag("tcp_md5")])]);
                }
            }
        }
    }

    private static IEnumerable<StrategyDefinition> GenerateHostFakeStrategies()
    {
        foreach ((string prefix, IReadOnlyList<BlockCheckProtocol> protocols, string payload) in
                 new[]
                 {
                     ("http", (IReadOnlyList<BlockCheckProtocol>)HttpProtocols, "http_req"),
                     ("tls", (IReadOnlyList<BlockCheckProtocol>)TlsProtocols, "tls_client_hello"),
                 })
        {
            yield return Strategy(
                $"{prefix}-hostfake-badsum",
                $"{prefix.ToUpperInvariant()} hostfakesplit badsum",
                "hostfake",
                protocols,
                BlockCheckScanTier.Balanced,
                2.2d,
                [Action("hostfakesplit", payload, [Flag("badsum"), Value("repeats", "1")])]);
            yield return Strategy(
                $"{prefix}-hostfake-disorder-midhost-md5",
                $"{prefix.ToUpperInvariant()} hostfakesplit disorder_after midsld tcp_md5",
                "hostfake",
                protocols,
                BlockCheckScanTier.Exhaustive,
                2.8d,
                [Action(
                    "hostfakesplit",
                    payload,
                    [Flag("disorder_after"), Value("midhost", "midsld"), Flag("tcp_md5"), Value("repeats", "1")])]);
            yield return Strategy(
                $"{prefix}-hostfake-nofake1-midhost-md5",
                $"{prefix.ToUpperInvariant()} hostfakesplit nofake1 midsld tcp_md5",
                "hostfake",
                protocols,
                BlockCheckScanTier.Exhaustive,
                2.9d,
                [Action(
                    "hostfakesplit",
                    payload,
                    [Flag("nofake1"), Value("midhost", "midsld"), Flag("tcp_md5"), Value("repeats", "1")])]);
        }
    }

    private static IEnumerable<StrategyDefinition> GenerateCombinedStrategies()
    {
        foreach ((string prefix, IReadOnlyList<BlockCheckProtocol> protocols, string payload, string blob, string position) in
                 new[]
                 {
                     ("http", (IReadOnlyList<BlockCheckProtocol>)HttpProtocols, "http_req", "fake_default_http", "midsld"),
                     ("tls", (IReadOnlyList<BlockCheckProtocol>)TlsProtocols, "tls_client_hello", "fake_default_tls", "midsld"),
                 })
        {
            LuaActionDefinition fake = Action(
                "fake",
                payload,
                [Value("blob", blob), Flag("tcp_md5"), Value("repeats", "1")]);
            yield return Strategy(
                $"{prefix}-fake-md5-multisplit-{PositionId(position)}",
                $"{prefix.ToUpperInvariant()} fake tcp_md5 + multisplit {position}",
                "fake-multi",
                protocols,
                BlockCheckScanTier.Balanced,
                3.0d,
                [fake, Action("multisplit", payload, [Value("pos", position)])]);
            yield return Strategy(
                $"{prefix}-fake-md5-fakedsplit-{PositionId(position)}",
                $"{prefix.ToUpperInvariant()} fake tcp_md5 + fakedsplit {position}",
                "fake-faked",
                protocols,
                BlockCheckScanTier.Exhaustive,
                3.4d,
                [fake, Action("fakedsplit", payload, [Value("pos", position), Flag("tcp_md5")])]);
            yield return Strategy(
                $"{prefix}-fake-md5-hostfake-midhost",
                $"{prefix.ToUpperInvariant()} fake tcp_md5 + hostfakesplit midsld",
                "fake-hostfake",
                protocols,
                BlockCheckScanTier.Exhaustive,
                3.6d,
                [
                    fake,
                    Action(
                        "hostfakesplit",
                        payload,
                        [Value("midhost", "midsld"), Flag("tcp_md5"), Value("repeats", "1")]),
                ]);
        }
    }

    private static IEnumerable<StrategyDefinition> GenerateQuicStrategies()
    {
        foreach (int repeats in new[] { 10, 20 })
        {
            yield return Strategy(
                $"quic-fake-r{repeats}",
                $"QUIC fake x{repeats}",
                "quic-fake",
                QuicProtocols,
                BlockCheckScanTier.Exhaustive,
                1.2d + repeats * 0.3d,
                [Action(
                    "fake",
                    "quic_initial",
                    [Value("blob", "fake_default_quic"), Value("repeats", repeats.ToString())])]);
        }

        foreach (int position in new[] { 16, 32, 64 })
        {
            LuaActionDefinition fragment = Action(
                "send",
                "quic_initial",
                [Flag("ipfrag"), Value("ipfrag_pos_udp", position.ToString())]);
            LuaActionDefinition drop = Action("drop", "quic_initial", []);
            BlockCheckScanTier tier = position == 16
                ? BlockCheckScanTier.Balanced
                : BlockCheckScanTier.Exhaustive;
            yield return Strategy(
                $"quic-frag-{position}",
                $"QUIC IP fragment position {position}",
                "quic-frag",
                QuicProtocols,
                tier,
                2.2d + position / 100d,
                [fragment, drop]);
            yield return Strategy(
                $"quic-fake-frag-{position}",
                $"QUIC fake + IP fragment position {position}",
                "quic-fake-frag",
                QuicProtocols,
                BlockCheckScanTier.Exhaustive,
                3.2d + position / 100d,
                [
                    Action(
                        "fake",
                        "quic_initial",
                        [Value("blob", "fake_default_quic"), Value("repeats", "1")]),
                    fragment,
                    drop,
                ]);
        }

        foreach ((string id, LuaArgumentDefinition[] arguments) in new[]
                 {
                     ("hopbyhop", new[] { Flag("ip6_hopbyhop") }),
                     ("destopt", new[] { Flag("ip6_destopt") }),
                     ("hopbyhop-destopt", new[] { Flag("ip6_hopbyhop"), Flag("ip6_destopt") }),
                 })
        {
            yield return Strategy(
                $"quic-ipv6-{id}",
                $"QUIC IPv6 {id}",
                "quic-ipv6",
                QuicProtocols,
                BlockCheckScanTier.Exhaustive,
                3d,
                [Action("send", "quic_initial", arguments), Action("drop", "quic_initial", [])],
                [BlockCheckIpVersion.IPv6]);
        }
    }

    private static StrategyDefinition SplitStrategy(
        string prefix,
        IReadOnlyList<BlockCheckProtocol> protocols,
        string payload,
        string function,
        string positionId,
        string position,
        BlockCheckScanTier tier) => Strategy(
        $"{prefix}-{function}-{positionId}",
        $"{prefix.ToUpperInvariant()} {function} {position}",
        "multi",
        protocols,
        tier,
        1d + (function == "multidisorder" ? 0.3d : 0d) + (int)tier * 0.2d,
        [Action(function, payload, [Value("pos", position)])]);

    private static StrategyDefinition Strategy(
        string id,
        string displayName,
        string family,
        IReadOnlyList<BlockCheckProtocol> protocols,
        BlockCheckScanTier tier,
        double cost,
        List<LuaActionDefinition> actions,
        IReadOnlyList<BlockCheckIpVersion>? ipVersions = null) => new()
    {
        Id = id,
        DisplayName = displayName,
        Family = family,
        Protocols = [.. protocols],
        IpVersions = ipVersions == null
            ? [BlockCheckIpVersion.IPv4, BlockCheckIpVersion.IPv6]
            : [.. ipVersions],
        ScanTier = tier,
        BaseCost = cost,
        Actions = actions,
    };

    private static LuaActionDefinition Action(
        string function,
        string payload,
        IEnumerable<LuaArgumentDefinition> arguments) => new()
    {
        Function = function,
        Payloads = [payload],
        Arguments = [.. arguments],
    };

    private static FoolingDefinition Fooling(
        string id,
        string displayName,
        BlockCheckScanTier tier,
        params LuaArgumentDefinition[] arguments) =>
        new(id, displayName, tier, arguments);

    private static LuaArgumentDefinition Flag(string name) => new() { Name = name };

    private static LuaArgumentDefinition Value(string name, string value) => new()
    {
        Name = name,
        Value = value,
    };

    private static string PositionId(string value) => value
        .Replace("+", string.Empty, StringComparison.Ordinal)
        .Replace(",", "-", StringComparison.Ordinal);

    private sealed record FoolingDefinition(
        string Id,
        string DisplayName,
        BlockCheckScanTier Tier,
        IReadOnlyList<LuaArgumentDefinition> Arguments);
}
