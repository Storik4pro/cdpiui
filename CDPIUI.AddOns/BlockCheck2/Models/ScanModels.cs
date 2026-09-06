namespace CDPIUI.AddOns.BlockCheck2.Models;

public enum BlockCheckScanPhase
{
    BaselineProbing,
    StartingStrategy,
    Probing,
    StrategyCompleted,
    ValidatingPreset,
    RepairingPreset,
}

public sealed class BlockCheckScanOptions
{
    public int AttemptsPerTarget { get; init; } = 3;
    public bool RunBaseline { get; init; } = true;
    public BlockCheckScanTier MaximumTier { get; init; } = BlockCheckScanTier.Balanced;
    public bool SkipFullyBaselineAccessibleRoutes { get; init; } = true;
    public bool EnableRouteEarlyStop { get; init; } = true;
    public int SuccessfulStrategiesPerRoute { get; init; } = 2;
    public double SuccessfulStrategyRate { get; init; } = 0.8d;
    public IReadOnlySet<string>? StrategyIds { get; init; }
    public Zapret2WriterOptions WriterOptions { get; init; } = new();
}

public sealed record BlockCheckScanProgress(
    BlockCheckScanPhase Phase,
    int CompletedStrategyTargets,
    int TotalStrategyTargets,
    string TargetId,
    string StrategyId,
    int AttemptNumber = 0,
    int SuccessfulChecks = 0,
    int FailedChecks = 0,
    int SuccessfulStrategies = 0)
{
    public int CompletedChecks => SuccessfulChecks + FailedChecks;
}

public sealed class BlockCheckScanResult
{
    public IReadOnlyList<ProbeResult> BaselineResults { get; init; } = [];
    public IReadOnlyList<ProbeResult> ProbeResults { get; init; } = [];
    public IReadOnlySet<string> IgnoredTargetIds { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
    public bool WasCanceled { get; init; }

    public bool Success => Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}

public sealed class BlockCheckRunOptions
{
    public BlockCheckScanOptions Scan { get; init; } = new();
    public BlockCheckSynthesisOptions Synthesis { get; init; } = new();
    public BlockCheckPresetValidationOptions Validation { get; init; } = new();
}

public sealed class BlockCheckRunResult
{
    public IReadOnlyList<BlockCheckIssue> PreflightIssues { get; init; } = [];
    public BlockCheckScanResult Scan { get; init; } = new();
    public BlockCheckSynthesisResult? Synthesis { get; init; }
    public BlockCheckPresetValidationResult? Validation { get; init; }
    public bool WasCanceled { get; init; }

    public bool Success =>
        !WasCanceled &&
        PreflightIssues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error) &&
        Scan.Success &&
        Synthesis?.Success == true &&
        (Validation?.Success ?? true);
}
