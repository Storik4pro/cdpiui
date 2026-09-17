using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Reporting;

namespace CDPIUI.AddOns.BlockCheck2.Presentation;

public sealed class BlockCheckResultSession
{
    public StrategyCatalog Catalog { get; init; } = new();
    public IReadOnlyList<BlockCheckTarget> Targets { get; init; } = [];
    public BlockCheckRunResult RunResult { get; init; } = new();
    public BlockCheckReport Report { get; init; } = new();
    public BlockCheckRunOptions RunOptions { get; init; } = new();
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan StartupGracePeriod { get; init; } = TimeSpan.FromSeconds(0.75d);
}
