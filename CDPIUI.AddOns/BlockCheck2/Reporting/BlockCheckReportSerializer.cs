using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Reporting;

public sealed class BlockCheckReportSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string SerializeJson(BlockCheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public BlockCheckReport DeserializeJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            return JsonSerializer.Deserialize<BlockCheckReport>(json, JsonOptions) ??
                throw new InvalidDataException("The BlockCheck2 report is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The file is not a valid BlockCheck2 JSON report.", exception);
        }
    }

    public string SerializeText(BlockCheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        StringBuilder output = new();
        output.AppendLine("CDPIUI BlockCheck2 report");
        output.AppendLine($"Created (UTC): {report.CreatedAtUtc:O}");
        output.AppendLine($"Catalog: {report.CatalogVersion}");
        output.AppendLine($"Mode: {report.RunPreset}");
        output.AppendLine($"Success: {report.Success}");
        output.AppendLine($"Canceled: {report.WasCanceled}");
        output.AppendLine($"Best effort: {report.IsBestEffort}");
        output.AppendLine($"Targets: {report.Targets.Count}");
        output.AppendLine($"Profiles: {report.Profiles.Count}");
        output.AppendLine($"Validation candidates: {report.ValidationAttempts.Count}");

        Dictionary<string, string> targetUrls = report.Targets.ToDictionary(
            target => target.Id,
            TargetUrl,
            StringComparer.Ordinal);
        Dictionary<string, string> targetConnectionDetails = report.Targets.ToDictionary(
            target => target.Id,
            TargetConnectionDetails,
            StringComparer.Ordinal);
        if (report.Targets.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("Targets:");
            foreach (BlockCheckReportTarget target in report.Targets)
            {
                output.AppendLine($"- {TargetUrl(target)}");
                output.AppendLine($"  Connection: {TargetConnectionDetails(target)}");
            }
        }

        foreach (BlockCheckReportProfile profile in report.Profiles)
        {
            output.AppendLine();
            string bestEffort = profile.IsBestEffort ? " [best effort]" : string.Empty;
            output.AppendLine($"[{profile.Name}]{bestEffort} {profile.Layer7Protocol}/{profile.Transport}/{profile.IpVersion}:{profile.Port}");
            if (profile.Domains.Count > 0)
            {
                output.AppendLine($"Domains: {string.Join(", ", profile.Domains)}");
            }
            if (profile.HostListPaths.Count > 0)
            {
                output.AppendLine($"Site lists: {string.Join(", ", profile.HostListPaths)}");
            }
            output.AppendLine($"Primary: {profile.PrimaryStrategyId}");
            if (profile.FallbackStrategyIds.Count > 0)
            {
                output.AppendLine($"Fallbacks: {string.Join(", ", profile.FallbackStrategyIds)}");
            }
        }

        if (report.Probes.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("Probe results:");
            foreach (BlockCheckReportProbe probe in report.Probes)
            {
                string strategy = string.IsNullOrWhiteSpace(probe.StrategyId)
                    ? "baseline"
                    : probe.StrategyId;
                string timing = probe.MedianTimeToFirstByteMs.HasValue
                    ? $", median={probe.MedianTimeToFirstByteMs.Value:0} ms"
                    : string.Empty;
                string statuses = probe.HttpStatusCodes.Count > 0
                    ? $", HTTP={string.Join('/', probe.HttpStatusCodes)}"
                    : string.Empty;
                string failures = probe.FailureCodes.Count > 0
                    ? $", failures={string.Join(',', probe.FailureCodes)}"
                    : string.Empty;
                output.AppendLine(
                    $"- {probe.Kind} {strategy} / " +
                    $"{targetUrls.GetValueOrDefault(probe.TargetId, probe.TargetId)}: " +
                    $"{probe.SuccessCount}/{probe.AttemptCount} ({probe.SuccessRate:P0})" +
                    $"{timing}{statuses}{failures}");
                if (targetConnectionDetails.TryGetValue(probe.TargetId, out string? connectionDetails))
                {
                    output.AppendLine($"  Connection: {connectionDetails}");
                }
            }
        }

        if (report.Issues.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("Issues:");
            foreach (var issue in report.Issues)
            {
                string subject = string.IsNullOrWhiteSpace(issue.SubjectId)
                    ? string.Empty
                    : $" [{issue.SubjectId}]";
                output.AppendLine($"- {issue.Severity} {issue.Code}{subject}: {issue.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(report.PresetArguments))
        {
            output.AppendLine();
            output.AppendLine("Preset arguments:");
            output.AppendLine(report.PresetArguments);
        }

        return output.ToString();
    }

    public Task SaveJsonAsync(
        string filePath,
        BlockCheckReport report,
        CancellationToken cancellationToken = default) =>
        SaveAsync(filePath, SerializeJson(report), cancellationToken);

    public Task SaveTextAsync(
        string filePath,
        BlockCheckReport report,
        CancellationToken cancellationToken = default) =>
        SaveAsync(filePath, SerializeText(report), cancellationToken);

    public Task SavePresetArgumentsAsync(
        string filePath,
        BlockCheckReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        if ((!report.Success && !report.IsBestEffort) || string.IsNullOrWhiteSpace(report.PresetArguments))
        {
            throw new InvalidOperationException(
                "A successful or explicitly best-effort report with preset arguments is required for export.");
        }

        return SaveAsync(filePath, report.PresetArguments + Environment.NewLine, cancellationToken);
    }

    public Task SavePresetArgumentsAsync(
        string filePath,
        string presetArguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetArguments);
        return SaveAsync(filePath, presetArguments + Environment.NewLine, cancellationToken);
    }

    private static Task SaveAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Report directory does not exist: {directory}");
        }

        return File.WriteAllTextAsync(
            fullPath,
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string TargetUrl(BlockCheckReportTarget target) =>
        BlockCheckTargetDisplayFormatter.FormatUrl(
            target.Host,
            target.Path,
            target.Protocol,
            target.Port);

    private static string TargetConnectionDetails(BlockCheckReportTarget target) =>
        BlockCheckTargetDisplayFormatter.FormatConnectionDetails(
            target.Protocol,
            target.IpVersion,
            target.Transport,
            target.Port);
}
