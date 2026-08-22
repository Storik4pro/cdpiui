using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

/// <summary>
/// Performs one protocol-aware request. Exact TLS 1.2 and automatic HTTPS negotiation belong
/// here, so the scan can prove both variants even though winws2 routes both as TLS.
/// </summary>
public interface IBlockCheckProbeRunner
{
    Task<ProbeAttempt> ProbeAsync(BlockCheckTarget target, CancellationToken cancellationToken);
}
