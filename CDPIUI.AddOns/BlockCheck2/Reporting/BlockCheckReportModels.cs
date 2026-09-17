using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.AddOns.BlockCheck2.Reporting;

public sealed class BlockCheckReport
{
    public int SchemaVersion { get; init; } = 2;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string CatalogVersion { get; init; } = string.Empty;
    public BlockCheckRunPreset RunPreset { get; init; }
    public bool Success { get; init; }
    public bool WasCanceled { get; init; }
    public bool IsBestEffort { get; init; }
    public IReadOnlyList<BlockCheckReportTarget> Targets { get; init; } = [];
    public IReadOnlyList<BlockCheckReportProbe> Probes { get; init; } = [];
    public IReadOnlyList<BlockCheckReportProfile> Profiles { get; init; } = [];
    public IReadOnlyList<BlockCheckReportValidationAttempt> ValidationAttempts { get; init; } = [];
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
    public string PresetArguments { get; init; } = string.Empty;
}

public sealed record BlockCheckReportTarget(
    string Id,
    string Host,
    string Path,
    BlockCheckProtocol Protocol,
    BlockCheckIpVersion IpVersion,
    BlockCheckTransport Transport,
    int Port,
    string RuntimeRoute);

public sealed record BlockCheckReportProbe(
    string Kind,
    int CandidateNumber,
    string StrategyId,
    string TargetId,
    int AttemptCount,
    int SuccessCount,
    double SuccessRate,
    double? MedianTimeToFirstByteMs,
    double? P95TimeToFirstByteMs,
    IReadOnlyList<int> HttpStatusCodes,
    IReadOnlyList<string> FailureCodes);

public sealed record BlockCheckReportProfile(
    string Name,
    BlockCheckIpVersion IpVersion,
    BlockCheckTransport Transport,
    int Port,
    string Layer7Protocol,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> HostListPaths,
    string PrimaryStrategyId,
    IReadOnlyList<string> FallbackStrategyIds,
    IReadOnlyList<string> TargetIds,
    bool IsBestEffort = false);

public sealed record BlockCheckReportValidationAttempt(
    int CandidateNumber,
    bool Success,
    bool InfrastructureFailure,
    IReadOnlyList<string> FailedTargetIds,
    IReadOnlyList<string> ExcludedStrategyIds);
