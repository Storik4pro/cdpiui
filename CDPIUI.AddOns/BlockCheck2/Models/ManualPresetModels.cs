namespace CDPIUI.AddOns.BlockCheck2.Models;

public enum BlockCheckManualScopeKind
{
    Site,
    SiteList,
}

public enum BlockCheckEvidenceStatus
{
    Successful,
    Partial,
    Failed,
    Untested,
}

public sealed class BlockCheckStrategyEvidence
{
    public StrategyDefinition Strategy { get; init; } = new();
    public BlockCheckTarget Target { get; init; } = new();
    public ProbeSummary Summary { get; init; } = new();
    public ProbeSummary BaselineSummary { get; init; } = new();
    public string StrategyArguments { get; init; } = string.Empty;
    public BlockCheckEvidenceStatus Status { get; init; }
    public IReadOnlyList<int> HttpStatusCodes { get; init; } = [];
    public IReadOnlyList<string> FailureCodes { get; init; } = [];
    public int Improvement => Summary.SuccessCount - BaselineSummary.SuccessCount;
}

public sealed record BlockCheckManualAssignment(
    string StrategyId,
    string AnchorTargetId,
    BlockCheckManualScopeKind ScopeKind,
    string? SiteListPath = null);

public sealed class BlockCheckManualPresetOptions
{
    public int MinimumAttempts { get; init; } = 1;
    public double MinimumSuccessRate { get; init; } = 0.8d;
    public Zapret2WriterOptions WriterOptions { get; init; } = new();
}

public sealed class BlockCheckManualPresetResult
{
    public IReadOnlyList<Zapret2ProfilePlan> Profiles { get; init; } = [];
    public Zapret2WriteResult Configuration { get; init; } = new();
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
    public bool Success =>
        Profiles.Count > 0 &&
        Configuration.Success &&
        Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}

public sealed class BlockCheckManualStrategyTestOptions
{
    public int Attempts { get; init; } = 3;
    public Zapret2WriterOptions WriterOptions { get; init; } = new();
}

public sealed class BlockCheckManualStrategyTestResult
{
    public ProbeResult? ProbeResult { get; init; }
    public string CommandLine { get; init; } = string.Empty;
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
    public bool Success =>
        ProbeResult?.Attempts.Count > 0 &&
        ProbeResult.Attempts.All(attempt => attempt.Success) &&
        Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}
