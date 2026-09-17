namespace CDPIUI.AddOns.BlockCheck2.Models;

public sealed class BlockCheckSynthesisOptions
{
    public double MinimumSuccessRate { get; init; } = 0.8d;
    public int MinimumAttempts { get; init; } = 3;
    public BlockCheckPreference Preference { get; init; } = BlockCheckPreference.Balanced;
    public bool EnableCircularFallback { get; init; } = true;
    public int MaximumFallbacksPerProfile { get; init; } = 1;
    public int CircularFailureThreshold { get; init; } = 3;
    public bool MakeLastFallbackFinal { get; init; } = true;
    public bool EnableBestEffort { get; init; } = true;
}

public sealed class CandidateEvaluation
{
    public StrategyDefinition Definition { get; init; } = new();
    public IReadOnlyDictionary<string, ProbeSummary> Results { get; init; } =
        new Dictionary<string, ProbeSummary>(StringComparer.Ordinal);

    public bool IsSuccessful(string targetId, BlockCheckSynthesisOptions options) =>
        Results.TryGetValue(targetId, out ProbeSummary? summary) &&
        summary.AttemptCount >= options.MinimumAttempts &&
        summary.SuccessRate >= options.MinimumSuccessRate;

    public IReadOnlySet<string> GetCoveredTargetIds(BlockCheckSynthesisOptions options) =>
        Results
            .Where(pair => IsSuccessful(pair.Key, options))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);

    public double GetMinimumSuccessRate(IEnumerable<string> targetIds)
    {
        ProbeSummary[] summaries = targetIds
            .Where(Results.ContainsKey)
            .Select(targetId => Results[targetId])
            .ToArray();
        return summaries.Length == 0 ? 0d : summaries.Min(summary => summary.SuccessRate);
    }

    public double GetAggregateP95(IEnumerable<string> targetIds)
    {
        double[] timings = targetIds
            .Where(Results.ContainsKey)
            .Select(targetId => Results[targetId].P95TimeToFirstByteMs)
            .Where(double.IsFinite)
            .ToArray();
        return timings.Length == 0 ? double.PositiveInfinity : timings.Max();
    }

    public double GetEffectiveCost(
        BlockCheckSynthesisOptions options,
        IEnumerable<string> targetIds)
    {
        string[] ids = targetIds.Distinct(StringComparer.Ordinal).ToArray();
        double minimumSuccess = GetMinimumSuccessRate(ids);
        double p95Seconds = GetAggregateP95(ids) / 1000d;
        if (!double.IsFinite(p95Seconds))
        {
            p95Seconds = 60d;
        }

        (double baseWeight, double latencyWeight, double instabilityWeight) = options.Preference switch
        {
            BlockCheckPreference.Speed => (0.5d, 2d, 80d),
            BlockCheckPreference.Stability => (1d, 0.5d, 300d),
            _ => (1d, 1d, 160d),
        };

        return Math.Max(0.001d,
            Definition.BaseCost * baseWeight +
            p95Seconds * latencyWeight +
            (1d - minimumSuccess) * instabilityWeight);
    }
}

public sealed class StrategyAssignment
{
    public CandidateEvaluation Candidate { get; init; } = new();
    public IReadOnlySet<string> TargetIds { get; init; } = new HashSet<string>();
    public IReadOnlySet<RuntimeRouteKey> RouteKeys { get; init; } = new HashSet<RuntimeRouteKey>();
    public bool IsBestEffort { get; init; }
}

public sealed class CoverageSelectionResult
{
    public IReadOnlyList<StrategyAssignment> Assignments { get; init; } = [];
    public IReadOnlySet<string> UncoveredTargetIds { get; init; } = new HashSet<string>();
}

public sealed class Zapret2ProfileFilter
{
    public BlockCheckIpVersion IpVersion { get; init; }
    public BlockCheckTransport Transport { get; init; }
    public int Port { get; init; }
    public string Layer7Protocol { get; init; } = string.Empty;
    public IReadOnlyList<string> Domains { get; init; } = [];
    public IReadOnlyList<string> HostListPaths { get; init; } = [];
}

public sealed class Zapret2ProfilePlan
{
    public string Name { get; set; } = string.Empty;
    public Zapret2ProfileFilter Filter { get; init; } = new();
    public StrategyDefinition Primary { get; init; } = new();
    public List<StrategyDefinition> Fallbacks { get; } = [];
    public IReadOnlySet<string> TargetIds { get; init; } = new HashSet<string>();
    public bool IsBestEffort { get; init; }

    public bool UsesCircular => Fallbacks.Count > 0;

    public IEnumerable<StrategyDefinition> EnumerateStrategies()
    {
        yield return Primary;
        foreach (StrategyDefinition fallback in Fallbacks)
        {
            yield return fallback;
        }
    }
}

public sealed class Zapret2WriterOptions
{
    public string ZapretLibraryPath { get; init; } = "lua/zapret-lib.lua";
    public string ZapretAntiDpiLibraryPath { get; init; } = "lua/zapret-antidpi.lua";
    public string ZapretAutoLibraryPath { get; init; } = "lua/zapret-auto.lua";
    public int CircularFailureThreshold { get; init; } = 3;
    public bool MakeLastFallbackFinal { get; init; } = true;
}

public sealed class Zapret2WriteResult
{
    public string CommandLine { get; init; } = string.Empty;
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
    public bool Success => Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}

public sealed class BlockCheckSynthesisResult
{
    public IReadOnlySet<string> BaselineAccessibleTargetIds { get; init; } =
        new HashSet<string>();
    public CoverageSelectionResult Selection { get; init; } = new();
    public IReadOnlyList<Zapret2ProfilePlan> Profiles { get; init; } = [];
    public Zapret2WriteResult Configuration { get; init; } = new();
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
    public bool IsBestEffort { get; init; }
    public bool Success =>
        Selection.UncoveredTargetIds.Count == 0 &&
        Configuration.Success &&
        Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}
