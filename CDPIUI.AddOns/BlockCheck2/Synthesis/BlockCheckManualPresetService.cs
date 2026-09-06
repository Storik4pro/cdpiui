using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Synthesis;

public sealed class BlockCheckManualPresetService
{
    private readonly Zapret2ConfigWriter _writer;

    public BlockCheckManualPresetService(Zapret2ConfigWriter? writer = null)
    {
        _writer = writer ?? new Zapret2ConfigWriter();
    }

    public IReadOnlyList<BlockCheckStrategyEvidence> BuildEvidence(
        StrategyCatalog catalog,
        IEnumerable<BlockCheckTarget> targets,
        IEnumerable<ProbeResult> probeResults,
        IEnumerable<ProbeResult>? baselineProbeResults,
        IReadOnlySet<string>? ignoredTargetIds,
        BlockCheckManualPresetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(probeResults);
        options ??= new BlockCheckManualPresetOptions();
        ignoredTargetIds ??= new HashSet<string>(StringComparer.Ordinal);

        Dictionary<string, StrategyDefinition> strategies = catalog.Strategies
            .ToDictionary(strategy => strategy.Id, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BlockCheckTarget> targetsById = targets
            .Where(target => !ignoredTargetIds.Contains(target.Id))
            .ToDictionary(target => target.Id, StringComparer.Ordinal);
        Dictionary<string, ProbeSummary> baseline = SummariesByTarget(baselineProbeResults);

        IGrouping<(string StrategyId, string TargetId), ProbeResult>[] probeGroups = probeResults
            .Where(result => targetsById.ContainsKey(result.TargetId) &&
                             strategies.ContainsKey(result.StrategyId))
            .GroupBy(result => (result.StrategyId, result.TargetId))
            .ToArray();
        Dictionary<(string StrategyId, string TargetId), ProbeSummary> strategySummaries = probeGroups
            .ToDictionary(
                group => group.Key,
                group => ProbeSummary.FromAttempts(group.SelectMany(result => result.Attempts)));

        List<BlockCheckStrategyEvidence> evidence = [];
        foreach (IGrouping<(string StrategyId, string TargetId), ProbeResult> group in probeGroups)
        {
            StrategyDefinition strategy = strategies[group.Key.StrategyId];
            BlockCheckTarget target = targetsById[group.Key.TargetId];
            ProbeSummary summary = strategySummaries[group.Key];
            ProbeAttempt[] attempts = group.SelectMany(result => result.Attempts).ToArray();
            evidence.Add(new BlockCheckStrategyEvidence
            {
                Strategy = strategy,
                Target = target,
                Summary = summary,
                BaselineSummary = baseline.GetValueOrDefault(target.Id) ?? new ProbeSummary(),
                StrategyArguments = _writer.FormatStrategyActions(strategy),
                Status = GetRouteAwareStatus(
                    strategy.Id,
                    target,
                    summary,
                    targetsById.Values,
                    strategySummaries,
                    options),
                HttpStatusCodes = attempts
                    .Select(attempt => attempt.HttpStatusCode)
                    .Where(status => status > 0)
                    .Distinct()
                    .Order()
                    .ToArray(),
                FailureCodes = attempts
                    .Select(attempt => attempt.FailureCode)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            });
        }

        return evidence
            .OrderBy(item => item.Target.Host, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Target.Protocol)
            .ThenBy(item => item.Strategy.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public BlockCheckManualPresetResult Build(
        StrategyCatalog catalog,
        IEnumerable<BlockCheckTarget> targets,
        IEnumerable<BlockCheckManualAssignment> assignments,
        IEnumerable<ProbeResult>? probeResults = null,
        BlockCheckManualPresetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(assignments);
        options ??= new BlockCheckManualPresetOptions();

        BlockCheckTarget[] targetArray = targets.ToArray();
        Dictionary<string, BlockCheckTarget> targetsById = targetArray
            .ToDictionary(target => target.Id, StringComparer.Ordinal);
        Dictionary<string, StrategyDefinition> strategies = catalog.Strategies
            .ToDictionary(strategy => strategy.Id, StringComparer.OrdinalIgnoreCase);
        Dictionary<(string StrategyId, string TargetId), ProbeSummary> summaries =
            SummariesByStrategyAndTarget(probeResults);
        List<BlockCheckIssue> issues = [];
        List<ResolvedAssignment> resolved = [];

        foreach (BlockCheckManualAssignment assignment in assignments)
        {
            if (!strategies.TryGetValue(assignment.StrategyId, out StrategyDefinition? strategy))
            {
                issues.Add(Error(
                    "MANUAL_STRATEGY_UNKNOWN",
                    "The selected strategy is absent from the active catalog.",
                    assignment.StrategyId));
                continue;
            }
            if (!targetsById.TryGetValue(assignment.AnchorTargetId, out BlockCheckTarget? anchor))
            {
                issues.Add(Error(
                    "MANUAL_TARGET_UNKNOWN",
                    "The selected site is absent from this BlockCheck session.",
                    assignment.AnchorTargetId));
                continue;
            }

            BlockCheckTarget[] scopeTargets = ResolveScopeTargets(
                targetArray,
                anchor,
                assignment,
                issues);
            if (scopeTargets.Length == 0)
            {
                continue;
            }
            if (scopeTargets.Any(target => !strategy.AppliesTo(target)))
            {
                issues.Add(Error(
                    "MANUAL_STRATEGY_INAPPLICABLE",
                    "The selected strategy does not support every connection in this scope.",
                    strategy.Id));
                continue;
            }

            resolved.Add(new ResolvedAssignment(
                assignment,
                strategy,
                anchor,
                scopeTargets,
                ScopeKey(anchor, assignment)));
        }

        List<Zapret2ProfilePlan> profiles = [];
        foreach (IGrouping<string, ResolvedAssignment> group in resolved.GroupBy(
                     item => item.ScopeKey,
                     StringComparer.OrdinalIgnoreCase))
        {
            ResolvedAssignment first = group.First();
            ResolvedAssignment[] unique = group
                .DistinctBy(item => CandidateNormalizer.GetPlanSignature(item.Strategy), StringComparer.Ordinal)
                .ToArray();
            if (unique.Length != group.Count())
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Warning,
                    "MANUAL_DUPLICATE_STRATEGY",
                    "A duplicate strategy in the same scope was ignored.",
                    first.ScopeKey));
            }
            if (unique.Length > 1 && unique.Any(item => !item.Strategy.SupportsCircular))
            {
                issues.Add(Error(
                    "MANUAL_CIRCULAR_UNSUPPORTED",
                    "At least one selected strategy cannot be used in a circular combination.",
                    first.ScopeKey));
                continue;
            }

            WarnAboutEvidence(unique, summaries, options, issues);
            Zapret2ProfilePlan profile = new()
            {
                Filter = new Zapret2ProfileFilter
                {
                    IpVersion = first.Anchor.IpVersion,
                    Transport = first.Anchor.Transport,
                    Port = first.Anchor.Port,
                    Layer7Protocol = first.Anchor.Layer7Protocol,
                    Domains = first.Assignment.ScopeKind == BlockCheckManualScopeKind.Site
                        ? [BlockCheckTarget.NormalizeHost(first.Anchor.Host)]
                        : [],
                    HostListPaths = first.Assignment.ScopeKind == BlockCheckManualScopeKind.SiteList
                        ? [Path.GetFullPath(first.Assignment.SiteListPath!)]
                        : [],
                },
                Primary = unique[0].Strategy,
                TargetIds = first.ScopeTargets
                    .Select(target => target.Id)
                    .ToHashSet(StringComparer.Ordinal),
            };
            foreach (ResolvedAssignment fallback in unique.Skip(1))
            {
                profile.Fallbacks.Add(fallback.Strategy);
            }
            profiles.Add(profile);
        }

        WarnAboutOverlap(profiles, issues);
        Zapret2ProfilePlan[] ordered = OrderAndName(profiles);
        Zapret2WriteResult configuration = ordered.Length == 0
            ? new Zapret2WriteResult
            {
                Issues = issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error)
                    ? []
                    : [Error("MANUAL_ASSIGNMENTS_EMPTY", "Add at least one strategy to the manual preset.")],
            }
            : _writer.Write(ordered, options.WriterOptions);
        issues.AddRange(configuration.Issues);

