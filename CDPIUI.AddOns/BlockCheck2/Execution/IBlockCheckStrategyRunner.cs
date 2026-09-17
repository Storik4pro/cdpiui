namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Starts and stops one isolated winws2 process. StartAsync must not return until
/// the process is ready for a probe. The CDPIUI adapter owns elevation and logging.
/// </summary>
public interface IBlockCheckStrategyRunner
{
    Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task StartAsync(string arguments, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task CompleteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
