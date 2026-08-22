namespace CDPIUI.AddOns.BlockCheck2.Models;

public sealed class BlockCheckPresetValidationOptions
{
    public bool Enabled { get; init; } = true;
    public int AttemptsPerTarget { get; init; } = 3;
    public double MinimumSuccessRate { get; init; } = 0.8d;
    public bool EnableRepair { get; init; } = true;
    public int MaximumRepairIterations { get; init; } = 3;
}

public sealed class BlockCheckPresetValidationAttempt
{
    public int CandidateNumber { get; init; }
    public IReadOnlyList<ProbeResult> ProbeResults { get; init; } = [];
    public IReadOnlySet<string> FailedTargetIds { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> ExcludedStrategyIds { get; init; } = new HashSet<string>();
    public bool InfrastructureFailure { get; init; }
    public bool Success => !InfrastructureFailure && FailedTargetIds.Count == 0;
}

public sealed class BlockCheckPresetValidationResult
{
    public bool ValidationRequired { get; init; }
    public IReadOnlyList<BlockCheckPresetValidationAttempt> Attempts { get; init; } = [];
    public BlockCheckSynthesisResult FinalSynthesis { get; init; } = new();
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];

    public bool Success =>
        FinalSynthesis.Success &&
        Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error) &&
        (!ValidationRequired || Attempts.LastOrDefault()?.Success == true);
}
