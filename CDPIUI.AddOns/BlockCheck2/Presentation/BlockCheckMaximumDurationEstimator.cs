using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Presentation;

public sealed class BlockCheckMaximumDurationEstimate
{
    public TimeSpan Duration { get; init; }
    public int TargetCount { get; init; }
    public int RouteCount { get; init; }
    public int EstimatedStrategyJobs { get; init; }
    public long EstimatedRequests { get; init; }
    public double AssumedFailureRate { get; init; }
}

/// <summary>
/// Builds a conservative pre-run estimate. Failed probes are assumed to consume
/// the complete request timeout, and only one strategy out of five is expected
/// to satisfy a route when early stopping is enabled.
/// </summary>
public sealed class BlockCheckMaximumDurationEstimator
{
    public BlockCheckMaximumDurationEstimate Estimate(
        StrategyCatalog catalog,
        IReadOnlyList<BlockCheckTarget> targets,
        BlockCheckRunOptions options,
        TimeSpan requestTimeout,
        TimeSpan startupGracePeriod,
        double assumedFailureRate = 0.8d)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(options);
        if (requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }
        if (startupGracePeriod < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startupGracePeriod));
        }
        if (!double.IsFinite(assumedFailureRate) || assumedFailureRate is < 0d or >= 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(assumedFailureRate));
        }

        BlockCheckTargetGroup[] routes = BlockCheckTargetGroupBuilder.Build(targets).ToArray();
        StrategyDefinition[] strategies = catalog.Strategies
            .Where(strategy => options.Scan.StrategyIds != null
                ? options.Scan.StrategyIds.Contains(strategy.Id)
                : strategy.ScanTier <= options.Scan.MaximumTier)
            .ToArray();

        int strategyJobs = 0;
        long scanRequests = 0;
        double assumedSuccessRate = 1d - assumedFailureRate;
        int jobsNeededForEarlyStop = (int)Math.Ceiling(
            options.Scan.SuccessfulStrategiesPerRoute / assumedSuccessRate);
        foreach (BlockCheckTargetGroup route in routes)
        {
            int applicableStrategies = strategies.Count(strategy =>
                route.Targets.All(strategy.AppliesTo));
            int routeJobs = options.Scan.EnableRouteEarlyStop
                ? Math.Min(applicableStrategies, jobsNeededForEarlyStop)
                : applicableStrategies;
            strategyJobs += routeJobs;
            scanRequests += (long)routeJobs *
                            route.Targets.Length *
                            options.Scan.AttemptsPerTarget;
        }

        long baselineRequests = options.Scan.RunBaseline
            ? (long)targets.Count * options.Scan.AttemptsPerTarget
            : 0L;
        int validationRounds = 1 + Math.Max(0, options.Validation.MaximumRepairIterations);
        long validationRequests = (long)validationRounds *
                                  targets.Count *
                                  options.Validation.AttemptsPerTarget;
        long totalRequests = baselineRequests + scanRequests + validationRequests;
        int processStarts = strategyJobs + validationRounds;
        TimeSpan duration = TimeSpan.FromTicks(checked(
            requestTimeout.Ticks * totalRequests +
            startupGracePeriod.Ticks * processStarts));

        return new BlockCheckMaximumDurationEstimate
        {
            Duration = duration,
            TargetCount = targets.Count,
            RouteCount = routes.Length,
            EstimatedStrategyJobs = strategyJobs,
            EstimatedRequests = totalRequests,
            AssumedFailureRate = assumedFailureRate,
        };
    }
}
