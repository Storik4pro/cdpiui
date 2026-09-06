using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Presentation;

public sealed class BlockCheckTargetInputOptions
{
    public IReadOnlySet<BlockCheckProtocol> Protocols { get; init; } =
        new HashSet<BlockCheckProtocol>
        {
            BlockCheckProtocol.Tls12,
            BlockCheckProtocol.TlsAuto,
        };

    public IReadOnlySet<BlockCheckIpVersion> IpVersions { get; init; } =
        new HashSet<BlockCheckIpVersion> { BlockCheckIpVersion.IPv4 };

    public int MaximumTargets { get; init; } = 200;
}

public sealed class BlockCheckTargetInputResult
{
    public IReadOnlyList<BlockCheckTarget> Targets { get; init; } = [];
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
    public bool Success => Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}

public sealed class BlockCheckTargetInputParser
{
    public BlockCheckTargetInputResult Parse(
        string input,
        BlockCheckTargetInputOptions? options = null)
    {
        options ??= new BlockCheckTargetInputOptions();
        List<BlockCheckIssue> issues = [];
        if (options.Protocols.Count == 0)
        {
            issues.Add(Error("TARGET_INPUT_PROTOCOLS_EMPTY", "At least one probe protocol must be selected."));
        }

        if (options.IpVersions.Count == 0)
        {
            issues.Add(Error("TARGET_INPUT_IP_VERSIONS_EMPTY", "At least one IP version must be selected."));
        }

        if (options.MaximumTargets < 1)
        {
            issues.Add(Error("TARGET_INPUT_LIMIT_INVALID", "Maximum target count must be at least one."));
        }

        if (issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return new BlockCheckTargetInputResult { Issues = issues };
        }

        string[] lines = (input ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();
        if (lines.Length == 0)
        {
            issues.Add(Error("TARGET_INPUT_EMPTY", "Enter at least one domain or URL."));
            return new BlockCheckTargetInputResult { Issues = issues };
        }

        List<TargetSeed> seeds = [];
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            bool hasExplicitScheme = line.Contains("://", StringComparison.Ordinal);
            string candidate = hasExplicitScheme ? line : $"https://{line}";
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) ||
                string.IsNullOrWhiteSpace(uri.Host))
            {
                issues.Add(Error(
                    "TARGET_INPUT_INVALID",
                    "The input line is not a valid domain or absolute URL.",
                    $"line-{lineIndex + 1}"));
                continue;
            }

            if (hasExplicitScheme &&
                !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error(
                    "TARGET_INPUT_SCHEME_UNSUPPORTED",
                    "Only HTTP and HTTPS URLs are supported.",
                    $"line-{lineIndex + 1}"));
                continue;
            }

            string host = BlockCheckTarget.NormalizeHost(uri.Host);
            if (!BlockCheckTarget.IsValidHost(host))
            {
                issues.Add(Error(
                    "TARGET_INPUT_HOST_INVALID",
                    "The input line does not contain a valid DNS host name.",
                    $"line-{lineIndex + 1}"));
                continue;
            }

            BlockCheckProtocol[] protocols = SelectProtocols(uri, hasExplicitScheme, options.Protocols);
            if (protocols.Length == 0)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Warning,
                    "TARGET_INPUT_PROTOCOL_DISABLED",
                    "The URL protocol is disabled by the current probe selection.",
                    $"line-{lineIndex + 1}"));
                continue;
            }

            string path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
            int? inputPort = GetInputPort(line, hasExplicitScheme, uri);
            foreach (BlockCheckProtocol protocol in protocols)
            {
                foreach (BlockCheckIpVersion ipVersion in options.IpVersions.OrderBy(value => value))
                {
                    seeds.Add(new TargetSeed(host, path, protocol, ipVersion, inputPort));
                }
            }
        }

        TargetSeed[] distinctSeeds = seeds.Distinct().ToArray();
        if (distinctSeeds.Length > options.MaximumTargets)
        {
            issues.Add(Error(
                "TARGET_INPUT_LIMIT_EXCEEDED",
                $"The input expands to {distinctSeeds.Length} targets; the configured limit is {options.MaximumTargets}."));
            return new BlockCheckTargetInputResult { Issues = issues };
        }

        List<BlockCheckTarget> targets = [];
        for (int index = 0; index < distinctSeeds.Length; index++)
        {
            TargetSeed seed = distinctSeeds[index];
            targets.Add(new BlockCheckTarget
            {
                Id = $"target-{index + 1:D3}-{ProtocolId(seed.Protocol)}-{IpVersionId(seed.IpVersion)}",
                Host = seed.Host,
                Path = seed.Path,
                Protocol = seed.Protocol,
                IpVersion = seed.IpVersion,
                CustomPort = seed.CustomPort,
            });
        }

        if (targets.Count == 0 && !issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            issues.Add(Error("TARGET_INPUT_NO_TARGETS", "No enabled probe targets could be created."));
        }

        return new BlockCheckTargetInputResult
        {
            Targets = targets,
            Issues = issues,
        };
    }

    private static BlockCheckProtocol[] SelectProtocols(
        Uri uri,
        bool hasExplicitScheme,
        IReadOnlySet<BlockCheckProtocol> enabled)
    {
        if (!hasExplicitScheme)
        {
            return enabled.OrderBy(protocol => protocol).ToArray();
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return enabled.Contains(BlockCheckProtocol.Http)
                ? [BlockCheckProtocol.Http]
                : [];
        }

        return enabled
            .Where(protocol => protocol is BlockCheckProtocol.Tls12 or BlockCheckProtocol.Tls13 or BlockCheckProtocol.TlsAuto or BlockCheckProtocol.Quic)
            .OrderBy(protocol => protocol)
            .ToArray();
    }

    private static int? GetInputPort(string line, bool hasExplicitScheme, Uri uri)
    {
        if (hasExplicitScheme)
        {
            return uri.IsDefaultPort ? null : uri.Port;
        }

        string authority = line.Split(['/', '?', '#'], 2)[0];
        int separator = authority.LastIndexOf(':');
        if (separator < 0 ||
            !int.TryParse(authority[(separator + 1)..], out int explicitPort))
        {
            return null;
        }

        return explicitPort;
    }

    private static string ProtocolId(BlockCheckProtocol protocol) => protocol switch
    {
        BlockCheckProtocol.Http => "http",
        BlockCheckProtocol.Tls12 => "tls12",
        BlockCheckProtocol.Tls13 => "tls13",
        BlockCheckProtocol.TlsAuto => "tlsauto",
        BlockCheckProtocol.Quic => "quic",
        _ => "unknown",
    };

    private static string IpVersionId(BlockCheckIpVersion ipVersion) => ipVersion switch
    {
        BlockCheckIpVersion.IPv4 => "ipv4",
        BlockCheckIpVersion.IPv6 => "ipv6",
        _ => "unknown",
    };

    private static BlockCheckIssue Error(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Error, code, message, subjectId);

    private readonly record struct TargetSeed(
        string Host,
        string Path,
        BlockCheckProtocol Protocol,
        BlockCheckIpVersion IpVersion,
        int? CustomPort);
}
