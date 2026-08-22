using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Analysis;

public static class ProbeResultAnalyzer
{
    public static IReadOnlyList<CandidateEvaluation> BuildEvaluations(
        StrategyCatalog catalog,
        IEnumerable<ProbeResult> probeResults,
        ICollection<BlockCheckIssue>? issues = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(probeResults);

        Dictionary<string, StrategyDefinition> strategies = catalog.Strategies
            .ToDictionary(strategy => strategy.Id, StringComparer.OrdinalIgnoreCase);

        Dictionary<(string StrategyId, string TargetId), List<ProbeAttempt>> attempts = new();
        foreach (ProbeResult result in probeResults)
        {
            if (!strategies.ContainsKey(result.StrategyId))
            {
                issues?.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Warning,
                    "PROBE_STRATEGY_UNKNOWN",
                    "Probe result references a strategy that is not present in the catalog.",
                    result.StrategyId));
                continue;
            }

            var key = (result.StrategyId.ToLowerInvariant(), result.TargetId);
            if (!attempts.TryGetValue(key, out List<ProbeAttempt>? values))
            {
                values = [];
                attempts.Add(key, values);
            }
            values.AddRange(result.Attempts);
        }

        List<CandidateEvaluation> evaluations = [];
        foreach (StrategyDefinition strategy in catalog.Strategies)
        {
            Dictionary<string, ProbeSummary> summaries = attempts
                .Where(pair => pair.Key.StrategyId.Equals(strategy.Id, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    pair => pair.Key.TargetId,
                    pair => ProbeSummary.FromAttempts(pair.Value),
                    StringComparer.Ordinal);

            evaluations.Add(new CandidateEvaluation
            {
                Definition = strategy,
                Results = summaries,
            });
        }

        return evaluations;
    }
}
