using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Analysis;

/// <summary>
/// Selects strategies over runtime-distinguishable route groups. TLS 1.2 and TLS 1.3
/// tests for the same host/IP/port are deliberately one group because a normal
/// Zapret2 profile cannot select between them.
/// </summary>
public sealed class CoverageOptimizer
{
    private const double Epsilon = 0.000001d;

    public CoverageSelectionResult Select(
        IEnumerable<BlockCheckTarget> targets,
        IEnumerable<CandidateEvaluation> candidates,
        BlockCheckSynthesisOptions options)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(options);

        BlockCheckTarget[] targetArray = targets.ToArray();
        CandidateEvaluation[] candidateArray = candidates.ToArray();

        Dictionary<string, BlockCheckTarget[]> routeGroups = BlockCheckTargetGroupBuilder
            .Build(targetArray)
            .ToDictionary(group => group.Id, group => group.Targets, StringComparer.Ordinal);

        HashSet<string> remaining = routeGroups.Keys.ToHashSet(StringComparer.Ordinal);
        HashSet<CandidateEvaluation> used = [];
        List<StrategyAssignment> assignments = [];

        while (remaining.Count > 0)
        {
            CandidateChoice? best = null;

            foreach (CandidateEvaluation candidate in candidateArray)
            {
                if (used.Contains(candidate))
                {
                    continue;
                }

                string[] newlyCovered = remaining
                    .Where(route => CoversRoute(candidate, routeGroups[route], options))
                    .ToArray();

                if (newlyCovered.Length == 0)
                {
                    continue;
                }

                string[] targetIds = newlyCovered
                    .SelectMany(route => routeGroups[route])
                    .Select(target => target.Id)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                double cost = candidate.GetEffectiveCost(options, targetIds);
                double benefit = newlyCovered.Sum(route => routeGroups[route].Length);
                CandidateChoice choice = new(candidate, newlyCovered, targetIds, benefit / cost);

                if (best == null || IsBetter(choice, best.Value))
                {
                    best = choice;
                }
            }

            if (best == null)
            {
                break;
            }

            used.Add(best.Value.Candidate);
            foreach (string route in best.Value.Routes)
            {
                remaining.Remove(route);
            }

            assignments.Add(new StrategyAssignment
            {
                Candidate = best.Value.Candidate,
                TargetIds = best.Value.TargetIds.ToHashSet(StringComparer.Ordinal),
                RouteKeys = best.Value.Routes
                    .SelectMany(route => routeGroups[route])
                    .Select(target => target.GetRuntimeRouteKey())
                    .ToHashSet(),
            });
        }

        HashSet<string> uncoveredTargetIds = remaining
            .SelectMany(route => routeGroups[route])
            .Select(target => target.Id)
            .ToHashSet(StringComparer.Ordinal);

        return new CoverageSelectionResult
        {
            Assignments = assignments,
            UncoveredTargetIds = uncoveredTargetIds,
        };
    }

    private static bool CoversRoute(
        CandidateEvaluation candidate,
        IReadOnlyList<BlockCheckTarget> routeTargets,
        BlockCheckSynthesisOptions options) =>
        routeTargets.All(target =>
            candidate.Definition.AppliesTo(target) &&
            candidate.IsSuccessful(target.Id, options));

    private static bool IsBetter(CandidateChoice candidate, CandidateChoice current)
    {
        if (candidate.Score > current.Score + Epsilon)
        {
            return true;
        }

        if (Math.Abs(candidate.Score - current.Score) > Epsilon)
        {
            return false;
        }

        if (candidate.Routes.Length != current.Routes.Length)
        {
            return candidate.Routes.Length > current.Routes.Length;
        }

        double candidateSuccess = candidate.Candidate.GetMinimumSuccessRate(candidate.TargetIds);
        double currentSuccess = current.Candidate.GetMinimumSuccessRate(current.TargetIds);
        if (Math.Abs(candidateSuccess - currentSuccess) > Epsilon)
        {
            return candidateSuccess > currentSuccess;
        }

        double candidateP95 = candidate.Candidate.GetAggregateP95(candidate.TargetIds);
        double currentP95 = current.Candidate.GetAggregateP95(current.TargetIds);
        if (Math.Abs(candidateP95 - currentP95) > Epsilon)
        {
            return candidateP95 < currentP95;
        }

        return string.CompareOrdinal(
            candidate.Candidate.Definition.Id,
            current.Candidate.Definition.Id) < 0;
    }

    private readonly record struct CandidateChoice(
        CandidateEvaluation Candidate,
        string[] Routes,
        string[] TargetIds,
        double Score);
}
