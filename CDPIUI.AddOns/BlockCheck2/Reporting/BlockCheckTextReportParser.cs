using System.Globalization;
using System.Text.RegularExpressions;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.AddOns.BlockCheck2.Reporting;

internal static partial class BlockCheckTextReportParser
{
    public static BlockCheckReport Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        if (lines.Length == 0 ||
            !string.Equals(lines[0].Trim(), "CDPIUI BlockCheck2 report", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The file is not a BlockCheck2 text report.");
        }

        DateTimeOffset created = ParseDate(Header(lines, "Created (UTC)"));
        string catalog = Header(lines, "Catalog");
        if (!Enum.TryParse(Header(lines, "Mode"), ignoreCase: true, out BlockCheckRunPreset preset))
        {
            throw new InvalidDataException("The text report contains an unknown selection mode.");
        }
        bool success = ParseBool(Header(lines, "Success"), "Success");
        string? canceledHeader = OptionalHeader(lines, "Canceled");
        bool wasCanceled = canceledHeader == null
            ? false
            : ParseBool(canceledHeader, "Canceled");
        bool bestEffort = ParseBool(Header(lines, "Best effort"), "Best effort");

        List<BlockCheckReportTarget> targets = ParseTargets(lines);
        Dictionary<string, BlockCheckReportTarget> targetByDisplay = targets.ToDictionary(
            target => DisplayKey(TargetUrl(target), TargetConnection(target)),
            StringComparer.OrdinalIgnoreCase);
        List<TemporaryProfile> temporaryProfiles = ParseProfiles(lines);
        List<BlockCheckReportProfile> profiles = temporaryProfiles
            .Select(profile => profile.ToReportProfile(targets))
            .ToList();
        List<BlockCheckReportProbe> probes = ParseProbes(lines, targetByDisplay);
        List<BlockCheckIssue> issues = ParseIssues(lines);
        if (temporaryProfiles.Any(profile => profile.HostListPaths.Count > 0))
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Warning,
                "REPORT_TEXT_SCOPE_INFERRED",
                "This legacy text report does not store exact target-to-list mappings. " +
                "They will be recovered from referenced list files where available; " +
                "save a JSON report for lossless import."));
        }

        return new BlockCheckReport
        {
            SchemaVersion = 2,
            CreatedAtUtc = created,
            CatalogVersion = catalog,
            RunPreset = preset,
            Success = success,
            WasCanceled = wasCanceled,
            IsBestEffort = bestEffort,
            Targets = targets,
            Probes = probes,
            Profiles = profiles,
            Issues = issues,
            PresetArguments = ParsePresetArguments(lines),
        };
    }

    private static List<BlockCheckReportTarget> ParseTargets(IReadOnlyList<string> lines)
    {
        int start = FindSection(lines, "Targets:");
        if (start < 0)
        {
            return [];
        }

        List<BlockCheckReportTarget> targets = [];
        for (int index = start + 1; index + 1 < lines.Count; index++)
        {
            if (!lines[index].StartsWith("- http", StringComparison.OrdinalIgnoreCase))
            {
                if (lines[index].Length == 0)
                {
                    break;
                }
                continue;
            }
            if (!lines[index + 1].StartsWith("  Connection: ", StringComparison.Ordinal))
            {
                throw new InvalidDataException("A target in the text report has no connection details.");
            }

            string url = lines[index][2..].Trim();
            string connection = lines[index + 1][14..].Trim();
            ParsedTarget parsed = ParseTarget(url, connection);
            targets.Add(new BlockCheckReportTarget(
                $"imported-target-{targets.Count + 1:D5}",
                parsed.Host,
                parsed.Path,
                parsed.Protocol,
                parsed.IpVersion,
                parsed.Transport,
                parsed.Port,
                $"{parsed.Host}|{parsed.IpVersion}|{parsed.Transport}|{parsed.Port}|{parsed.Layer7}"));
            index++;
        }
        return targets;
    }

    private static List<TemporaryProfile> ParseProfiles(IReadOnlyList<string> lines)
    {
        int probeSection = FindSection(lines, "Probe results:");
        int limit = probeSection < 0 ? lines.Count : probeSection;
        List<TemporaryProfile> profiles = [];
        for (int index = 0; index < limit; index++)
        {
            Match match = ProfileHeaderRegex().Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            TemporaryProfile profile = new(
                match.Groups["name"].Value,
                match.Groups["best"].Success,
                ParseIpVersion(match.Groups["ip"].Value),
                ParseTransport(match.Groups["transport"].Value),
                int.Parse(match.Groups["port"].Value, CultureInfo.InvariantCulture),
                match.Groups["l7"].Value);
            for (index++; index < limit && lines[index].Length > 0; index++)
            {
                string line = lines[index];
                if (line.StartsWith("Domains: ", StringComparison.Ordinal))
                {
                    profile.Domains.AddRange(SplitValues(line[9..]));
                }
                else if (line.StartsWith("Site lists: ", StringComparison.Ordinal))
                {
                    profile.HostListPaths.AddRange(SplitValues(line[12..]));
                }
                else if (line.StartsWith("Primary: ", StringComparison.Ordinal))
                {
                    profile.Primary = line[9..].Trim();
                }
                else if (line.StartsWith("Fallbacks: ", StringComparison.Ordinal))
                {
                    profile.Fallbacks.AddRange(SplitValues(line[11..]));
                }
            }
            if (string.IsNullOrWhiteSpace(profile.Primary))
            {
                throw new InvalidDataException("A profile in the text report has no primary strategy.");
            }
            profiles.Add(profile);
        }
        return profiles;
    }

    private static List<BlockCheckReportProbe> ParseProbes(
        IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, BlockCheckReportTarget> targets)
    {
        int start = FindSection(lines, "Probe results:");
        if (start < 0)
        {
            return [];
        }
        int limit = SectionLimit(lines, start, "Issues:", "Preset arguments:");
        List<BlockCheckReportProbe> probes = [];
        for (int index = start + 1; index + 1 < limit; index++)
        {
            Match match = ProbeRegex().Match(lines[index]);
            if (!match.Success ||
                !lines[index + 1].StartsWith("  Connection: ", StringComparison.Ordinal))
            {
                continue;
            }
            string connection = lines[index + 1][14..].Trim();
            string key = DisplayKey(match.Groups["url"].Value, connection);
            if (!targets.TryGetValue(key, out BlockCheckReportTarget? target))
            {
                throw new InvalidDataException(
                    $"Probe evidence references a target absent from the text report: {match.Groups["url"].Value}");
            }

            int attempts = int.Parse(match.Groups["attempts"].Value, CultureInfo.InvariantCulture);
            int successes = int.Parse(match.Groups["successes"].Value, CultureInfo.InvariantCulture);
            double? median = match.Groups["median"].Success
                ? double.Parse(match.Groups["median"].Value, CultureInfo.InvariantCulture)
                : null;
            int[] statuses = match.Groups["statuses"].Success
                ? match.Groups["statuses"].Value.Split('/').Select(value =>
                    int.Parse(value, CultureInfo.InvariantCulture)).ToArray()
                : [];
            string[] failures = match.Groups["failures"].Success
                ? match.Groups["failures"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [];
            probes.Add(new BlockCheckReportProbe(
                match.Groups["kind"].Value,
                0,
                string.Equals(match.Groups["strategy"].Value, "baseline", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : match.Groups["strategy"].Value,
                target.Id,
                attempts,
                successes,
                attempts == 0 ? 0d : (double)successes / attempts,
                median,
                median,
                statuses,
                failures));
            index++;
        }
        return probes;
    }

    private static List<BlockCheckIssue> ParseIssues(IReadOnlyList<string> lines)
    {
        int start = FindSection(lines, "Issues:");
        if (start < 0)
        {
            return [];
        }
        int limit = SectionLimit(lines, start, "Preset arguments:");
        List<BlockCheckIssue> issues = [];
        for (int index = start + 1; index < limit; index++)
        {
            Match match = IssueRegex().Match(lines[index]);
            if (!match.Success ||
                !Enum.TryParse(match.Groups["severity"].Value, ignoreCase: true, out BlockCheckIssueSeverity severity))
            {
                continue;
            }
            issues.Add(new BlockCheckIssue(
                severity,
                match.Groups["code"].Value,
                match.Groups["message"].Value,
                match.Groups["subject"].Success ? match.Groups["subject"].Value : null));
        }
        return issues;
    }

    private static string ParsePresetArguments(IReadOnlyList<string> lines)
    {
        int start = FindSection(lines, "Preset arguments:");
        return start < 0
            ? string.Empty
            : string.Join(' ', lines.Skip(start + 1).Where(line => !string.IsNullOrWhiteSpace(line))).Trim();
    }

    private static ParsedTarget ParseTarget(string url, string connection)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            !BlockCheckTarget.IsValidHost(uri.Host))
        {
            throw new InvalidDataException($"The text report contains an invalid target URL: {url}");
        }
        Match portMatch = PortRegex().Match(connection);
        if (!portMatch.Success)
        {
            throw new InvalidDataException($"The text report contains invalid connection details: {connection}");
        }
        BlockCheckProtocol protocol = connection switch
        {
            _ when connection.StartsWith("HTTPS (automatic TLS)", StringComparison.OrdinalIgnoreCase) => BlockCheckProtocol.TlsAuto,
            _ when connection.StartsWith("Browser TLS", StringComparison.OrdinalIgnoreCase) => BlockCheckProtocol.TlsAuto,
            _ when connection.StartsWith("HTTP", StringComparison.OrdinalIgnoreCase) => BlockCheckProtocol.Http,
            _ when connection.StartsWith("TLS 1.2", StringComparison.OrdinalIgnoreCase) => BlockCheckProtocol.Tls12,
            _ when connection.StartsWith("TLS 1.3", StringComparison.OrdinalIgnoreCase) => BlockCheckProtocol.Tls13,
            _ when connection.StartsWith("QUIC", StringComparison.OrdinalIgnoreCase) => BlockCheckProtocol.Quic,
            _ => throw new InvalidDataException($"The text report contains an unknown protocol: {connection}"),
        };
        BlockCheckIpVersion ipVersion = connection.Contains("IPv6", StringComparison.OrdinalIgnoreCase)
            ? BlockCheckIpVersion.IPv6
            : BlockCheckIpVersion.IPv4;
        BlockCheckTransport transport = connection.Contains("UDP:", StringComparison.OrdinalIgnoreCase)
            ? BlockCheckTransport.Udp
            : BlockCheckTransport.Tcp;
        int port = int.Parse(portMatch.Groups["port"].Value, CultureInfo.InvariantCulture);
        return new ParsedTarget(
            BlockCheckTarget.NormalizeHost(uri.Host),
            string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery,
            protocol,
            ipVersion,
            transport,
            port,
            protocol == BlockCheckProtocol.Http ? "http" : protocol == BlockCheckProtocol.Quic ? "quic" : "tls");
    }

    private static int FindSection(IReadOnlyList<string> lines, string name)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            if (string.Equals(lines[index].Trim(), name, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }

    private static int SectionLimit(IReadOnlyList<string> lines, int start, params string[] sections)
    {
        int limit = lines.Count;
        foreach (string section in sections)
        {
            int index = FindSection(lines, section);
            if (index > start)
            {
                limit = Math.Min(limit, index);
            }
        }
        return limit;
    }

    private static string Header(IReadOnlyList<string> lines, string name)
    {
        string prefix = name + ":";
        string? line = lines.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));
        return line == null
            ? throw new InvalidDataException($"The text report has no '{name}' header.")
            : line[prefix.Length..].Trim();
    }

    private static string? OptionalHeader(IReadOnlyList<string> lines, string name)
    {
        string prefix = name + ":";
        string? line = lines.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));
        return line?[prefix.Length..].Trim();
    }

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed)
            ? parsed
            : throw new InvalidDataException("The text report contains an invalid creation date.");

    private static bool ParseBool(string value, string name) =>
        bool.TryParse(value, out bool parsed)
            ? parsed
            : throw new InvalidDataException($"The text report contains an invalid '{name}' value.");

    private static BlockCheckIpVersion ParseIpVersion(string value) =>
        Enum.TryParse(value, ignoreCase: true, out BlockCheckIpVersion parsed)
            ? parsed
            : throw new InvalidDataException("A text-report profile contains an unknown IP version.");

    private static BlockCheckTransport ParseTransport(string value) =>
        Enum.TryParse(value, ignoreCase: true, out BlockCheckTransport parsed)
            ? parsed
            : throw new InvalidDataException("A text-report profile contains an unknown transport.");

    private static string[] SplitValues(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string DisplayKey(string url, string connection) =>
        $"{url.Trim()}|{connection.Trim()}";

    private static string TargetUrl(BlockCheckReportTarget target) =>
        BlockCheckTargetDisplayFormatter.FormatUrl(target.Host, target.Path, target.Protocol, target.Port);

    private static string TargetConnection(BlockCheckReportTarget target) =>
        BlockCheckTargetDisplayFormatter.FormatConnectionDetails(
            target.Protocol,
            target.IpVersion,
            target.Transport,
            target.Port);

    [GeneratedRegex("^\\[(?<name>[^\\]]+)\\](?<best> \\[best effort\\])? (?<l7>[^/]+)/(?<transport>[^/]+)/(?<ip>[^:]+):(?<port>\\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileHeaderRegex();

    [GeneratedRegex("^- (?<kind>\\S+) (?<strategy>.*?) / (?<url>https?://.*): (?<successes>\\d+)/(?<attempts>\\d+) \\([^)]+\\)(?:, median=(?<median>\\d+(?:\\.\\d+)?) ms)?(?:, HTTP=(?<statuses>[0-9/]+))?(?:, failures=(?<failures>[^\\r\\n]+))?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ProbeRegex();

    [GeneratedRegex("^- (?<severity>\\w+) (?<code>\\S+?)(?: \\[(?<subject>.*)\\])?: (?<message>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex IssueRegex();

    [GeneratedRegex("(?<port>\\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PortRegex();

    private sealed class TemporaryProfile(
        string name,
        bool isBestEffort,
        BlockCheckIpVersion ipVersion,
        BlockCheckTransport transport,
        int port,
        string layer7)
    {
        public string Name { get; } = name;
        public bool IsBestEffort { get; } = isBestEffort;
        public BlockCheckIpVersion IpVersion { get; } = ipVersion;
        public BlockCheckTransport Transport { get; } = transport;
        public int Port { get; } = port;
        public string Layer7 { get; } = layer7;
        public List<string> Domains { get; } = [];
        public List<string> HostListPaths { get; } = [];
        public string Primary { get; set; } = string.Empty;
        public List<string> Fallbacks { get; } = [];

        public BlockCheckReportProfile ToReportProfile(IReadOnlyList<BlockCheckReportTarget> targets)
        {
            string[] targetIds = targets
                .Where(target =>
                    target.IpVersion == IpVersion &&
                    target.Transport == Transport &&
                    target.Port == Port &&
                    string.Equals(
                        target.Protocol == BlockCheckProtocol.Http
                            ? "http"
                            : target.Protocol == BlockCheckProtocol.Quic ? "quic" : "tls",
                        Layer7,
                        StringComparison.OrdinalIgnoreCase) &&
                    (Domains.Count == 0 || Domains.Contains(target.Host, StringComparer.OrdinalIgnoreCase)))
                .Select(target => target.Id)
                .ToArray();
            return new BlockCheckReportProfile(
                Name,
                IpVersion,
                Transport,
                Port,
                Layer7,
                Domains,
                HostListPaths,
                Primary,
                Fallbacks,
                targetIds,
                IsBestEffort);
        }
    }

    private sealed record ParsedTarget(
        string Host,
        string Path,
        BlockCheckProtocol Protocol,
        BlockCheckIpVersion IpVersion,
        BlockCheckTransport Transport,
        int Port,
        string Layer7);
}
