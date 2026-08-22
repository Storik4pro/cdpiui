using System.Text;
using CDPIUI.AddOns.BlockCheck2.Catalog;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.AddOns.BlockCheck2.Reporting;

public sealed class BlockCheckReportImportResult
{
    public BlockCheckReport Report { get; init; } = new();
    public BlockCheckResultSession Session { get; init; } = new();
    public IReadOnlyList<BlockCheckIssue> Issues { get; init; } = [];
}

public sealed class BlockCheckReportImportService
{
    private const long MaximumReportBytes = 64L * 1024L * 1024L;
    private readonly BlockCheckReportSerializer _serializer;

    public BlockCheckReportImportService(BlockCheckReportSerializer? serializer = null)
    {
        _serializer = serializer ?? new BlockCheckReportSerializer();
    }

    public async Task<BlockCheckReportImportResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);
        FileInfo file = new(fullPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The BlockCheck2 report was not found.", fullPath);
        }
        if (file.Length <= 0 || file.Length > MaximumReportBytes)
        {
            throw new InvalidDataException("The BlockCheck2 report is empty or exceeds 64 MiB.");
        }

        string json = await File.ReadAllTextAsync(
                fullPath,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                cancellationToken)
            .ConfigureAwait(false);
        BlockCheckReport report = json.AsSpan().TrimStart().StartsWith("{".AsSpan(), StringComparison.Ordinal)
            ? _serializer.DeserializeJson(json)
            : BlockCheckTextReportParser.Parse(json);
        return Import(report);
    }

    public Task<BlockCheckReportImportResult> LoadJsonAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        LoadAsync(filePath, cancellationToken);

    public BlockCheckReportImportResult Import(BlockCheckReport sourceReport)
    {
        ArgumentNullException.ThrowIfNull(sourceReport);
        ValidateReport(sourceReport);

        StrategyCatalog catalog = StrategyCatalogLoader.LoadBuiltIn();
        List<BlockCheckIssue> importIssues = [];
        if (!string.Equals(
                sourceReport.CatalogVersion,
                catalog.CatalogVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            importIssues.Add(Warning(
                "REPORT_CATALOG_VERSION_MISMATCH",
                $"The report uses catalog '{sourceReport.CatalogVersion}', while this CDPIUI build " +
                $"contains '{catalog.CatalogVersion}'. Unknown strategies cannot be edited or tested."));
        }

        Dictionary<string, IReadOnlyList<string>> hostListsByTarget =
            BuildHostListsByTarget(sourceReport);
        BlockCheckTarget[] targets = sourceReport.Targets
            .Select(target => new BlockCheckTarget
            {
                Id = target.Id,
                Host = target.Host,
                Path = target.Path,
                Protocol = target.Protocol,
                IpVersion = target.IpVersion,
                CustomPort = target.Port,
                HostListPaths = hostListsByTarget[target.Id],
            })
            .ToArray();

        foreach (string missingPath in sourceReport.Profiles
                     .SelectMany(profile => profile.HostListPaths)
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Where(path => !File.Exists(path)))
        {
            importIssues.Add(Warning(
                "REPORT_SITE_LIST_MISSING",
                "A site list referenced by the imported report does not exist on this computer. " +
                "The saved config can be inspected, but that list scope must be repaired before use.",
                missingPath));
        }

        ProbeResult[] baseline = sourceReport.Probes
            .Where(probe => string.Equals(probe.Kind, "baseline", StringComparison.OrdinalIgnoreCase))
            .Select(ToProbeResult)
            .ToArray();
        ProbeResult[] strategyProbes = sourceReport.Probes
            .Where(probe => string.Equals(probe.Kind, "strategy", StringComparison.OrdinalIgnoreCase))
            .Select(ToProbeResult)
            .ToArray();
        IReadOnlyList<Zapret2ProfilePlan> profiles = RestoreProfiles(
            sourceReport,
            catalog,
            importIssues);

        BlockCheckReport report = CopyWithIssues(sourceReport, sourceReport.Issues
            .Concat(importIssues)
            .DistinctBy(issue => (issue.Severity, issue.Code, issue.Message, issue.SubjectId))
            .ToArray());
        BlockCheckRunResult runResult = new()
        {
            PreflightIssues = report.Issues,
            Scan = new BlockCheckScanResult
            {
                BaselineResults = baseline,
                ProbeResults = strategyProbes,
                WasCanceled = report.WasCanceled,
            },
            Synthesis = new BlockCheckSynthesisResult
            {
                Profiles = profiles,
                Configuration = new Zapret2WriteResult
                {
                    CommandLine = report.PresetArguments,
                },
                IsBestEffort = report.IsBestEffort,
            },
            WasCanceled = report.WasCanceled,
        };
        BlockCheckRunOptions runOptions = BlockCheckRunPresetFactory.Create(report.RunPreset);
        BlockCheckResultSession session = new()
        {
            Catalog = catalog,
            Targets = targets,
            RunResult = runResult,
            Report = report,
            RunOptions = runOptions,
        };

        return new BlockCheckReportImportResult
        {
            Report = report,
            Session = session,
            Issues = importIssues,
        };
    }

    private static void ValidateReport(BlockCheckReport report)
    {
        if (report.SchemaVersion != 2)
        {
            throw new InvalidDataException(
                $"Unsupported BlockCheck2 report schema {report.SchemaVersion}. Expected schema 2.");
        }
        if (report.Targets is null || report.Probes is null || report.Profiles is null ||
            report.ValidationAttempts is null || report.Issues is null)
        {
            throw new InvalidDataException("The BlockCheck2 report contains null collections.");
        }
        if (report.Targets.Count > 100_000 || report.Probes.Count > 2_000_000 ||
            report.Profiles.Count > 100_000)
        {
            throw new InvalidDataException("The BlockCheck2 report contains unreasonable item counts.");
        }
        if (report.Targets.Any(target =>
                target is null ||
                string.IsNullOrWhiteSpace(target.Id) ||
                !BlockCheckTarget.IsValidHost(target.Host) ||
                target.Port is < 1 or > 65535 ||
                !Enum.IsDefined(target.Protocol) ||
                !Enum.IsDefined(target.IpVersion) ||
                !Enum.IsDefined(target.Transport)) ||
            report.Targets.Select(target => target.Id).Distinct(StringComparer.Ordinal).Count() !=
            report.Targets.Count)
        {
            throw new InvalidDataException("The BlockCheck2 report contains invalid or duplicate targets.");
        }
        HashSet<string> targetIds = report.Targets
            .Select(target => target.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (report.Probes.Any(probe =>
                probe is null ||
                !targetIds.Contains(probe.TargetId) ||
                string.IsNullOrWhiteSpace(probe.Kind) ||
                probe.AttemptCount is < 0 or > 10_000 ||
                probe.SuccessCount < 0 ||
                probe.SuccessCount > probe.AttemptCount ||
                probe.HttpStatusCodes is null ||
                probe.FailureCodes is null))
        {
            throw new InvalidDataException("The BlockCheck2 report contains invalid probe evidence.");
        }
        if (report.Profiles.Any(profile =>
                profile is null ||
                string.IsNullOrWhiteSpace(profile.Name) ||
                string.IsNullOrWhiteSpace(profile.PrimaryStrategyId) ||
                profile.Domains is null ||
                profile.HostListPaths is null ||
                profile.FallbackStrategyIds is null ||
                profile.TargetIds is null ||
                profile.TargetIds.Any(id => !targetIds.Contains(id))))
        {
            throw new InvalidDataException("The BlockCheck2 report contains invalid preset profiles.");
        }
        if (report.Issues.Any(issue =>
                issue is null ||
                string.IsNullOrWhiteSpace(issue.Code) ||
                !Enum.IsDefined(issue.Severity)) ||
            report.ValidationAttempts.Any(attempt =>
                attempt is null ||
                attempt.CandidateNumber < 0 ||
                attempt.FailedTargetIds is null ||
                attempt.ExcludedStrategyIds is null ||
                attempt.FailedTargetIds.Any(id => !targetIds.Contains(id))))
        {
            throw new InvalidDataException("The BlockCheck2 report contains invalid diagnostics.");
        }
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildHostListsByTarget(
        BlockCheckReport report)
    {
        bool inferredTextScope = report.Issues.Any(issue =>
            string.Equals(issue.Code, "REPORT_TEXT_SCOPE_INFERRED", StringComparison.Ordinal));
        if (!inferredTextScope)
        {
            return report.Targets.ToDictionary(
                target => target.Id,
                target => (IReadOnlyList<string>)report.Profiles
                    .Where(profile => profile.TargetIds.Contains(target.Id, StringComparer.Ordinal))
                    .SelectMany(profile => profile.HostListPaths)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.Ordinal);
        }

        // The legacy text export omits profile target IDs. Recover the scope from
        // the referenced files when they are still available. A missing file is
        // intentionally not assigned to every target because that would make a
        // manually built config look precise while silently using the wrong list.
        Dictionary<string, HashSet<string>?> domainsByPath = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in report.Profiles
                     .SelectMany(profile => profile.HostListPaths)
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            domainsByPath[path] = TryReadHostList(path);
        }

        return report.Targets.ToDictionary(
            target => target.Id,
            target => (IReadOnlyList<string>)domainsByPath
                .Where(item => item.Value != null && HostMatchesList(target.Host, item.Value))
                .Select(item => item.Key)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static HashSet<string>? TryReadHostList(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            HashSet<string> domains = new(StringComparer.OrdinalIgnoreCase);
            foreach (string source in File.ReadLines(fullPath))
            {
                string line = source.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }
                line = line.TrimStart('^').TrimEnd('.');
                if (Uri.TryCreate(line, UriKind.Absolute, out Uri? uri))
                {
                    line = uri.Host;
                }
                string domain = BlockCheckTarget.NormalizeHost(line);
                if (BlockCheckTarget.IsValidHost(domain))
                {
                    domains.Add(domain);
                }
            }
            return domains;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool HostMatchesList(string host, IReadOnlySet<string> domains)
    {
        string candidate = BlockCheckTarget.NormalizeHost(host);
        while (candidate.Length > 0)
        {
            if (domains.Contains(candidate))
            {
                return true;
            }
            int separator = candidate.IndexOf('.');
            candidate = separator < 0 ? string.Empty : candidate[(separator + 1)..];
        }
        return false;
    }

    private static ProbeResult ToProbeResult(BlockCheckReportProbe probe)
    {
        int status = probe.HttpStatusCodes.FirstOrDefault();
        string failure = probe.FailureCodes.FirstOrDefault() ?? "imported-probe-failed";
        List<ProbeAttempt> attempts = [];
        for (int index = 0; index < probe.SuccessCount; index++)
        {
            attempts.Add(new ProbeAttempt
            {
                Success = true,
                TimeToFirstByteMs = probe.MedianTimeToFirstByteMs ?? 0d,
                HttpStatusCode = status,
            });
        }
        for (int index = probe.SuccessCount; index < probe.AttemptCount; index++)
        {
            attempts.Add(new ProbeAttempt
            {
                Success = false,
                TimeToFirstByteMs = -1d,
                ExitCode = -1,
                FailureCode = failure,
            });
        }
        return new ProbeResult
        {
            StrategyId = probe.StrategyId,
            TargetId = probe.TargetId,
            Attempts = attempts,
        };
    }

    private static IReadOnlyList<Zapret2ProfilePlan> RestoreProfiles(
        BlockCheckReport report,
        StrategyCatalog catalog,
        ICollection<BlockCheckIssue> issues)
    {
        Dictionary<string, StrategyDefinition> strategies = catalog.Strategies
            .ToDictionary(strategy => strategy.Id, StringComparer.OrdinalIgnoreCase);
        List<Zapret2ProfilePlan> result = [];
        foreach (BlockCheckReportProfile source in report.Profiles)
        {
            if (!strategies.TryGetValue(source.PrimaryStrategyId, out StrategyDefinition? primary))
            {
                issues.Add(Warning(
                    "REPORT_STRATEGY_UNKNOWN",
                    "A strategy referenced by the imported report is absent from the active catalog.",
                    source.PrimaryStrategyId));
                continue;
            }

            Zapret2ProfilePlan profile = new()
            {
                Name = source.Name,
                Filter = new Zapret2ProfileFilter
                {
                    IpVersion = source.IpVersion,
                    Transport = source.Transport,
                    Port = source.Port,
                    Layer7Protocol = source.Layer7Protocol,
                    Domains = source.Domains,
                    HostListPaths = source.HostListPaths,
                },
                Primary = primary,
                TargetIds = source.TargetIds.ToHashSet(StringComparer.Ordinal),
                IsBestEffort = source.IsBestEffort,
            };
            foreach (string fallbackId in source.FallbackStrategyIds)
            {
                if (strategies.TryGetValue(fallbackId, out StrategyDefinition? fallback))
                {
                    profile.Fallbacks.Add(fallback);
                }
                else
                {
                    issues.Add(Warning(
                        "REPORT_STRATEGY_UNKNOWN",
                        "A fallback strategy referenced by the imported report is absent from the active catalog.",
                        fallbackId));
                }
            }
            result.Add(profile);
        }
        return result;
    }

    private static BlockCheckReport CopyWithIssues(
        BlockCheckReport source,
        IReadOnlyList<BlockCheckIssue> issues) => new()
    {
        SchemaVersion = source.SchemaVersion,
        CreatedAtUtc = source.CreatedAtUtc,
        CatalogVersion = source.CatalogVersion,
        RunPreset = source.RunPreset,
        Success = source.Success,
        WasCanceled = source.WasCanceled,
        IsBestEffort = source.IsBestEffort,
        Targets = source.Targets,
        Probes = source.Probes,
        Profiles = source.Profiles,
        ValidationAttempts = source.ValidationAttempts,
        Issues = issues,
        PresetArguments = source.PresetArguments,
    };

    private static BlockCheckIssue Warning(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Warning, code, message, subjectId);
}
