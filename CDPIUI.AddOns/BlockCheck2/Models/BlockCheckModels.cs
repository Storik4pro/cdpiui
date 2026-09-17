using System.Globalization;
using System.Net;

namespace CDPIUI.AddOns.BlockCheck2.Models;

public enum BlockCheckProtocol
{
    Http,
    Tls12,
    Tls13,
    TlsAuto,
    Quic,
}

public enum BlockCheckIpVersion
{
    IPv4,
    IPv6,
}

public enum BlockCheckTransport
{
    Tcp,
    Udp,
}

public enum BlockCheckPreference
{
    Speed,
    Balanced,
    Stability,
}

public enum BlockCheckScanTier
{
    Quick,
    Balanced,
    Exhaustive,
}

public enum BlockCheckIssueSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record BlockCheckIssue(
    BlockCheckIssueSeverity Severity,
    string Code,
    string Message,
    string? SubjectId = null);

public sealed class BlockCheckTarget
{
    public string Id { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public string Path { get; init; } = "/";
    public BlockCheckProtocol Protocol { get; init; }
    public BlockCheckIpVersion IpVersion { get; init; }
    public int? CustomPort { get; init; }
    public IReadOnlyList<string> HostListPaths { get; init; } = [];

    public int Port => CustomPort ?? (Protocol == BlockCheckProtocol.Http ? 80 : 443);

    public BlockCheckTransport Transport =>
        Protocol == BlockCheckProtocol.Quic ? BlockCheckTransport.Udp : BlockCheckTransport.Tcp;

    public string Layer7Protocol => Protocol switch
    {
        BlockCheckProtocol.Http => "http",
        BlockCheckProtocol.Tls12 or BlockCheckProtocol.Tls13 or BlockCheckProtocol.TlsAuto => "tls",
        BlockCheckProtocol.Quic => "quic",
        _ => throw new ArgumentOutOfRangeException(nameof(Protocol)),
    };

    public RuntimeRouteKey GetRuntimeRouteKey() => new(
        NormalizeHost(Host),
        IpVersion,
        Transport,
        Port,
        Layer7Protocol);

    public static string NormalizeHost(string host)
    {
        string value = (host ?? string.Empty).Trim().TrimEnd('.');
        if (value.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return new IdnMapping().GetAscii(value).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return value.ToLowerInvariant();
        }
    }

    public static bool IsValidHost(string host)
    {
        string normalized = NormalizeHost(host);
        return normalized.Length > 0 && Uri.CheckHostName(normalized) == UriHostNameType.Dns;
    }
}

public readonly record struct RuntimeRouteKey(
    string Host,
    BlockCheckIpVersion IpVersion,
    BlockCheckTransport Transport,
    int Port,
    string Layer7Protocol)
{
    public override string ToString() =>
        $"{Host}|{IpVersion}|{Transport}|{Port}|{Layer7Protocol}";
}

public sealed class StrategyCatalog
{
    public int SchemaVersion { get; init; }
    public string CatalogVersion { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public List<StrategyDefinition> Strategies { get; init; } = [];
}

public sealed class StrategyDefinition
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Family { get; init; } = string.Empty;
    public List<BlockCheckProtocol> Protocols { get; init; } = [];
    public List<BlockCheckIpVersion> IpVersions { get; init; } =
        [BlockCheckIpVersion.IPv4, BlockCheckIpVersion.IPv6];
    public double BaseCost { get; init; } = 1d;
    public BlockCheckScanTier ScanTier { get; init; } = BlockCheckScanTier.Quick;
    public bool SupportsCircular { get; init; } = true;
    public bool RequiresPreHost { get; init; }
    public bool RequiresInboundTraffic { get; init; }
    public List<BlobDefinition> Blobs { get; init; } = [];
    public List<LuaActionDefinition> Actions { get; init; } = [];

    public bool AppliesTo(BlockCheckTarget target) =>
        (Protocols.Contains(target.Protocol) ||
         (target.Protocol == BlockCheckProtocol.TlsAuto &&
          Protocols.Any(protocol => protocol is BlockCheckProtocol.Tls12 or BlockCheckProtocol.Tls13))) &&
        IpVersions.Contains(target.IpVersion);
}

public sealed class BlobDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

public sealed class LuaActionDefinition
{
    public string Function { get; init; } = string.Empty;
    public List<LuaArgumentDefinition> Arguments { get; init; } = [];
    public List<string> Payloads { get; init; } = ["all"];
    public string InRange { get; init; } = "x";
    public string OutRange { get; init; } = "a";
}

public sealed class LuaArgumentDefinition
{
    public string Name { get; init; } = string.Empty;
    public string? Value { get; init; }
}

public sealed class ProbeAttempt
{
    public bool Success { get; init; }
    public double TimeToFirstByteMs { get; init; }
    public int ExitCode { get; init; }
    public int HttpStatusCode { get; init; }
    public string FailureCode { get; init; } = string.Empty;
    public string Diagnostic { get; init; } = string.Empty;
}

public sealed class ProbeResult
{
    public string StrategyId { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public List<ProbeAttempt> Attempts { get; init; } = [];
}

public sealed class ProbeSummary
{
    public int AttemptCount { get; init; }
    public int SuccessCount { get; init; }
    public double SuccessRate => AttemptCount == 0 ? 0d : (double)SuccessCount / AttemptCount;
    public double MedianTimeToFirstByteMs { get; init; }
    public double P95TimeToFirstByteMs { get; init; }

    public static ProbeSummary FromAttempts(IEnumerable<ProbeAttempt> attempts)
    {
        ProbeAttempt[] all = attempts.ToArray();
        double[] successfulTimings = all
            .Where(attempt => attempt.Success && attempt.TimeToFirstByteMs >= 0)
            .Select(attempt => attempt.TimeToFirstByteMs)
            .OrderBy(value => value)
            .ToArray();

        return new ProbeSummary
        {
            AttemptCount = all.Length,
            SuccessCount = all.Count(attempt => attempt.Success),
            MedianTimeToFirstByteMs = Percentile(successfulTimings, 0.5d),
            P95TimeToFirstByteMs = Percentile(successfulTimings, 0.95d),
        };
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return double.PositiveInfinity;
        }

        int index = Math.Clamp((int)Math.Ceiling(values.Count * percentile) - 1, 0, values.Count - 1);
        return values[index];
    }
}
