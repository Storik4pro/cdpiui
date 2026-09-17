using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

public sealed class BlockCheckPresetValidationService
{
    private readonly BlockCheckSynthesisService _synthesisService;

    public BlockCheckPresetValidationService(BlockCheckSynthesisService? synthesisService = null)
    {
        _synthesisService = synthesisService ?? new BlockCheckSynthesisService();
    }

    public async Task<BlockCheckPresetValidationResult> ValidateAndRepairAsync(
        StrategyCatalog catalog,
        IEnumerable<BlockCheckTarget> targets,
        IEnumerable<ProbeResult> scanProbeResults,
        IEnumerable<ProbeResult> baselineProbeResults,
        BlockCheckSynthesisResult initialSynthesis,
        BlockCheckSynthesisOptions synthesisOptions,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckPresetValidationOptions? options = null,
        IProgress<BlockCheckScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(scanProbeResults);
        ArgumentNullException.ThrowIfNull(baselineProbeResults);
        ArgumentNullException.ThrowIfNull(initialSynthesis);
        ArgumentNullException.ThrowIfNull(synthesisOptions);
        ArgumentNullException.ThrowIfNull(strategyRunner);
        ArgumentNullException.ThrowIfNull(probeRunner);

        options ??= new BlockCheckPresetValidationOptions();
        BlockCheckTarget[] targetArray = targets.ToArray();
        ProbeResult[] originalProbeResults = scanProbeResults.ToArray();
        ProbeResult[] baselineResults = baselineProbeResults.ToArray();
        List<BlockCheckIssue> issues = [];
        if (!initialSynthesis.Success)
        {
            issues.Add(Error(
                "PRESET_SYNTHESIS_INVALID",
                "Preset validation requires a successful initial synthesis result."));
        }

        if (issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return Result(false, [], initialSynthesis, issues);
        }

        if (!options.Enabled)
        {
            return Result(false, [], initialSynthesis, issues);
        }

        issues.AddRange(ValidateOptions(options));
        if (issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return Result(false, [], initialSynthesis, issues);
        }

        if (initialSynthesis.Profiles.Count == 0)
        {
            return Result(false, [], initialSynthesis, issues);
        }

        Dictionary<string, BlockCheckTarget> targetsById = targetArray
            .ToDictionary(target => target.Id, StringComparer.Ordinal);
        Dictionary<string, RuntimeRouteKey> routesByTargetId = targetArray
            .ToDictionary(target => target.Id, target => target.GetRuntimeRouteKey(), StringComparer.Ordinal);
        Dictionary<string, string> signaturesByStrategyId = catalog.Strategies
            .ToDictionary(
                strategy => strategy.Id,
                CandidateNormalizer.GetPlanSignature,
                StringComparer.OrdinalIgnoreCase);
        HashSet<StrategyRouteExclusion> exclusions = [];
        List<BlockCheckPresetValidationAttempt> attempts = [];
        BlockCheckSynthesisResult currentSynthesis = initialSynthesis;

        bool prepareAttempted = false;
        try
        {
            prepareAttempted = true;
            await strategyRunner.PrepareAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                await strategyRunner.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Cancellation still has to propagate; cleanup was attempted with a non-cancelable token.
            }
            throw;
        }
        catch (Exception exception)
        {
            issues.Add(Error(
                "PRESET_VALIDATION_PREPARE_FAILED",
                $"Could not prepare the preset validation environment: {exception.Message}"));
            try
            {
                await strategyRunner.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception completeException)
            {
                issues.Add(Error(
                    "PRESET_VALIDATION_COMPLETE_FAILED",
                    $"Could not restore the process environment after failed preparation: {completeException.Message}"));
            }
            return Result(true, attempts, currentSynthesis, issues);
        }