        return new BlockCheckManualPresetResult
        {
            Profiles = ordered,
            Configuration = configuration,
            Issues = issues,
        };
    }

    private static BlockCheckTarget[] ResolveScopeTargets(
        IReadOnlyList<BlockCheckTarget> targets,
        BlockCheckTarget anchor,
        BlockCheckManualAssignment assignment,
        ICollection<BlockCheckIssue> issues)
    {
        if (assignment.ScopeKind == BlockCheckManualScopeKind.Site)
        {
            return targets
                .Where(target => SameRuntimeShape(target, anchor) &&
                                 string.Equals(
                                     BlockCheckTarget.NormalizeHost(target.Host),
                                     BlockCheckTarget.NormalizeHost(anchor.Host),
                                     StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        if (string.IsNullOrWhiteSpace(assignment.SiteListPath) ||
            !anchor.HostListPaths.Contains(assignment.SiteListPath, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "MANUAL_SITE_LIST_INVALID",
                "The selected site list is not associated with the current site.",
                assignment.SiteListPath));
            return [];
        }

        return targets
            .Where(target => SameRuntimeShape(target, anchor) &&
                             target.HostListPaths.Contains(
                                 assignment.SiteListPath,
                                 StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static bool SameRuntimeShape(BlockCheckTarget left, BlockCheckTarget right) =>
        left.IpVersion == right.IpVersion &&
        left.Transport == right.Transport &&
        left.Port == right.Port &&
        string.Equals(left.Layer7Protocol, right.Layer7Protocol, StringComparison.Ordinal);

    private static string ScopeKey(
        BlockCheckTarget anchor,
        BlockCheckManualAssignment assignment) =>
        $"{anchor.IpVersion}|{anchor.Transport}|{anchor.Port}|{anchor.Layer7Protocol}|" +
        (assignment.ScopeKind == BlockCheckManualScopeKind.Site
            ? $"site:{BlockCheckTarget.NormalizeHost(anchor.Host)}"
            : $"list:{Path.GetFullPath(assignment.SiteListPath ?? string.Empty)}");

    private static void WarnAboutEvidence(
        IReadOnlyList<ResolvedAssignment> assignments,
        IReadOnlyDictionary<(string StrategyId, string TargetId), ProbeSummary> summaries,
        BlockCheckManualPresetOptions options,
        ICollection<BlockCheckIssue> issues)
    {
        foreach (ResolvedAssignment assignment in assignments)
        {
            ProbeSummary[] evidence = assignment.ScopeTargets
                .Select(target => summaries.GetValueOrDefault((assignment.Strategy.Id, target.Id)))
                .Where(summary => summary != null)
                .Cast<ProbeSummary>()
                .ToArray();
            if (evidence.Length == 0)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Warning,
                    "MANUAL_STRATEGY_UNTESTED",
                    "This strategy has no scan evidence for the selected scope. Test it before using the preset.",
                    assignment.Strategy.Id));
                continue;
            }

            int passing = evidence.Count(summary =>
                summary.AttemptCount >= options.MinimumAttempts &&
                summary.SuccessRate >= options.MinimumSuccessRate);
            if (passing == 0)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Warning,
                    "MANUAL_STRATEGY_UNPROVEN",
                    "The strategy did not pass the configured threshold for any tested site in this scope.",
                    assignment.Strategy.Id));
            }
            else if (passing < assignment.ScopeTargets.Length)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Warning,
                    "MANUAL_SCOPE_PARTIAL",
                    $"The strategy passed {passing} of {assignment.ScopeTargets.Length} site connection(s) in this scope.",
                    assignment.Strategy.Id));
            }
        }
    }

    private static void WarnAboutOverlap(
        IReadOnlyList<Zapret2ProfilePlan> profiles,
        ICollection<BlockCheckIssue> issues)
    {
        for (int left = 0; left < profiles.Count; left++)
        {
            for (int right = left + 1; right < profiles.Count; right++)
            {
                if (!profiles[left].TargetIds.Overlaps(profiles[right].TargetIds))
                {
                    continue;
                }
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Warning,
                    "MANUAL_SCOPE_OVERLAP",
                    "Two manual scopes contain the same site connections. The more specific domain profile will be placed first; verify the generated order.",
                    $"{profiles[left].Primary.Id}/{profiles[right].Primary.Id}"));
            }
        }
    }

    private static Zapret2ProfilePlan[] OrderAndName(IEnumerable<Zapret2ProfilePlan> profiles)
    {
        Zapret2ProfilePlan[] ordered = profiles
            .OrderByDescending(profile => profile.Filter.Domains.Count > 0)
            .ThenByDescending(profile => profile.Filter.Domains
                .Select(domain => domain.Count(character => character == '.') + 1)
                .DefaultIfEmpty(0)
                .Max())
            .ThenBy(profile => profile.Filter.Transport)
            .ThenBy(profile => profile.Filter.Port)
            .ThenBy(profile => profile.Filter.Layer7Protocol, StringComparer.Ordinal)
            .ThenBy(profile => profile.Filter.IpVersion)
            .ThenBy(profile => profile.Primary.Id, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            ordered[index].Name = $"bc_manual_{index + 1}";
        }
        return ordered;
    }

    private static Dictionary<string, ProbeSummary> SummariesByTarget(
        IEnumerable<ProbeResult>? results) =>
        results == null
            ? new Dictionary<string, ProbeSummary>(StringComparer.Ordinal)
            : results
                .GroupBy(result => result.TargetId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => ProbeSummary.FromAttempts(group.SelectMany(result => result.Attempts)),
                    StringComparer.Ordinal);

    private static Dictionary<(string StrategyId, string TargetId), ProbeSummary>
        SummariesByStrategyAndTarget(IEnumerable<ProbeResult>? results) =>
        results == null
            ? []
            : results
                .GroupBy(result => (result.StrategyId, result.TargetId))
                .ToDictionary(
                    group => group.Key,
                    group => ProbeSummary.FromAttempts(group.SelectMany(result => result.Attempts)));

    private static BlockCheckEvidenceStatus GetStatus(
        ProbeSummary summary,
        BlockCheckManualPresetOptions options)
    {
        if (summary.AttemptCount == 0)
        {
            return BlockCheckEvidenceStatus.Untested;
        }
        if (summary.AttemptCount >= options.MinimumAttempts &&
            summary.SuccessRate >= options.MinimumSuccessRate)
        {
            return BlockCheckEvidenceStatus.Successful;
        }
        return summary.SuccessCount > 0
            ? BlockCheckEvidenceStatus.Partial
            : BlockCheckEvidenceStatus.Failed;
    }

    private static BlockCheckEvidenceStatus GetRouteAwareStatus(
        string strategyId,
        BlockCheckTarget target,
        ProbeSummary summary,
        IEnumerable<BlockCheckTarget> targets,
        IReadOnlyDictionary<(string StrategyId, string TargetId), ProbeSummary> summaries,
        BlockCheckManualPresetOptions options)
    {
        BlockCheckEvidenceStatus ownStatus = GetStatus(summary, options);
        if (ownStatus != BlockCheckEvidenceStatus.Successful ||
            target.Protocol is not (BlockCheckProtocol.Tls12 or BlockCheckProtocol.Tls13 or BlockCheckProtocol.TlsAuto))
        {
            return ownStatus;
        }

        BlockCheckTarget[] siblings = targets
            .Where(candidate =>
                !string.Equals(candidate.Id, target.Id, StringComparison.Ordinal) &&
                candidate.Protocol is BlockCheckProtocol.Tls12 or BlockCheckProtocol.Tls13 or BlockCheckProtocol.TlsAuto &&
                string.Equals(
                    BlockCheckTarget.NormalizeHost(candidate.Host),
                    BlockCheckTarget.NormalizeHost(target.Host),
                    StringComparison.OrdinalIgnoreCase) &&
                candidate.IpVersion == target.IpVersion &&
                candidate.Transport == target.Transport &&
                candidate.Port == target.Port &&
                string.Equals(candidate.Layer7Protocol, target.Layer7Protocol, StringComparison.Ordinal))
            .ToArray();
        if (siblings.Length == 0)
        {
            return ownStatus;
        }

        return siblings.All(sibling =>
                summaries.TryGetValue((strategyId, sibling.Id), out ProbeSummary? siblingSummary) &&
                GetStatus(siblingSummary, options) == BlockCheckEvidenceStatus.Successful)
            ? ownStatus
            : BlockCheckEvidenceStatus.Partial;
    }

    private static BlockCheckIssue Error(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Error, code, message, subjectId);

    private sealed record ResolvedAssignment(
        BlockCheckManualAssignment Assignment,
        StrategyDefinition Strategy,
        BlockCheckTarget Anchor,
        BlockCheckTarget[] ScopeTargets,
        string ScopeKey);
}
