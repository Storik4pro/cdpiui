using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

public sealed class BlockCheckScanService
{
    private readonly StrategyCatalogValidator _validator;
    private readonly Zapret2ConfigWriter _writer;

    public BlockCheckScanService(
        StrategyCatalogValidator? validator = null,
        Zapret2ConfigWriter? writer = null)
    {
        _validator = validator ?? new StrategyCatalogValidator();
        _writer = writer ?? new Zapret2ConfigWriter();
    }

    public async Task<BlockCheckScanResult> ScanAsync(
        StrategyCatalog catalog,
        IEnumerable<BlockCheckTarget> targets,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckScanOptions? options = null,
        IProgress<BlockCheckScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(strategyRunner);
        ArgumentNullException.ThrowIfNull(probeRunner);

        options ??= new BlockCheckScanOptions();
        BlockCheckTarget[] targetArray = targets.ToArray();
        List<BlockCheckIssue> issues =
        [
            .. _validator.Validate(catalog),
            .. _validator.ValidateTargets(targetArray),
        ];

        ValidateOptions(options, issues);

        HashSet<string> catalogIds = catalog.Strategies
            .Select(strategy => strategy.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? selectedIds = options.StrategyIds?
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedIds != null)
        {
            foreach (string unknownId in selectedIds.Where(id => !catalogIds.Contains(id)))
            {
                issues.Add(Error("SCAN_STRATEGY_UNKNOWN", "Requested strategy is absent from the catalog.", unknownId));
            }
        }

        if (issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return new BlockCheckScanResult { Issues = issues };
        }

        bool prepareAttempted = false;
        try
        {
            prepareAttempted = true;
            await strategyRunner.PrepareAsync(cancellationToken).ConfigureAwait(false);
            return await ScanPreparedAsync(
                    catalog,
                    targetArray,
                    selectedIds,
                    strategyRunner,
                    probeRunner,
                    options,
                    progress,
                    issues,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            issues.Add(CanceledIssue());
            return new BlockCheckScanResult
            {
                Issues = issues,
                WasCanceled = true,
            };
        }
        catch (Exception exception)
        {
            issues.Add(Error(
                "SCAN_PREPARE_FAILED",
                $"Could not prepare the BlockCheck process environment: {exception.Message}"));
            return new BlockCheckScanResult { Issues = issues };
        }
        finally
        {
            if (prepareAttempted)
            {
                try
                {
                    await strategyRunner.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    issues.Add(Error(
                        "SCAN_COMPLETE_FAILED",
                        $"Could not restore the process environment: {exception.Message}"));
                }
            }
        }
    }

    private async Task<BlockCheckScanResult> ScanPreparedAsync(
        StrategyCatalog catalog,
        BlockCheckTarget[] targetArray,
        HashSet<string>? selectedIds,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckScanOptions options,
        IProgress<BlockCheckScanProgress>? progress,
        List<BlockCheckIssue> issues,
        CancellationToken cancellationToken)
    {
        BaselineProbeOutcome baseline = await ProbeBaselineAsync(
                targetArray,
                probeRunner,
                options,
                progress,
                issues,
                cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<ProbeResult> baselineResults = baseline.Results;

        if (baseline.WasCanceled)
        {
            issues.Add(CanceledIssue());
            return new BlockCheckScanResult
            {
                BaselineResults = baselineResults,
                Issues = issues,
                WasCanceled = true,
            };
        }

        int successfulChecks = baselineResults.Sum(result => result.Attempts.Count(attempt => attempt.Success));
        int failedChecks = baselineResults.Sum(result => result.Attempts.Count(attempt => !attempt.Success));
        int successfulStrategies = 0;

        HashSet<string> ignoredTargetIds = options.RunBaseline
            ? baselineResults
                .Where(IsDnsUnresolved)
                .Select(result => result.TargetId)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        BlockCheckTarget[] activeTargets = targetArray
            .Where(target => !ignoredTargetIds.Contains(target.Id))
            .ToArray();
        if (ignoredTargetIds.Count > 0)
        {
            string[] ignoredHosts = targetArray
                .Where(target => ignoredTargetIds.Contains(target.Id))
                .Select(target => BlockCheckTarget.NormalizeHost(target.Host))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(host => host, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string preview = string.Join(", ", ignoredHosts.Take(5));
            if (ignoredHosts.Length > 5)
            {
                preview += $", +{ignoredHosts.Length - 5} more";
            }
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Warning,
                "DNS_TARGETS_IGNORED",
                $"Ignored {ignoredTargetIds.Count} target(s) for {ignoredHosts.Length} host(s) because " +
                $"the selected DNS resolver returned no address: {preview}."));
        }

        if (activeTargets.Length == 0)
        {
            issues.Add(Error(
                "NO_RESOLVABLE_TARGETS",
                "None of the requested URLs has an address in the selected DNS resolver."));
            return new BlockCheckScanResult
            {
                BaselineResults = baselineResults,
                IgnoredTargetIds = ignoredTargetIds,
                Issues = issues,
            };
        }

        Dictionary<string, ProbeResult> baselineByTarget = baselineResults
            .ToDictionary(result => result.TargetId, StringComparer.Ordinal);
        RouteScanGroup[] allRoutes = BlockCheckTargetGroupBuilder.Build(activeTargets)
            .Select(group => new RouteScanGroup(group.Id, group.Targets))
            .ToArray();
        RouteScanGroup[] routes = allRoutes
            .Where(route => !ShouldSkipBaselineAccessibleRoute(route, baselineByTarget, options))
            .ToArray();

        StrategyDefinition[] strategies = OrderStrategiesProgressively(catalog.Strategies
            .Where(strategy => selectedIds != null
                ? selectedIds.Contains(strategy.Id)
                : strategy.ScanTier <= options.MaximumTier));

        Dictionary<string, StrategyDefinition[]> strategiesByRoute = routes.ToDictionary(
            route => route.Key,
            route => strategies
                .Where(strategy => route.Targets.All(strategy.AppliesTo))
                .ToArray());
        int totalJobs = strategiesByRoute.Values.Sum(routeStrategies => routeStrategies.Length);

        if (routes.Length > 0 && totalJobs == 0)
        {
            issues.Add(Error(
                "SCAN_NO_APPLICABLE_STRATEGIES",
                "No selected catalog strategy applies to every target in the requested runtime routes."));
            return new BlockCheckScanResult
            {
                BaselineResults = baselineResults,
                IgnoredTargetIds = ignoredTargetIds,
                Issues = issues,
            };
        }

        List<ProbeResult> results = [];
        int completed = 0;
        bool wasCanceled = false;
        foreach (RouteScanGroup route in routes)
        {
            HashSet<string> successfulPlans = new(StringComparer.Ordinal);
            foreach (StrategyDefinition strategy in strategiesByRoute[route.Key])
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    wasCanceled = true;
                    break;
                }
                string progressTargetId = route.Targets[0].Id;
                progress?.Report(new BlockCheckScanProgress(
                    BlockCheckScanPhase.StartingStrategy,
                    completed,
                    totalJobs,
                    progressTargetId,
                    strategy.Id,
                    SuccessfulChecks: successfulChecks,
                    FailedChecks: failedChecks,
                    SuccessfulStrategies: successfulStrategies));

                Zapret2WriteResult configuration = _writer.Write(
                    [BuildProfile(route, strategy)],
                    options.WriterOptions);
                if (!configuration.Success)
                {
                    issues.AddRange(configuration.Issues.Select(issue => new BlockCheckIssue(
                        BlockCheckIssueSeverity.Warning,
                        "SCAN_STRATEGY_CONFIG_FAILED",
                        $"Strategy configuration could not be generated: {issue.Message}",
                        strategy.Id)));
                    results.AddRange(CreateFailedRouteResults(
                        route,
                        strategy.Id,
                        "strategy_config_failed",
                        "Zapret2 arguments could not be generated for this strategy."));
                    failedChecks += route.Targets.Length;
                    completed++;
                    progress?.Report(new BlockCheckScanProgress(
                        BlockCheckScanPhase.StrategyCompleted,
                        completed,
                        totalJobs,
                        progressTargetId,
                        strategy.Id,
                        SuccessfulChecks: successfulChecks,
                        FailedChecks: failedChecks,
                        SuccessfulStrategies: successfulStrategies));
                    continue;
                }

                Dictionary<string, List<ProbeAttempt>> attemptsByTarget = route.Targets
                    .ToDictionary(target => target.Id, _ => new List<ProbeAttempt>(), StringComparer.Ordinal);
                bool startAttempted = false;
                try
                {
                    startAttempted = true;
                    await strategyRunner.StartAsync(configuration.CommandLine, cancellationToken)
                        .ConfigureAwait(false);

                    foreach (BlockCheckTarget target in route.Targets)
                    {
                        List<ProbeAttempt> attempts = attemptsByTarget[target.Id];
                        for (int attempt = 1; attempt <= options.AttemptsPerTarget; attempt++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            progress?.Report(new BlockCheckScanProgress(
                                BlockCheckScanPhase.Probing,
                                completed,
                                totalJobs,
                                target.Id,
                                strategy.Id,
                                attempt,
                                successfulChecks,
                                failedChecks,
                                successfulStrategies));
                            try
                            {
                                ProbeAttempt probeAttempt = await probeRunner
                                    .ProbeAsync(target, cancellationToken)
                                    .ConfigureAwait(false);
                                attempts.Add(probeAttempt);
                                if (probeAttempt.Success)
                                {
                                    successfulChecks++;
                                }
                                else
                                {
                                    failedChecks++;
                                }
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                attempts.Add(FailedAttempt(
                                    "target_probe_failed",
                                    exception.Message));
                                failedChecks++;
                                issues.Add(Warning(
                                    "SCAN_TARGET_PROBE_FAILED",
                                    $"Strategy/target scan failed: {exception.Message}",
                                    $"{strategy.Id}:{target.Id}"));
                                progress?.Report(new BlockCheckScanProgress(
                                    BlockCheckScanPhase.Probing,
                                    completed,
                                    totalJobs,
                                    target.Id,
                                    strategy.Id,
                                    attempt,
                                    successfulChecks,
                                    failedChecks,
                                    successfulStrategies));
                                break;
                            }

                            progress?.Report(new BlockCheckScanProgress(
                                BlockCheckScanPhase.Probing,
                                completed,
                                totalJobs,
                                target.Id,
                                strategy.Id,
                                attempt,
                                successfulChecks,
                                failedChecks,
                                successfulStrategies));
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    wasCanceled = true;
                }
                catch (Exception exception)
                {
                    foreach (List<ProbeAttempt> attempts in attemptsByTarget.Values
                                 .Where(attempts => attempts.Count == 0))
                    {
                        attempts.Add(FailedAttempt(
                            "strategy_start_failed",
                            exception.Message));
                        failedChecks++;
                    }
                    issues.Add(Warning(
                        "SCAN_STRATEGY_FAILED",
                        $"Strategy/route scan failed: {exception.Message}",
                        $"{strategy.Id}:{route.Key}"));
                }
                finally
                {
                    if (startAttempted)
                    {
                        try
                        {
                            await strategyRunner.StopAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            issues.Add(Warning(
                                "SCAN_STOP_FAILED",
                                $"Could not stop strategy process: {exception.Message}",
                                strategy.Id));
                        }
                    }
                }

                ProbeResult[] routeResults = route.Targets
                    .Where(target => attemptsByTarget[target.Id].Count > 0)
                    .Select(target => new ProbeResult
                    {
                        StrategyId = strategy.Id,
                        TargetId = target.Id,
                        Attempts = attemptsByTarget[target.Id],
                    })
                    .ToArray();
                results.AddRange(routeResults);
                if (!wasCanceled &&
                    routeResults.Length == route.Targets.Length &&
                    routeResults.All(result => IsSuccessful(result, options.SuccessfulStrategyRate)))
                {
                    successfulPlans.Add(CandidateNormalizer.GetPlanSignature(strategy));
                    successfulStrategies++;
                }

                if (wasCanceled)
                {
                    break;
                }

                completed++;
                progress?.Report(new BlockCheckScanProgress(
                    BlockCheckScanPhase.StrategyCompleted,
                    completed,
                    totalJobs,
                    progressTargetId,
                    strategy.Id,
                    routeResults.Sum(result => result.Attempts.Count),
                    successfulChecks,
                    failedChecks,
                    successfulStrategies));

                if (options.EnableRouteEarlyStop &&
                    successfulPlans.Count >= options.SuccessfulStrategiesPerRoute)
                {
                    break;
                }
            }

            if (wasCanceled)
            {
                break;
            }
        }

        if (wasCanceled)
        {
            issues.Add(CanceledIssue());
        }

        return new BlockCheckScanResult
        {
            BaselineResults = baselineResults,
            ProbeResults = results,
            IgnoredTargetIds = ignoredTargetIds,
            Issues = issues,
            WasCanceled = wasCanceled,
        };
    }

    private static async Task<BaselineProbeOutcome> ProbeBaselineAsync(
        IReadOnlyList<BlockCheckTarget> targets,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckScanOptions options,
        IProgress<BlockCheckScanProgress>? progress,
        ICollection<BlockCheckIssue> issues,
        CancellationToken cancellationToken)
    {
        List<ProbeResult> results = [];
        if (!options.RunBaseline)
        {
            return new BaselineProbeOutcome(results, false);
        }

        int successfulChecks = 0;
        int failedChecks = 0;
        int completedChecks = 0;
        int totalChecks = targets.Count * options.AttemptsPerTarget;

        foreach (BlockCheckTarget target in targets)
        {
            List<ProbeAttempt> attempts = [];
            try
            {
                for (int attempt = 1; attempt <= options.AttemptsPerTarget; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new BlockCheckScanProgress(
                        BlockCheckScanPhase.BaselineProbing,
                        completedChecks,
                        totalChecks,
                        target.Id,
                        string.Empty,
                        attempt,
                        successfulChecks,
                        failedChecks));
                    try
                    {
                        ProbeAttempt probeAttempt = await probeRunner
                            .ProbeAsync(target, cancellationToken)
                            .ConfigureAwait(false);
                        attempts.Add(probeAttempt);
                        if (probeAttempt.Success)
                        {
                            successfulChecks++;
                        }
                        else
                        {
                            failedChecks++;
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        attempts.Add(FailedAttempt("baseline_probe_failed", exception.Message));
                        failedChecks++;
                        issues.Add(Error(
                            "SCAN_BASELINE_FAILED",
                            $"Baseline probe failed: {exception.Message}",
                            target.Id));
                    }

                    completedChecks++;
                    progress?.Report(new BlockCheckScanProgress(
                        BlockCheckScanPhase.BaselineProbing,
                        completedChecks,
                        totalChecks,
                        target.Id,
                        string.Empty,
                        attempt,
                        successfulChecks,
                        failedChecks));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (attempts.Count > 0)
                {
                    results.Add(new ProbeResult
                    {
                        StrategyId = string.Empty,
                        TargetId = target.Id,
                        Attempts = attempts,
                    });
                }

                return new BaselineProbeOutcome(results, true);
            }

            results.Add(new ProbeResult
            {
                StrategyId = string.Empty,
                TargetId = target.Id,
                Attempts = attempts,
            });
        }

        return new BaselineProbeOutcome(results, false);
    }

    private static bool ShouldSkipBaselineAccessibleRoute(
        RouteScanGroup route,
        IReadOnlyDictionary<string, ProbeResult> baselineByTarget,
        BlockCheckScanOptions options) =>
        options.RunBaseline &&
        options.SkipFullyBaselineAccessibleRoutes &&
        route.Targets.All(target =>
            baselineByTarget.TryGetValue(target.Id, out ProbeResult? result) &&
            IsSuccessful(result, options.SuccessfulStrategyRate));

    private static bool IsDnsUnresolved(ProbeResult result) =>
        result.Attempts.Count > 0 &&
        result.Attempts.All(attempt =>
            !attempt.Success &&
            string.Equals(
                attempt.FailureCode,
                "dns-resolution-failed",
                StringComparison.OrdinalIgnoreCase));

    private static bool IsSuccessful(ProbeResult result, double minimumRate)
    {
        ProbeSummary summary = ProbeSummary.FromAttempts(result.Attempts);
        return summary.AttemptCount > 0 && summary.SuccessRate >= minimumRate;
    }

    private static StrategyDefinition[] OrderStrategiesProgressively(
        IEnumerable<StrategyDefinition> strategies)
    {
        List<StrategyDefinition> result = [];
        foreach (IGrouping<BlockCheckScanTier, StrategyDefinition> tier in strategies
                     .GroupBy(strategy => strategy.ScanTier)
                     .OrderBy(group => group.Key))
        {
            StrategyDefinition[][] families = tier
                .GroupBy(
                    strategy => string.IsNullOrWhiteSpace(strategy.Family)
                        ? strategy.Id
                        : strategy.Family,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(strategy => strategy.BaseCost)
                    .ThenBy(strategy => strategy.Id, StringComparer.Ordinal)
                    .ToArray())
                .OrderBy(group => group[0].BaseCost)
                .ThenBy(group => group[0].Family, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            int maximumFamilySize = families.Length == 0
                ? 0
                : families.Max(family => family.Length);
            for (int index = 0; index < maximumFamilySize; index++)
            {
                foreach (StrategyDefinition[] family in families)
                {
                    if (index < family.Length)
                    {
                        result.Add(family[index]);
                    }
                }
            }
        }

        return [.. result];
    }

    private static Zapret2ProfilePlan BuildProfile(
        RouteScanGroup route,
        StrategyDefinition strategy)
    {
        BlockCheckTarget target = route.Targets[0];
        return new Zapret2ProfilePlan
        {
            Name = $"bc_scan_{strategy.Id}_{target.Id}",
            Filter = new Zapret2ProfileFilter
            {
                IpVersion = target.IpVersion,
                Transport = target.Transport,
                Port = target.Port,
                Layer7Protocol = target.Layer7Protocol,
                Domains = route.Targets.All(routeTarget => routeTarget.HostListPaths.Count == 0)
                    ? route.Targets
                        .Select(routeTarget => BlockCheckTarget.NormalizeHost(routeTarget.Host))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                    : [],
                HostListPaths = route.Targets
                    .SelectMany(routeTarget => routeTarget.HostListPaths)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            },
            Primary = strategy,
            TargetIds = route.Targets
                .Select(routeTarget => routeTarget.Id)
                .ToHashSet(StringComparer.Ordinal),
        };
    }

    private static void ValidateOptions(
        BlockCheckScanOptions options,
        ICollection<BlockCheckIssue> issues)
    {
        if (options.AttemptsPerTarget < 1)
        {
            issues.Add(Error("SCAN_ATTEMPTS_INVALID", "Attempts per target must be at least one."));
        }

        if (!Enum.IsDefined(options.MaximumTier))
        {
            issues.Add(Error("SCAN_TIER_INVALID", "Maximum scan tier is invalid."));
        }

        if (options.SuccessfulStrategiesPerRoute < 1)
        {
            issues.Add(Error(
                "SCAN_SUCCESS_COUNT_INVALID",
                "Successful strategies per route must be at least one."));
        }

        if (!double.IsFinite(options.SuccessfulStrategyRate) ||
            options.SuccessfulStrategyRate <= 0d ||
            options.SuccessfulStrategyRate > 1d)
        {
            issues.Add(Error(
                "SCAN_SUCCESS_RATE_INVALID",
                "Successful strategy rate must be greater than zero and at most one."));
        }
    }

    private static BlockCheckIssue Error(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Error, code, message, subjectId);

    private static BlockCheckIssue Warning(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Warning, code, message, subjectId);

    private static BlockCheckIssue CanceledIssue() =>
        Warning(
            "SCAN_CANCELED",
            "The scan was stopped by the user. The report contains only checks completed before cancellation.");

    private static ProbeAttempt FailedAttempt(string failureCode, string diagnostic) => new()
    {
        Success = false,
        TimeToFirstByteMs = -1,
        ExitCode = -1,
        FailureCode = failureCode,
        Diagnostic = diagnostic,
    };

    private static IEnumerable<ProbeResult> CreateFailedRouteResults(
        RouteScanGroup route,
        string strategyId,
        string failureCode,
        string diagnostic) =>
        route.Targets.Select(target => new ProbeResult
        {
            StrategyId = strategyId,
            TargetId = target.Id,
            Attempts = [FailedAttempt(failureCode, diagnostic)],
        });

    private sealed record RouteScanGroup(
        string Key,
        BlockCheckTarget[] Targets);

    private sealed record BaselineProbeOutcome(
        IReadOnlyList<ProbeResult> Results,
        bool WasCanceled);
}
