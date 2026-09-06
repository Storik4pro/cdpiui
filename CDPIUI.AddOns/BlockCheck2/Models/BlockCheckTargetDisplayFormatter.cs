namespace CDPIUI.AddOns.BlockCheck2.Models;

public static class BlockCheckTargetDisplayFormatter
{
    public static string Format(BlockCheckTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return $"{FormatUrl(target)} [{FormatConnectionDetails(target)}]";
    }

    public static string FormatUrl(BlockCheckTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return FormatUrl(target.Host, target.Path, target.Protocol, target.Port);
    }

    public static string FormatUrl(
        string host,
        string path,
        BlockCheckProtocol protocol,
        int port)
    {
        string scheme = protocol == BlockCheckProtocol.Http ? "http" : "https";
        int defaultPort = scheme == "http" ? 80 : 443;
        string portPart = port == defaultPort ? string.Empty : $":{port}";
        string normalizedPath = string.IsNullOrWhiteSpace(path)
            ? "/"
            : path.StartsWith('/') ? path : $"/{path}";
        return $"{scheme}://{host}{portPart}{normalizedPath}";
    }

    public static string FormatConnectionDetails(BlockCheckTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return FormatConnectionDetails(
            target.Protocol,
            target.IpVersion,
            target.Transport,
            target.Port);
    }

    public static string FormatConnectionDetails(
        BlockCheckProtocol protocol,
        BlockCheckIpVersion ipVersion,
        BlockCheckTransport transport,
        int port)
    {
        string protocolName = protocol switch
        {
            BlockCheckProtocol.Http => "HTTP",
            BlockCheckProtocol.Tls12 => "TLS 1.2",
            BlockCheckProtocol.Tls13 => "TLS 1.3",
            BlockCheckProtocol.TlsAuto => "HTTPS (automatic TLS)",
            BlockCheckProtocol.Quic => "QUIC",
            _ => protocol.ToString(),
        };
        string transportName = transport == BlockCheckTransport.Tcp ? "TCP" : "UDP";
        return $"{protocolName} · {ipVersion} · {transportName}:{port}";
    }

    public static string Format(
        string host,
        string path,
        BlockCheckProtocol protocol,
        BlockCheckIpVersion ipVersion,
        BlockCheckTransport transport,
        int port)
    {
        string url = FormatUrl(host, path, protocol, port);
        string details = FormatConnectionDetails(protocol, ipVersion, transport, port);
        return $"{url} [{details}]";
    }
}
