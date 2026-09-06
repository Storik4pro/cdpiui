using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.AddOns.BlockCheck2.Reporting;

public sealed class BlockCheckReportBuilder
{
    public BlockCheckReport Build(
        StrategyCatalog catalog,
        BlockCheckRunPreset runPreset,
        IEnumerable<BlockCheckTarget> targets,
        BlockCheckRunResult result,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(result);

        HashSet<string> ignoredTargetIds = result.Scan.IgnoredTargetIds
            .ToHashSet(StringComparer.Ordinal);
        BlockCheckTarget[] targetArray = targets
            .Where(target => !ignoredTargetIds.Contains(target.Id))
            .ToArray();
        List<BlockCheckReportProbe> probes = [];
        probes.AddRange(result.Scan.BaselineResults
            .Where(probe => !ignoredTargetIds.Contains(probe.TargetId))
            .Select(probe => Probe("baseline", 0, probe)));
        probes.AddRange(result.Scan.ProbeResults
            .Where(probe => !ignoredTargetIds.Contains(probe.TargetId))
            .Select(probe => Probe("strategy", 0, probe)));
        if (result.Validation != null)
        {
            foreach (BlockCheckPresetValidationAttempt attempt in result.Validation.Attempts)
            {
                probes.AddRange(attempt.ProbeResults.Select(probe =>
                    Probe("preset-validation", attempt.CandidateNumber, probe)));
            }
        }

        IReadOnlyList<Zapret2ProfilePlan> profiles = result.Synthesis?.Profiles ?? [];
        return new BlockCheckReport
        {
            CreatedAtUtc = (createdAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            CatalogVersion = catalog.CatalogVersion,
            RunPreset = runPreset,
            Success = result.Success,
            WasCanceled = result.WasCanceled,
            IsBestEffort = result.Synthesis?.IsBestEffort == true,
            Targets = targetArray.Select(target => new BlockCheckReportTarget(
                    target.Id,
                    BlockCheckTarget.NormalizeHost(target.Host),
                    target.Path,
                    target.Protocol,
                    target.IpVersion,
                    target.Transport,
                    target.Port,
                    target.GetRuntimeRouteKey().ToString()))
                .ToArray(),
            Probes = probes,
            Profiles = profiles.Select(profile => new BlockCheckReportProfile(
                    profile.Name,
                    profile.Filter.IpVersion,
                    profile.Filter.Transport,
                    profile.Filter.Port,
                    profile.Filter.Layer7Protocol,
                    profile.Filter.Domains.ToArray(),
                    profile.Filter.HostListPaths.ToArray(),
                    profile.Primary.Id,
                    profile.Fallbacks.Select(strategy => strategy.Id).ToArray(),
                    profile.TargetIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                    profile.IsBestEffort))
                .ToArray(),
            ValidationAttempts = result.Validation?.Attempts.Select(attempt =>
                    new BlockCheckReportValidationAttempt(
                        attempt.CandidateNumber,
                        attempt.Success,
                        attempt.InfrastructureFailure,
                        attempt.FailedTargetIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                        attempt.ExcludedStrategyIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()))
                .ToArray() ?? [],
            Issues = CollectIssues(result),
            PresetArguments = result.Synthesis?.Configuration.CommandLine ?? string.Empty,
        };
    }

    private static BlockCheckReportProbe Probe(
        string kind,
        int candidateNumber,
        ProbeResult result)
    {
        ProbeSummary summary = ProbeSummary.FromAttempts(result.Attempts);
        return new BlockCheckReportProbe(
            kind,
            candidateNumber,
            result.StrategyId,
            result.TargetId,
            summary.AttemptCount,
            summary.SuccessCount,
            summary.SuccessRate,
            FiniteOrNull(summary.MedianTimeToFirstByteMs),
            FiniteOrNull(summary.P95TimeToFirstByteMs),
            result.Attempts
                .Select(attempt => attempt.HttpStatusCode)
                .Where(status => status > 0)
                .Distinct()
                .Order()
                .ToArray(),
            result.Attempts
                .Select(attempt => attempt.FailureCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IReadOnlyList<BlockCheckIssue> CollectIssues(BlockCheckRunResult result) =>
        result.PreflightIssues
            .Concat(result.Scan.Issues)
            .Concat(result.Synthesis?.Issues ?? [])
            .Concat(result.Validation?.Issues ?? [])
            .DistinctBy(issue => (issue.Severity, issue.Code, issue.Message, issue.SubjectId))
            .ToArray();

    private static double? FiniteOrNull(double value) => double.IsFinite(value) ? value : null;
}
