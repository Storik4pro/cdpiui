using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;

namespace CDPIUI.AddOns.BlockCheck2;

/// <summary>
/// UI-independent entry point for the AddOn. UI code only supplies targets,
/// progress, cancellation, and production process/probe adapters.
/// </summary>
public sealed class BlockCheckAddOnService
{
    private readonly BlockCheckScanService _scanService;
    private readonly BlockCheckSynthesisService _synthesisService;
    private readonly BlockCheckPresetValidationService _validationService;

    public BlockCheckAddOnService(
        BlockCheckScanService? scanService = null,
        BlockCheckSynthesisService? synthesisService = null,
        BlockCheckPresetValidationService? validationService = null)
    {
        _scanService = scanService ?? new BlockCheckScanService();
        _synthesisService = synthesisService ?? new BlockCheckSynthesisService();
        _validationService = validationService ?? new BlockCheckPresetValidationService(_synthesisService);
    }

    public async Task<BlockCheckRunResult> RunAsync(
        StrategyCatalog catalog,
        IEnumerable<BlockCheckTarget> targets,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckRunOptions? options = null,
        IProgress<BlockCheckScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(strategyRunner);
        ArgumentNullException.ThrowIfNull(probeRunner);

        options ??= new BlockCheckRunOptions();
        BlockCheckTarget[] targetArray = targets.ToArray();
        List<BlockCheckIssue> preflightIssues = [.. ValidateRunOptions(options)];
        if (!preflightIssues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error) &&
            probeRunner is IBlockCheckProbePreflight preflight)
        {
            try
            {
                preflightIssues.AddRange(
                    await preflight.CheckAsync(targetArray, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                preflightIssues.Add(CanceledIssue());
                return new BlockCheckRunResult
                {
                    PreflightIssues = preflightIssues,
                    Scan = new BlockCheckScanResult { WasCanceled = true },
                    WasCanceled = true,
                };
            }
        }

        if (preflightIssues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return new BlockCheckRunResult
            {
                PreflightIssues = preflightIssues,
                Scan = new BlockCheckScanResult { Issues = preflightIssues },
            };
        }

        BlockCheckScanResult scan;
        try
        {
            scan = await _scanService.ScanAsync(
                    catalog,
                    targetArray,
                    strategyRunner,
                    probeRunner,
                    options.Scan,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            preflightIssues.Add(CanceledIssue());
            return new BlockCheckRunResult
            {
                PreflightIssues = preflightIssues,
                Scan = new BlockCheckScanResult { WasCanceled = true },
                WasCanceled = true,
            };
        }

        if (!scan.Success)
        {
            return new BlockCheckRunResult
            {
                PreflightIssues = preflightIssues,
                Scan = scan,
                WasCanceled = scan.WasCanceled,
            };
        }

        BlockCheckTarget[] activeTargets = targetArray
            .Where(target => !scan.IgnoredTargetIds.Contains(target.Id))
            .ToArray();
        ProbeResult[] activeBaselineResults = scan.BaselineResults
            .Where(result => !scan.IgnoredTargetIds.Contains(result.TargetId))
            .ToArray();

        BlockCheckSynthesisResult synthesis = _synthesisService.Synthesize(
            catalog,
            activeTargets,
            scan.ProbeResults,
            options.Synthesis,
            activeBaselineResults);
        if (!synthesis.Success)
        {
            return new BlockCheckRunResult
            {
                PreflightIssues = preflightIssues,
                Scan = scan,
                Synthesis = synthesis,
                WasCanceled = scan.WasCanceled,
            };
        }

        if (scan.WasCanceled)
        {
            return new BlockCheckRunResult
            {
                PreflightIssues = preflightIssues,
                Scan = scan,
                Synthesis = synthesis,
                WasCanceled = true,
            };
        }

        BlockCheckPresetValidationResult validation;
        try
        {
            validation = await _validationService.ValidateAndRepairAsync(
                    catalog,
                    activeTargets,
                    scan.ProbeResults,
                    activeBaselineResults,
                    synthesis,
                    options.Synthesis,
                    strategyRunner,
                    probeRunner,
                    options.Validation,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            preflightIssues.Add(CanceledIssue());
            return new BlockCheckRunResult
            {
                PreflightIssues = preflightIssues,
                Scan = scan,
                Synthesis = synthesis,
                WasCanceled = true,
            };
        }

        return new BlockCheckRunResult
        {
            PreflightIssues = preflightIssues,
            Scan = scan,
            Synthesis = validation.FinalSynthesis,
            Validation = validation,
            WasCanceled = false,
        };
    }

    public Task<BlockCheckRunResult> RunWithCdpiuiAdaptersAsync(
        StrategyCatalog catalog,
        IEnumerable<BlockCheckTarget> targets,
        BlockCheckRunOptions? options = null,
        IProgress<BlockCheckScanProgress>? progress = null,
        CdpiuiZapret2StrategyRunnerOptions? strategyRunnerOptions = null,
        CurlBlockCheckProbeRunnerOptions? probeRunnerOptions = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            catalog,
            targets,
            new CdpiuiZapret2StrategyRunner(strategyRunnerOptions),
            new CurlBlockCheckProbeRunner(probeRunnerOptions),
            options,
            progress,
            cancellationToken);

    private static IReadOnlyList<BlockCheckIssue> ValidateRunOptions(BlockCheckRunOptions options)
    {
        List<BlockCheckIssue> issues = [];
        if (options.Synthesis.MinimumAttempts < 1)
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Error,
                "RUN_SYNTHESIS_ATTEMPTS_INVALID",
                "Synthesis minimum attempt count must be at least one."));
        }

        if (!double.IsFinite(options.Synthesis.MinimumSuccessRate) ||
            options.Synthesis.MinimumSuccessRate <= 0d ||
            options.Synthesis.MinimumSuccessRate > 1d)
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Error,
                "RUN_SYNTHESIS_RATE_INVALID",
                "Synthesis success rate must be greater than zero and at most one."));
        }

        if (options.Scan.AttemptsPerTarget < options.Synthesis.MinimumAttempts)
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Error,
                "RUN_ATTEMPTS_MISMATCH",
                "Scan attempts per target must be at least the synthesis minimum attempt count."));
        }

        if (options.Scan.SuccessfulStrategyRate < options.Synthesis.MinimumSuccessRate)
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Error,
                "RUN_SUCCESS_RATE_MISMATCH",
                "Scan success rate must be at least the synthesis minimum success rate."));
        }

        if (options.Validation.Enabled)
        {
            if (options.Validation.AttemptsPerTarget < options.Synthesis.MinimumAttempts)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Error,
                    "RUN_VALIDATION_ATTEMPTS_MISMATCH",
                    "Preset validation attempts must be at least the synthesis minimum attempt count."));
            }

            if (!double.IsFinite(options.Validation.MinimumSuccessRate) ||
                options.Validation.MinimumSuccessRate <= 0d ||
                options.Validation.MinimumSuccessRate > 1d)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Error,
                    "RUN_VALIDATION_RATE_INVALID",
                    "Preset validation success rate must be greater than zero and at most one."));
            }
            else if (options.Validation.MinimumSuccessRate < options.Synthesis.MinimumSuccessRate)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Error,
                    "RUN_VALIDATION_RATE_MISMATCH",
                    "Preset validation success rate must be at least the synthesis success rate."));
            }

            if (options.Validation.MaximumRepairIterations < 0)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Error,
                    "RUN_REPAIR_ITERATIONS_INVALID",
                    "Maximum preset repair iterations cannot be negative."));
            }
        }

        return issues;
    }

    private static BlockCheckIssue CanceledIssue() =>
        new(
            BlockCheckIssueSeverity.Warning,
            "RUN_CANCELED",
            "The run was stopped by the user. The report contains only checks completed before cancellation.");
}
