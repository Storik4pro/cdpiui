using System.Text.RegularExpressions;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Catalog;

public sealed partial class StrategyCatalogValidator
{
    public const int SupportedSchemaVersion = 1;

    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "fake",
        "multisplit",
        "multidisorder",
        "multidisorder_legacy",
        "fakedsplit",
        "fakeddisorder",
        "hostfakesplit",
        "tcpseg",
        "oob",
        "send",
        "drop",
        "pktmod",
        "wssize",
        "syndata",
        "synack",
        "rst",
        "udplen",
    };

    private static readonly HashSet<string> AllowedPayloads = new(StringComparer.OrdinalIgnoreCase)
    {
        "all",
        "known",
        "empty",
        "http_req",
        "http_reply",
        "tls_client_hello",
        "tls_server_hello",
        "quic_initial",
        "quic_handshake",
        "wireguard_initiation",
        "wireguard_response",
        "dht",
        "discord_ip_discovery",
        "stun",
        "dns_query",
        "dns_response",
        "dtls_client_hello",
        "dtls_server_hello",
        "ipv4",
        "ipv6",
        "icmp",
    };

    private static readonly HashSet<string> ReservedArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        "strategy",
        "final",
    };

    public IReadOnlyList<BlockCheckIssue> Validate(StrategyCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        List<BlockCheckIssue> issues = [];

        if (catalog.SchemaVersion != SupportedSchemaVersion)
        {
            issues.Add(Error(
                "UNSUPPORTED_CATALOG_SCHEMA",
                $"Catalog schema {catalog.SchemaVersion} is not supported; expected {SupportedSchemaVersion}."));
        }

        if (string.IsNullOrWhiteSpace(catalog.CatalogVersion))
        {
            issues.Add(Error("CATALOG_VERSION_MISSING", "Catalog version is required."));
        }

        HashSet<string> strategyIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (StrategyDefinition strategy in catalog.Strategies)
        {
            ValidateStrategy(strategy, strategyIds, issues);
        }

        if (catalog.Strategies.Count == 0)
        {
            issues.Add(Error("CATALOG_EMPTY", "Catalog must contain at least one strategy."));
        }

        return issues;
    }

    public IReadOnlyList<BlockCheckIssue> ValidateTargets(IEnumerable<BlockCheckTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        List<BlockCheckIssue> issues = [];
        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (BlockCheckTarget target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.Id) || !SafeIdentifierRegex().IsMatch(target.Id))
            {
                issues.Add(Error(
                    "TARGET_ID_INVALID",
                    "Target id must contain only letters, digits, dot, underscore or dash.",
                    target.Id));
            }
            else if (!ids.Add(target.Id))
            {
                issues.Add(Error("TARGET_ID_DUPLICATE", "Target id must be unique.", target.Id));
            }

            if (!BlockCheckTarget.IsValidHost(target.Host))
            {
                issues.Add(Error("TARGET_HOST_INVALID", "Target host is not a valid DNS name.", target.Id));
            }

            if (!Enum.IsDefined(target.Protocol))
            {
                issues.Add(Error("TARGET_PROTOCOL_INVALID", "Target protocol is unknown.", target.Id));
            }

            if (!Enum.IsDefined(target.IpVersion))
            {
                issues.Add(Error("TARGET_IP_VERSION_INVALID", "Target IP version is unknown.", target.Id));
            }

            if (target.Port is < 1 or > 65535)
            {
                issues.Add(Error("TARGET_PORT_INVALID", "Target port is outside 1..65535.", target.Id));
            }

            if (string.IsNullOrWhiteSpace(target.Path) || !target.Path.StartsWith('/'))
            {
                issues.Add(Error("TARGET_PATH_INVALID", "Target path must start with '/'.", target.Id));
            }
        }

        return issues;
    }

    private static void ValidateStrategy(
        StrategyDefinition strategy,
        ISet<string> strategyIds,
        ICollection<BlockCheckIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(strategy.Id) || !SafeIdentifierRegex().IsMatch(strategy.Id))
        {
            issues.Add(Error(
                "STRATEGY_ID_INVALID",
                "Strategy id must contain only letters, digits, dot, underscore or dash.",
                strategy.Id));
        }
        else if (!strategyIds.Add(strategy.Id))
        {
            issues.Add(Error("STRATEGY_ID_DUPLICATE", "Strategy id must be unique.", strategy.Id));
        }

        if (strategy.Protocols.Count == 0)
        {
            issues.Add(Error("STRATEGY_PROTOCOLS_EMPTY", "Strategy must support at least one protocol.", strategy.Id));
        }
        else if (strategy.Protocols.Any(protocol => !Enum.IsDefined(protocol)))
        {
            issues.Add(Error("STRATEGY_PROTOCOL_INVALID", "Strategy contains an unknown protocol.", strategy.Id));
        }

        if (strategy.IpVersions.Count == 0)
        {
            issues.Add(Error("STRATEGY_IP_VERSIONS_EMPTY", "Strategy must support at least one IP version.", strategy.Id));
        }
        else if (strategy.IpVersions.Any(ipVersion => !Enum.IsDefined(ipVersion)))
        {
            issues.Add(Error("STRATEGY_IP_VERSION_INVALID", "Strategy contains an unknown IP version.", strategy.Id));
        }

        if (!double.IsFinite(strategy.BaseCost) || strategy.BaseCost <= 0)
        {
            issues.Add(Error("STRATEGY_COST_INVALID", "Strategy base cost must be positive.", strategy.Id));
        }

        if (!Enum.IsDefined(strategy.ScanTier))
        {
            issues.Add(Error("STRATEGY_SCAN_TIER_INVALID", "Strategy contains an unknown scan tier.", strategy.Id));
        }

        if (strategy.Actions.Count == 0)
        {
            issues.Add(Error("STRATEGY_ACTIONS_EMPTY", "Strategy must contain at least one Lua action.", strategy.Id));
        }

        if (strategy.RequiresPreHost && strategy.SupportsCircular)
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Warning,
                "PREHOST_CIRCULAR_DISABLED",
                "Pre-host strategies cannot be used as circular fallbacks and will be treated as static.",
                strategy.Id));
        }

        ValidateBlobs(strategy, issues);

        for (int index = 0; index < strategy.Actions.Count; index++)
        {
            ValidateAction(strategy, strategy.Actions[index], index, issues);
        }
    }

    private static void ValidateBlobs(
        StrategyDefinition strategy,
        ICollection<BlockCheckIssue> issues)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (BlobDefinition blob in strategy.Blobs)
        {
            if (!SafeIdentifierRegex().IsMatch(blob.Name))
            {
                issues.Add(Error("BLOB_NAME_INVALID", "Blob name is invalid.", strategy.Id));
            }
            else if (!names.Add(blob.Name))
            {
                issues.Add(Error("BLOB_NAME_DUPLICATE", "Blob name is duplicated in one strategy.", strategy.Id));
            }

            if (!IsSafeBlobSource(blob.Source))
            {
                issues.Add(Error(
                    "BLOB_SOURCE_UNSAFE",
                    "Blob source must be inline hex or a relative path without traversal.",
                    strategy.Id));
            }
        }
    }

    private static void ValidateAction(
        StrategyDefinition strategy,
        LuaActionDefinition action,
        int index,
        ICollection<BlockCheckIssue> issues)
    {
        if (!AllowedFunctions.Contains(action.Function))
        {
            issues.Add(Error(
                "LUA_FUNCTION_NOT_ALLOWED",
                $"Lua function '{action.Function}' is not allowed in the built-in catalog.",
                strategy.Id));
        }

        if (!IsValidRange(action.InRange) || !IsValidRange(action.OutRange))
        {
            issues.Add(Error(
                "ACTION_RANGE_INVALID",
                $"Lua action #{index + 1} contains an invalid in/out range.",
                strategy.Id));
        }

        if (action.Payloads.Count == 0 || action.Payloads.Any(payload => !AllowedPayloads.Contains(payload)))
        {
            issues.Add(Error(
                "ACTION_PAYLOAD_INVALID",
                $"Lua action #{index + 1} contains an unknown payload filter.",
                strategy.Id));
        }

        foreach (LuaArgumentDefinition argument in action.Arguments)
        {
            if (!SafeArgumentNameRegex().IsMatch(argument.Name))
            {
                issues.Add(Error(
                    "ACTION_ARGUMENT_NAME_INVALID",
                    $"Lua action #{index + 1} contains an invalid argument name.",
                    strategy.Id));
            }
            else if (ReservedArguments.Contains(argument.Name))
            {
                issues.Add(Error(
                    "ACTION_ARGUMENT_RESERVED",
                    $"Argument '{argument.Name}' is reserved for generated orchestration.",
                    strategy.Id));
            }

            if (argument.Value != null && !SafeArgumentValueRegex().IsMatch(argument.Value))
            {
                issues.Add(Error(
                    "ACTION_ARGUMENT_VALUE_UNSAFE",
                    $"Lua action #{index + 1} contains characters that are unsafe for generated command lines.",
                    strategy.Id));
            }
        }
    }

    private static bool IsSafeBlobSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (source.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return source.Length > 2 && source[2..].All(Uri.IsHexDigit);
        }

        string path = source.StartsWith('@') ? source[1..] : source;
        return path.Length > 0 &&
               !Path.IsPathRooted(path) &&
               !path.Split('/', '\\').Any(part => part == "..") &&
               !path.Contains(':');
    }

    private static bool IsValidRange(string range)
    {
        if (range is "a" or "x")
        {
            return true;
        }

        Match match = RangeRegex().Match(range ?? string.Empty);
        return match.Success &&
               (match.Groups["left"].Success || match.Groups["right"].Success);
    }

    private static BlockCheckIssue Error(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Error, code, message, subjectId);

    [GeneratedRegex("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeArgumentNameRegex();

    [GeneratedRegex("^[A-Za-z0-9_@%#.+,\\-<>=:/\\\\]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeArgumentValueRegex();

    [GeneratedRegex("^(?<left>[ndbsp]-?\\d+)?(?:-|<)(?<right>[ndbsp]-?\\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex RangeRegex();
}
