using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Analysis;

/// <summary>
/// Chooses one evidence-backed strategy for each atomic target group that the
/// strict optimizer could not cover. A selected site list stays atomic: the
/// same list is never emitted under several conflicting strategies.
/// </summary>
public sealed class BestEffortCoverageOptimizer
{
    public IReadOnlyList<StrategyAssignment> Select(
        IEnumerable<BlockCheckTarget> targets,
        IEnumerable<CandidateEvaluation> candidates,
        IReadOnlySet<string> uncoveredTargetIds,
        IReadOnlyDictionary<string, ProbeSummary> baseline,
        BlockCheckSynthesisOptions options)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(uncoveredTargetIds);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(options);

        CandidateEvaluation[] candidateArray = candidates.ToArray();
        List<StrategyAssignment> assignments = [];
        foreach (BlockCheckTargetGroup group in BlockCheckTargetGroupBuilder.Build(targets)
                     .Where(group => group.Targets.All(target =>
                         uncoveredTargetIds.Contains(target.Id))))
        {
            CandidateScore? best = candidateArray
                .Where(candidate => group.Targets.All(candidate.Definition.AppliesTo))
                .Select(candidate => Score(candidate, group.Targets, baseline, options))
                .Where(score => score.ImprovesBaseline)
                .OrderByDescending(score => score.PassedTargets)
                .ThenByDescending(score => score.SuccessfulTargets)
                .ThenByDescending(score => score.SuccessfulAttempts)
                .ThenByDescending(score => score.AggregateSuccessRate)
                .ThenBy(score => score.P95TimeToFirstByteMs)
                .ThenBy(score => score.Candidate.Definition.BaseCost)
                .ThenBy(score => score.Candidate.Definition.Id, StringComparer.Ordinal)
                .Select(score => (CandidateScore?)score)
                .FirstOrDefault();
            if (!best.HasValue)
            {
                continue;
            }

            assignments.Add(new StrategyAssignment
            {
                Candidate = best.Value.Candidate,
                TargetIds = group.Targets
                    .Select(target => target.Id)
                    .ToHashSet(StringComparer.Ordinal),
                RouteKeys = group.Targets
                    .Select(target => target.GetRuntimeRouteKey())
                    .ToHashSet(),
                IsBestEffort = true,
            });
        }

        return assignments;
    }

    private static CandidateScore Score(
        CandidateEvaluation candidate,
        IReadOnlyList<BlockCheckTarget> targets,
        IReadOnlyDictionary<string, ProbeSummary> baseline,
        BlockCheckSynthesisOptions options)
    {
        ProbeSummary[] results = targets
            .Select(target => candidate.Results.GetValueOrDefault(target.Id) ?? new ProbeSummary())
            .ToArray();
        ProbeSummary[] baselineResults = targets
            .Select(target => baseline.GetValueOrDefault(target.Id) ?? new ProbeSummary())
            .ToArray();
        int passedTargets = targets.Count(target => candidate.IsSuccessful(target.Id, options));
        int baselinePassedTargets = baselineResults.Count(summary =>
            summary.AttemptCount >= options.MinimumAttempts &&
            summary.SuccessRate >= options.MinimumSuccessRate);
        int successfulAttempts = results.Sum(summary => summary.SuccessCount);
        int baselineSuccessfulAttempts = baselineResults.Sum(summary => summary.SuccessCount);
        int attempts = results.Sum(summary => summary.AttemptCount);
        double[] timings = results
            .Where(summary => summary.SuccessCount > 0 &&
                              double.IsFinite(summary.P95TimeToFirstByteMs))
            .Select(summary => summary.P95TimeToFirstByteMs)
            .ToArray();

        return new CandidateScore(
            candidate,
            passedTargets,
            results.Count(summary => summary.SuccessCount > 0),
            successfulAttempts,
            attempts == 0 ? 0d : (double)successfulAttempts / attempts,
            timings.Length == 0 ? double.PositiveInfinity : timings.Max(),
            passedTargets > baselinePassedTargets ||
            successfulAttempts > baselineSuccessfulAttempts);
    }

    private readonly record struct CandidateScore(
        CandidateEvaluation Candidate,
        int PassedTargets,
        int SuccessfulTargets,
        int SuccessfulAttempts,
        double AggregateSuccessRate,
        double P95TimeToFirstByteMs,
        bool ImprovesBaseline);
}
