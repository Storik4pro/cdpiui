namespace CDPIUI.AddOns.BlockCheck2.Presentation;

/// <summary>
/// Estimates the remaining strategy-scan time using an exponential moving
/// average. One unusually slow or fast strategy therefore does not make the
/// displayed value jump as sharply as a simple last-sample estimate would.
/// </summary>
public sealed class BlockCheckRemainingTimeEstimator
{
    private const double Alpha = 0.2d;
    private int lastCompleted;
    private TimeSpan lastElapsed;
    private double? secondsPerJob;

    public void Reset()
    {
        lastCompleted = 0;
        lastElapsed = TimeSpan.Zero;
        secondsPerJob = null;
    }

    public TimeSpan? Update(int completed, int total, TimeSpan elapsed)
    {
        if (completed < 0 || total <= 0 || elapsed < TimeSpan.Zero)
        {
            return null;
        }

        if (completed > lastCompleted)
        {
            int completedDelta = completed - lastCompleted;
            double elapsedDelta = Math.Max(0d, (elapsed - lastElapsed).TotalSeconds);
            double sample = elapsedDelta / completedDelta;
            secondsPerJob = secondsPerJob.HasValue
                ? Alpha * sample + (1d - Alpha) * secondsPerJob.Value
                : sample;
            lastCompleted = completed;
            lastElapsed = elapsed;
        }

        if (!secondsPerJob.HasValue || completed >= total)
        {
            return completed >= total ? TimeSpan.Zero : null;
        }

        double seconds = Math.Max(0d, (total - completed) * secondsPerJob.Value);
        return TimeSpan.FromSeconds(seconds);
    }
}