        try
        {
            int repairsApplied = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BlockCheckPresetValidationAttempt attempt = await ValidateCandidateAsync(
                        currentSynthesis.Configuration.CommandLine,
                        targetArray,
                        strategyRunner,
                        probeRunner,
                        options,
                        attempts.Count + 1,
                        GetExcludedStrategyIds(exclusions, signaturesByStrategyId),
                        progress,
                        issues,
                        cancellationToken)
                    .ConfigureAwait(false);
                attempts.Add(attempt);

                if (attempt.InfrastructureFailure)
                {
                    break;
                }

                if (attempt.Success)
                {
                    if (repairsApplied > 0)
                    {
                        issues.Add(new BlockCheckIssue(
                            BlockCheckIssueSeverity.Info,
                            "PRESET_REPAIRED",
                            $"The combined preset passed after {repairsApplied} repair iteration(s)."));
                    }
                    break;
                }

                if (!options.EnableRepair)
                {
                    issues.Add(Error(
                        "PRESET_VALIDATION_FAILED",
                        "The synthesized preset failed combined validation and automatic repair is disabled."));
                    break;
                }

                if (repairsApplied >= options.MaximumRepairIterations)
                {
                    issues.Add(Error(
                        "PRESET_REPAIR_LIMIT_REACHED",
                        "The synthesized preset still fails after the configured repair iterations."));
                    break;
                }

                HashSet<StrategyRouteExclusion> newExclusions = FindExclusions(
                    attempt.FailedTargetIds,
                    currentSynthesis.Profiles,
                    targetsById,
                    routesByTargetId);
                newExclusions.ExceptWith(exclusions);
                if (newExclusions.Count == 0)
                {
                    issues.Add(Error(
                        "PRESET_REPAIR_STALLED",
                        "The failing targets could not be mapped to a replaceable preset strategy."));
                    break;
                }

                exclusions.UnionWith(newExclusions);
                progress?.Report(new BlockCheckScanProgress(
                    BlockCheckScanPhase.RepairingPreset,
                    repairsApplied,
                    options.MaximumRepairIterations,
                    attempt.FailedTargetIds.OrderBy(id => id, StringComparer.Ordinal).First(),
                    string.Join(',', GetExcludedStrategyIds(newExclusions, signaturesByStrategyId)
                        .OrderBy(id => id, StringComparer.Ordinal))));

                ProbeResult[] remainingResults = originalProbeResults
                    .Where(result => !IsExcluded(
                        result,
                        exclusions,
                        routesByTargetId,
                        signaturesByStrategyId))
                    .ToArray();
                BlockCheckSynthesisResult repaired = _synthesisService.Synthesize(
                    catalog,
                    targetArray,
                    remainingResults,
                    synthesisOptions,
                    baselineResults);
                repairsApplied++;
                currentSynthesis = repaired;
                if (!repaired.Success)
                {
                    issues.Add(Error(
                        "PRESET_REPAIR_NO_CANDIDATE",
                        "No remaining compatible strategy can replace the failing preset profile."));
                    break;
                }
            }
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
                        "PRESET_VALIDATION_COMPLETE_FAILED",
                        $"Could not restore the process environment after preset validation: {exception.Message}"));
                }
            }
        }

        return Result(true, attempts, currentSynthesis, issues);
    }

    private static async Task<BlockCheckPresetValidationAttempt> ValidateCandidateAsync(
        string commandLine,
        IReadOnlyList<BlockCheckTarget> targets,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckPresetValidationOptions options,
        int candidateNumber,
        IReadOnlySet<string> excludedStrategyIds,
        IProgress<BlockCheckScanProgress>? progress,
        ICollection<BlockCheckIssue> issues,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<ProbeAttempt>> attemptsByTarget = targets
            .ToDictionary(target => target.Id, _ => new List<ProbeAttempt>(), StringComparer.Ordinal);
        bool infrastructureFailure = false;
        bool startAttempted = false;
        int successfulChecks = 0;
        int failedChecks = 0;
        int completedChecks = 0;
        int totalChecks = targets.Count * options.AttemptsPerTarget;
        try
        {
            startAttempted = true;
            await strategyRunner.StartAsync(commandLine, cancellationToken).ConfigureAwait(false);

            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                BlockCheckTarget target = targets[targetIndex];
                List<ProbeAttempt> targetAttempts = attemptsByTarget[target.Id];
                for (int attemptNumber = 1; attemptNumber <= options.AttemptsPerTarget; attemptNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new BlockCheckScanProgress(
                        BlockCheckScanPhase.ValidatingPreset,
                        completedChecks,
                        totalChecks,
                        target.Id,
                        $"preset-{candidateNumber}",
                        attemptNumber,
                        successfulChecks,
                        failedChecks));
                    try
                    {
                        ProbeAttempt probeAttempt = await probeRunner
                            .ProbeAsync(target, cancellationToken)
                            .ConfigureAwait(false);
                        targetAttempts.Add(probeAttempt);
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
                        infrastructureFailure = true;
                        failedChecks++;
                        issues.Add(Error(
                            "PRESET_VALIDATION_PROBE_FAILED",
                            $"Preset validation probe failed: {exception.Message}",
                            target.Id));
                        completedChecks++;
                        progress?.Report(new BlockCheckScanProgress(
                            BlockCheckScanPhase.ValidatingPreset,
                            completedChecks,
                            totalChecks,
                            target.Id,
                            $"preset-{candidateNumber}",
                            attemptNumber,
                            successfulChecks,
                            failedChecks));
                        break;
                    }

                    completedChecks++;
                    progress?.Report(new BlockCheckScanProgress(
                        BlockCheckScanPhase.ValidatingPreset,
                        completedChecks,
                        totalChecks,
                        target.Id,
                        $"preset-{candidateNumber}",
                        attemptNumber,
                        successfulChecks,
                        failedChecks));
                }

                if (infrastructureFailure)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            infrastructureFailure = true;
            issues.Add(Error(
                "PRESET_VALIDATION_START_FAILED",
                $"Could not start the synthesized preset: {exception.Message}"));
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
                    infrastructureFailure = true;
                    issues.Add(Error(
                        "PRESET_VALIDATION_STOP_FAILED",
                        $"Could not stop the synthesized preset: {exception.Message}"));
                }
            }
        }

        ProbeResult[] probeResults = targets
            .Select(target => new ProbeResult
            {
                StrategyId = string.Empty,
                TargetId = target.Id,
                Attempts = attemptsByTarget[target.Id],
            })
            .ToArray();
        HashSet<string> failedTargetIds = infrastructureFailure
            ? targets.Select(target => target.Id).ToHashSet(StringComparer.Ordinal)
            : probeResults
                .Where(result => !IsSuccessful(result, options))
                .Select(result => result.TargetId)
                .ToHashSet(StringComparer.Ordinal);

        return new BlockCheckPresetValidationAttempt
        {
            CandidateNumber = candidateNumber,
            ProbeResults = probeResults,
            FailedTargetIds = failedTargetIds,
            ExcludedStrategyIds = excludedStrategyIds,
            InfrastructureFailure = infrastructureFailure,
        };
    }

    private static HashSet<StrategyRouteExclusion> FindExclusions(
        IReadOnlySet<string> failedTargetIds,
        IReadOnlyList<Zapret2ProfilePlan> profiles,
        IReadOnlyDictionary<string, BlockCheckTarget> targetsById,
        IReadOnlyDictionary<string, RuntimeRouteKey> routesByTargetId)
    {
        HashSet<StrategyRouteExclusion> exclusions = [];
        foreach (string failedTargetId in failedTargetIds)
        {
            if (!targetsById.TryGetValue(failedTargetId, out BlockCheckTarget? failedTarget) ||
                !routesByTargetId.TryGetValue(failedTargetId, out RuntimeRouteKey failedRoute))
            {
                continue;
            }

            Zapret2ProfilePlan[] implicated = profiles
                .Where(profile => profile.TargetIds.Contains(failedTargetId))
                .ToArray();
            bool directlyAssigned = implicated.Length > 0;
            if (!directlyAssigned)
            {
                implicated = profiles
                    .Where(profile => ProfileCanCapture(profile, failedTarget))
                    .ToArray();
            }

            foreach (Zapret2ProfilePlan profile in implicated)
            {
                if (directlyAssigned)
                {
                    exclusions.Add(new StrategyRouteExclusion(
                        CandidateNormalizer.GetPlanSignature(profile.Primary),
                        failedRoute));
                    continue;
                }

                foreach (string profileTargetId in profile.TargetIds)
                {
                    if (routesByTargetId.TryGetValue(profileTargetId, out RuntimeRouteKey profileRoute))
                    {
                        exclusions.Add(new StrategyRouteExclusion(
                            CandidateNormalizer.GetPlanSignature(profile.Primary),
                            profileRoute));
                    }
                }
            }
        }

        return exclusions;
    }

    private static bool ProfileCanCapture(Zapret2ProfilePlan profile, BlockCheckTarget target)
    {
        if (profile.Filter.IpVersion != target.IpVersion ||
            profile.Filter.Transport != target.Transport ||
            profile.Filter.Port != target.Port ||
            !string.Equals(profile.Filter.Layer7Protocol, target.Layer7Protocol, StringComparison.Ordinal))
        {
            return false;
        }

        string host = BlockCheckTarget.NormalizeHost(target.Host);
        return profile.Filter.Domains.Any(domain =>
            string.Equals(host, domain, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExcluded(
        ProbeResult result,
        IReadOnlySet<StrategyRouteExclusion> exclusions,
        IReadOnlyDictionary<string, RuntimeRouteKey> routesByTargetId,
        IReadOnlyDictionary<string, string> signaturesByStrategyId) =>
        routesByTargetId.TryGetValue(result.TargetId, out RuntimeRouteKey route) &&
        signaturesByStrategyId.TryGetValue(result.StrategyId, out string? signature) &&
        exclusions.Contains(new StrategyRouteExclusion(signature, route));

    private static HashSet<string> GetExcludedStrategyIds(
        IReadOnlySet<StrategyRouteExclusion> exclusions,
        IReadOnlyDictionary<string, string> signaturesByStrategyId)
    {
        HashSet<string> signatures = exclusions
            .Select(exclusion => exclusion.PlanSignature)
            .ToHashSet(StringComparer.Ordinal);
        return signaturesByStrategyId
            .Where(pair => signatures.Contains(pair.Value))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsSuccessful(
        ProbeResult result,
        BlockCheckPresetValidationOptions options)
    {
        ProbeSummary summary = ProbeSummary.FromAttempts(result.Attempts);
        return summary.AttemptCount >= options.AttemptsPerTarget &&
               summary.SuccessRate >= options.MinimumSuccessRate;
    }

    private static IReadOnlyList<BlockCheckIssue> ValidateOptions(
        BlockCheckPresetValidationOptions options)
    {
        List<BlockCheckIssue> issues = [];
        if (options.AttemptsPerTarget < 1)
        {
            issues.Add(Error(
                "PRESET_VALIDATION_ATTEMPTS_INVALID",
                "Preset validation attempts per target must be at least one."));
        }

        if (!double.IsFinite(options.MinimumSuccessRate) ||
            options.MinimumSuccessRate <= 0d ||
            options.MinimumSuccessRate > 1d)
        {
            issues.Add(Error(
                "PRESET_VALIDATION_RATE_INVALID",
                "Preset validation success rate must be greater than zero and at most one."));
        }

        if (options.MaximumRepairIterations < 0)
        {
            issues.Add(Error(
                "PRESET_REPAIR_ITERATIONS_INVALID",
                "Maximum preset repair iterations cannot be negative."));
        }

        return issues;
    }

    private static BlockCheckPresetValidationResult Result(
        bool validationRequired,
        IReadOnlyList<BlockCheckPresetValidationAttempt> attempts,
        BlockCheckSynthesisResult synthesis,
        IReadOnlyList<BlockCheckIssue> issues) => new()
    {
        ValidationRequired = validationRequired,
        Attempts = attempts,
        FinalSynthesis = synthesis,
        Issues = issues,
    };

    private static BlockCheckIssue Error(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Error, code, message, subjectId);

    private readonly record struct StrategyRouteExclusion(
        string PlanSignature,
        RuntimeRouteKey Route);
}
