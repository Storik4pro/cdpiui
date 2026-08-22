using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Synthesis;

public sealed class BlockCheckSynthesisService
{
    private readonly StrategyCatalogValidator _validator = new();
    private readonly ParetoReducer _paretoReducer = new();
    private readonly CoverageOptimizer _coverageOptimizer = new();
    private readonly BestEffortCoverageOptimizer _bestEffortOptimizer = new();
    private readonly ProfilePlanner _profilePlanner = new();
    private readonly CircularFallbackPlanner _fallbackPlanner = new();
    private readonly Zapret2ConfigWriter _writer = new();

    public BlockCheckSynthesisResult Synthesize(
        StrategyCatalog catalog,
        IEnumerable<BlockCheckTarget> targets,
        IEnumerable<ProbeResult> probeResults,
        BlockCheckSynthesisOptions? options = null,
        IEnumerable<ProbeResult>? baselineProbeResults = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(probeResults);
        options ??= new BlockCheckSynthesisOptions();

        BlockCheckTarget[] targetArray = targets.ToArray();
        List<BlockCheckIssue> issues = [];
        issues.AddRange(_validator.Validate(catalog));
        issues.AddRange(_validator.ValidateTargets(targetArray));

        if (issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return Failed(issues);
        }

        HashSet<string> baselineAccessibleTargetIds = GetBaselineAccessibleTargetIds(
            targetArray,
            baselineProbeResults,
            options,
            issues);
        BlockCheckTarget[] targetsRequiringProfile = targetArray
            .GroupBy(target => target.GetRuntimeRouteKey())
            .Where(group => group.Any(target => !baselineAccessibleTargetIds.Contains(target.Id)))
            .SelectMany(group => group)
            .ToArray();

        if (targetsRequiringProfile.Length == 0)
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Info,
                "NO_BYPASS_REQUIRED",
                "All requested transport routes passed the curl baseline, so no Zapret2 profile " +
                "is required for those exact routes. This does not verify browser rendering or a " +
                "different protocol such as HTTPS after an HTTP redirect."));
            return new BlockCheckSynthesisResult
            {
                BaselineAccessibleTargetIds = baselineAccessibleTargetIds,
                Configuration = new Zapret2WriteResult(),
                Issues = issues,
            };
        }

        IReadOnlyList<CandidateEvaluation> allEvaluations =
            ProbeResultAnalyzer.BuildEvaluations(catalog, probeResults, issues);
        IReadOnlyList<CandidateEvaluation> reduced = _paretoReducer.Reduce(allEvaluations, options);
        CoverageSelectionResult selection = _coverageOptimizer.Select(targetsRequiringProfile, reduced, options);
        IReadOnlyDictionary<string, ProbeSummary> baselineSummaries = BuildProbeSummaries(
            baselineProbeResults);
        IReadOnlyList<StrategyAssignment> bestEffortAssignments =
            options.EnableBestEffort && selection.UncoveredTargetIds.Count > 0
                ? _bestEffortOptimizer.Select(
                    targetsRequiringProfile,
                    allEvaluations,
                    selection.UncoveredTargetIds,
                    baselineSummaries,
                    options)
                : [];
        CoverageSelectionResult profileSelection = bestEffortAssignments.Count == 0
            ? selection
            : new CoverageSelectionResult
            {
                Assignments = selection.Assignments
                    .Concat(bestEffortAssignments)
                    .ToArray(),
                UncoveredTargetIds = selection.UncoveredTargetIds,
            };

        if (selection.UncoveredTargetIds.Count > 0)
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Warning,
                "TARGETS_UNCOVERED",
                $"No compatible strategy covers {selection.UncoveredTargetIds.Count} test target(s)."));

            HashSet<string> uncovered = selection.UncoveredTargetIds
                .ToHashSet(StringComparer.Ordinal);
            bool hasAtomicSiteListGap = targetsRequiringProfile
                .Where(target => uncovered.Contains(target.Id))
                .Any(target => target.HostListPaths.Count > 0) &&
                uncovered.Any(targetId =>
                    allEvaluations.Any(candidate => candidate.IsSuccessful(targetId, options)));
            if (hasAtomicSiteListGap)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Warning,
                    "NO_COMMON_SITE_LIST_STRATEGY",
                    "Some domains passed with different strategies, but no single strategy " +
                    "passed the strict threshold for the complete selected site list. The list " +
                    "was kept atomic; a best-effort profile may therefore remain only partially effective."));
            }
        }

        IReadOnlyList<Zapret2ProfilePlan> profiles = _profilePlanner.Plan(
            profileSelection,
            targetsRequiringProfile);
        _fallbackPlanner.Apply(profiles, allEvaluations, targetsRequiringProfile, options);

        if (bestEffortAssignments.Count > 0)
        {
            int targetCount = bestEffortAssignments
                .SelectMany(assignment => assignment.TargetIds)
                .Distinct(StringComparer.Ordinal)
                .Count();
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Warning,
                "BEST_EFFORT_CONFIG",
                $"No strategy passed the strict threshold for {targetCount} target(s). " +
                "A best-effort config was generated from the strategy with the strongest " +
                "observed improvement. It is not a fully validated result and some sites may remain unavailable."));
        }

        Zapret2WriteResult configuration = profiles.Count == 0
            ? new Zapret2WriteResult
            {
                Issues =
                [
                    BuildNoProfilesIssue(
                        targetsRequiringProfile,
                        selection,
                        allEvaluations,
                        options),
                ],
            }
            : _writer.Write(profiles, new Zapret2WriterOptions
            {
                CircularFailureThreshold = options.CircularFailureThreshold,
                MakeLastFallbackFinal = options.MakeLastFallbackFinal,
            });

        issues.AddRange(configuration.Issues);
        return new BlockCheckSynthesisResult
        {
            BaselineAccessibleTargetIds = baselineAccessibleTargetIds,
            Selection = selection,
            Profiles = profiles,
            Configuration = configuration,
            Issues = issues,
            IsBestEffort = bestEffortAssignments.Count > 0,
        };
    }

    private static HashSet<string> GetBaselineAccessibleTargetIds(
        IReadOnlyList<BlockCheckTarget> targets,
        IEnumerable<ProbeResult>? baselineProbeResults,
        BlockCheckSynthesisOptions options,
        ICollection<BlockCheckIssue> issues)
    {
        if (baselineProbeResults == null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        HashSet<string> targetIds = targets
            .Select(target => target.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> accessible = new(StringComparer.Ordinal);
        foreach (IGrouping<string, ProbeResult> group in baselineProbeResults
                     .GroupBy(result => result.TargetId, StringComparer.Ordinal))
        {
            if (!targetIds.Contains(group.Key))
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Warning,
                    "BASELINE_TARGET_UNKNOWN",
                    "Baseline result refers to an unknown target.",
                    group.Key));
                continue;
            }

            ProbeSummary summary = ProbeSummary.FromAttempts(group.SelectMany(result => result.Attempts));
            if (summary.AttemptCount >= options.MinimumAttempts &&
                summary.SuccessRate >= options.MinimumSuccessRate)
            {
                accessible.Add(group.Key);
            }
        }

        return accessible;
    }

    private static BlockCheckSynthesisResult Failed(IReadOnlyList<BlockCheckIssue> issues) => new()
    {
        Configuration = new Zapret2WriteResult { Issues = issues },
        Issues = issues,
    };

    private static IReadOnlyDictionary<string, ProbeSummary> BuildProbeSummaries(
        IEnumerable<ProbeResult>? probeResults) =>
        probeResults == null
            ? new Dictionary<string, ProbeSummary>(StringComparer.Ordinal)
            : probeResults
                .GroupBy(result => result.TargetId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => ProbeSummary.FromAttempts(group.SelectMany(result => result.Attempts)),
                    StringComparer.Ordinal);

    private static BlockCheckIssue BuildNoProfilesIssue(
        IReadOnlyList<BlockCheckTarget> targets,
        CoverageSelectionResult selection,
        IReadOnlyList<CandidateEvaluation> evaluations,
        BlockCheckSynthesisOptions options)
    {
        HashSet<string> uncovered = selection.UncoveredTargetIds.ToHashSet(StringComparer.Ordinal);
        int individuallyCovered = uncovered.Count(targetId =>
            evaluations.Any(candidate => candidate.IsSuccessful(targetId, options)));
        bool usesSiteLists = targets
            .Where(target => uncovered.Contains(target.Id))
            .Any(target => target.HostListPaths.Count > 0);

        if (individuallyCovered == 0)
        {
            return new BlockCheckIssue(
                BlockCheckIssueSeverity.Error,
                "NO_EFFECTIVE_STRATEGY",
                $"None of the tested strategies met the success threshold for the " +
                $"{uncovered.Count} target(s) that failed without Zapret2. A config was not " +
                "emitted because it would not improve the baseline result. Try a broader scan mode.");
        }

        if (usesSiteLists)
        {
            return new BlockCheckIssue(
                BlockCheckIssueSeverity.Error,
                "NO_COMMON_SITE_LIST_STRATEGY",
                "Some domains passed with different strategies, but no single compatible " +
                "strategy covers the complete selected site list. Emitting the original list " +
                "under several conflicting profiles would route domains through the wrong profile.");
        }

        return new BlockCheckIssue(
            BlockCheckIssueSeverity.Error,
            "NO_PROFILES",
            "No compatible profile could be generated from the successful probe results.");
    }
}
