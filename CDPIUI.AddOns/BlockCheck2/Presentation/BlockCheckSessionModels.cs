using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Presentation;

public enum BlockCheckSessionState
{
    Idle,
    InputInvalid,
    Running,
    Canceling,
    Completed,
    CompletedWithWarnings,
    Failed,
    Canceled,
}

public sealed class BlockCheckSessionRequest
{
    public string TargetInput { get; init; } = string.Empty;
    public IReadOnlyList<BlockCheckSiteListInput> SiteLists { get; init; } = [];
    public BlockCheckRunPreset RunPreset { get; init; } = BlockCheckRunPreset.Balanced;
    public bool TestAllStrategies { get; init; }
    public int? AttemptsPerTarget { get; init; }
    public IReadOnlySet<BlockCheckProtocol> Protocols { get; init; } =
        new HashSet<BlockCheckProtocol>
        {
            BlockCheckProtocol.Tls12,
            BlockCheckProtocol.TlsAuto,
        };
    public IReadOnlySet<BlockCheckIpVersion> IpVersions { get; init; } =
        new HashSet<BlockCheckIpVersion> { BlockCheckIpVersion.IPv4 };
}

public sealed class BlockCheckSessionPreparationResult
{
    public StrategyCatalog Catalog { get; init; } = new();
    public IReadOnlyList<BlockCheckTarget> Targets { get; init; } = [];
    public BlockCheckRunOptions RunOptions { get; init; } = new();
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
    public bool Success => Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}

public sealed class BlockCheckSessionPreparationService
{
    private readonly BlockCheckTargetInputParser _targetParser;
    private readonly BlockCheckSiteListLoader _siteListLoader;

    public BlockCheckSessionPreparationService(
        BlockCheckTargetInputParser? targetParser = null,
        BlockCheckSiteListLoader? siteListLoader = null)
    {
        _targetParser = targetParser ?? new BlockCheckTargetInputParser();
        _siteListLoader = siteListLoader ?? new BlockCheckSiteListLoader(_targetParser);
    }

    public BlockCheckSessionPreparationResult Prepare(BlockCheckSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        BlockCheckTargetInputOptions inputOptions = new()
        {
            Protocols = request.Protocols,
            IpVersions = request.IpVersions,
        };
        List<BlockCheckTarget> targets = [];
        List<BlockCheckIssue> issues = [];
        if (request.Protocols.SetEquals([BlockCheckProtocol.Http]))
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Warning,
                "HTTP_ONLY_BROWSER_LIMITATION",
                "Only clear-text HTTP on port 80 is selected. A successful response or redirect " +
                "does not verify that the HTTPS page used by a browser can load. Enable TLS 1.2 " +
                "and automatic HTTPS when checking websites such as YouTube."));
        }
        if (!string.IsNullOrWhiteSpace(request.TargetInput))
        {
            BlockCheckTargetInputResult manualTargets = _targetParser.Parse(
                request.TargetInput,
                inputOptions);
            targets.AddRange(manualTargets.Targets);
            issues.AddRange(manualTargets.Issues);
        }

        if (request.SiteLists.Count > 0)
        {
            BlockCheckSiteListLoadResult listTargets = _siteListLoader.Load(
                request.SiteLists,
                inputOptions);
            targets.AddRange(listTargets.Targets);
            issues.AddRange(listTargets.Issues);
        }

        if (targets.Count == 0 && !issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Error,
                "TARGET_INPUT_EMPTY",
                "Enter at least one domain, URL, or site list."));
        }

        IReadOnlyList<BlockCheckTarget> mergedTargets = MergeTargets(targets);

        return new BlockCheckSessionPreparationResult
        {
            Catalog = StrategyCatalogLoader.LoadBuiltIn(),
            Targets = mergedTargets,
            RunOptions = BlockCheckRunPresetFactory.Create(
                request.RunPreset,
                request.TestAllStrategies,
                request.AttemptsPerTarget),
            Issues = issues,
        };
    }

    private static IReadOnlyList<BlockCheckTarget> MergeTargets(IEnumerable<BlockCheckTarget> targets)
    {
        var groups = targets.GroupBy(target => new
        {
            Host = BlockCheckTarget.NormalizeHost(target.Host),
            target.Path,
            target.Protocol,
            target.IpVersion,
            target.CustomPort,
        });

        List<BlockCheckTarget> result = [];
        foreach (var group in groups)
        {
            result.Add(new BlockCheckTarget
            {
                Id = $"target-{result.Count + 1:D3}-{group.Key.Protocol.ToString().ToLowerInvariant()}-{group.Key.IpVersion.ToString().ToLowerInvariant()}",
                Host = group.Key.Host,
                Path = group.Key.Path,
                Protocol = group.Key.Protocol,
                IpVersion = group.Key.IpVersion,
                CustomPort = group.Key.CustomPort,
                HostListPaths = group
                    .SelectMany(target => target.HostListPaths)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            });
        }
        return result;
    }
}
