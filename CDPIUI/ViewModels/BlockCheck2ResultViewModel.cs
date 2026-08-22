using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.AddOns.BlockCheck2.Synthesis;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Shared.Models;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2ResultViewModel : INotifyPropertyChanged
{
    private readonly BlockCheckManualPresetService manualPresetService = new();
    private readonly BlockCheckManualStrategyTestService manualTestService = new();
    private BlockCheckReport? report;
    private BlockCheckResultSession? session;
    private IReadOnlyList<BlockCheck2StrategyEvidenceItem> allStrategyEvidence = [];
    private readonly List<ProbeResult> manualTestResults = [];
    private readonly HashSet<string> automaticAssignmentKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> acknowledgedManualIssueKeys = new(StringComparer.Ordinal);
    private BlockCheckManualPresetResult? manualPreset;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BlockCheck2ResultProfileItem> Profiles { get; } = [];
    public ObservableCollection<BlockCheck2ResultProbeItem> Probes { get; } = [];
    public ObservableCollection<BlockCheckIssue> Issues { get; } = [];
    public ObservableCollection<BlockCheck2StrategyEvidenceItem> StrategyEvidence { get; } = [];
    public ObservableCollection<BlockCheck2SiteStrategyGroup> StrategyGroups { get; } = [];
    public BlockCheck2PresetDraft Draft { get; } = new();
    public ObservableCollection<BlockCheck2ManualAssignmentItem> ManualAssignments => Draft.Assignments;
    public ObservableCollection<BlockCheck2ManualIssueItem> ManualIssues { get; } = [];

    public BlockCheckReport? Report => report;
    public bool HasReport => report != null;
    public bool HasSession => session != null;
    public bool Success => report?.Success == true;
    public bool IsBestEffort => report?.IsBestEffort == true;
    public bool NoBypassRequired => report?.Issues.Any(issue =>
        string.Equals(issue.Code, "NO_BYPASS_REQUIRED", StringComparison.Ordinal)) == true;
    public bool IsHttpOnly => report?.Targets.Count > 0 &&
        report.Targets.All(target => target.Protocol == BlockCheckProtocol.Http);
    public bool HasStrategyEvidence => StrategyEvidence.Count > 0;
    public bool HasManualAssignments => ManualAssignments.Count > 0;
    public bool CanUseConfig => Draft.CanUseConfig;
    public string PresetArguments => report?.PresetArguments ?? string.Empty;
    public string ManualPresetArguments => manualPreset?.Configuration.CommandLine ?? string.Empty;
    public string EffectivePresetArguments => Draft.EffectiveArguments;
    public int TargetCount => report?.Targets.Count ?? 0;
    public int ProfileCount => report?.Profiles.Count ?? 0;
    public int ProbeCount => report?.Probes.Count ?? 0;
    public int IssueCount => report?.Issues.Count ?? 0;
    public string CreatedAtLocalText => report == null
        ? string.Empty
        : report.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string RunPresetText => report?.RunPreset.ToString() ?? string.Empty;
    public int StrategyEvidenceCount => allStrategyEvidence.Count;
    public int VisibleStrategyEvidenceCount => StrategyEvidence.Count;
    public int ManualAssignmentCount => ManualAssignments.Count;

    public BlockCheck2ResultViewModel()
    {
        Draft.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(BlockCheck2PresetDraft.EffectiveArguments) or
                nameof(BlockCheck2PresetDraft.CanUseConfig))
            {
                Notify(nameof(EffectivePresetArguments));
                Notify(nameof(CanUseConfig));
            }
        };
    }

    public void Load(BlockCheckReport value)
    {
        ArgumentNullException.ThrowIfNull(value);

        report = value;
        session = null;
        allStrategyEvidence = [];
        manualTestResults.Clear();
        automaticAssignmentKeys.Clear();
        acknowledgedManualIssueKeys.Clear();
        StrategyEvidence.Clear();
        StrategyGroups.Clear();
        ManualAssignments.Clear();
        ManualIssues.Clear();
        manualPreset = null;
        Draft.LoadAutomatic(value.PresetArguments, value.Profiles);
        Profiles.Clear();
        foreach (BlockCheckReportProfile profile in value.Profiles)
        {
            Profiles.Add(new BlockCheck2ResultProfileItem(profile));
        }

        Probes.Clear();
        var targetsById = value.Targets.ToDictionary(target => target.Id, StringComparer.Ordinal);
        foreach (BlockCheckReportProbe probe in value.Probes)
        {
            targetsById.TryGetValue(probe.TargetId, out BlockCheckReportTarget? target);
            Probes.Add(new BlockCheck2ResultProbeItem(probe, target));
        }

        Issues.Clear();
        foreach (BlockCheckIssue issue in value.Issues)
        {
            Issues.Add(issue);
        }

        Notify(nameof(Report));
        Notify(nameof(HasReport));
        Notify(nameof(HasSession));
        Notify(nameof(Success));
        Notify(nameof(IsBestEffort));
        Notify(nameof(NoBypassRequired));
        Notify(nameof(IsHttpOnly));
        Notify(nameof(CanUseConfig));
        Notify(nameof(PresetArguments));
        Notify(nameof(ManualPresetArguments));
        Notify(nameof(EffectivePresetArguments));
        Notify(nameof(TargetCount));
        Notify(nameof(ProfileCount));
        Notify(nameof(ProbeCount));
        Notify(nameof(IssueCount));
        Notify(nameof(CreatedAtLocalText));
        Notify(nameof(RunPresetText));
        NotifyManualState();
    }

    public void Load(BlockCheckResultSession value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Load(value.Report);
        session = value;
        BlockCheckManualPresetOptions options = ManualOptions(value);
        
        allStrategyEvidence = manualPresetService.BuildEvidence(
                value.Catalog,
                value.Targets,
                value.RunResult.Scan.ProbeResults,
                value.RunResult.Scan.BaselineResults,
                value.RunResult.Scan.IgnoredTargetIds,
                options)
            .Select(evidence => new BlockCheck2StrategyEvidenceItem(evidence))
            .ToArray();
        SeedAutomaticAssignments(value);
        ApplyStrategyView(string.Empty, 0, 0);
        Notify(nameof(HasSession));
        NotifyManualState();
    }

    public void ApplyStrategyView(string? searchText, int sortMode, int filterMode)
    {
        string search = (searchText ?? string.Empty).Trim();
        IEnumerable<BlockCheck2StrategyEvidenceItem> query = allStrategyEvidence;
        if (search.Length > 0)
        {
            query = query.Where(item =>
                item.SiteUrl.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                item.ConnectionDetails.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                item.StrategyArguments.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                item.StrategyName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                item.StrategyId.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        query = filterMode switch
        {
            1 => query.Where(item => item.Status == BlockCheckEvidenceStatus.Successful),
            2 => query.Where(item => item.Status == BlockCheckEvidenceStatus.Partial),
            3 => query.Where(item => item.Status == BlockCheckEvidenceStatus.Failed),
            _ => query,
        };
        query = sortMode switch
        {
            1 => query.OrderBy(item => item.SuccessRate)
                .ThenBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase),
            2 => query.OrderBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(item => item.SuccessRate),
            3 => query.OrderByDescending(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(item => item.SuccessRate),
            4 => query.OrderBy(item => item.ResponseTimeSortValue)
                .ThenByDescending(item => item.SuccessRate),
            5 => query.OrderByDescending(item => item.ResponseTimeSortValue)
                .ThenByDescending(item => item.SuccessRate),
            6 => query.OrderBy(item => item.StrategyArguments, StringComparer.Ordinal)
                .ThenBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase),
            7 => query.OrderByDescending(item => item.StrategyArguments, StringComparer.Ordinal)
                .ThenBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase),
            _ => query.OrderByDescending(item => item.SuccessRate)
                .ThenByDescending(item => item.SuccessCount)
                .ThenBy(item => item.ResponseTimeSortValue)
                .ThenBy(item => item.SiteUrl, StringComparer.CurrentCultureIgnoreCase),
        };

        StrategyEvidence.Clear();
        StrategyGroups.Clear();
        foreach (BlockCheck2StrategyEvidenceItem item in query)
        {
            StrategyEvidence.Add(item);
            BlockCheck2SiteStrategyGroup? group = StrategyGroups.FirstOrDefault(candidate =>
                string.Equals(candidate.SiteUrl, item.SiteUrl, StringComparison.CurrentCultureIgnoreCase));
            if (group == null)
            {
                group = new BlockCheck2SiteStrategyGroup(item.SiteUrl);
                StrategyGroups.Add(group);
            }
            group.Add(item);
        }
        RefreshEvidenceSelections();
        Notify(nameof(HasStrategyEvidence));
        Notify(nameof(VisibleStrategyEvidenceCount));
    }

    public bool AddForSite(BlockCheck2StrategyEvidenceItem item) =>
        AddAssignment(new BlockCheckManualAssignment(
            item.StrategyId,
            item.TargetId,
            BlockCheckManualScopeKind.Site));

    public bool AddForSiteList(BlockCheck2StrategyEvidenceItem item, string siteListPath)
    {
        BlockCheckTarget? anchor = FindCompatibleSiteListTargets(item)
            .FirstOrDefault(target => target.HostListPaths.Contains(
                siteListPath,
                StringComparer.OrdinalIgnoreCase));
        return anchor != null && AddAssignment(new BlockCheckManualAssignment(
            item.StrategyId,
            anchor.Id,
            BlockCheckManualScopeKind.SiteList,
            siteListPath));
    }

    public void RemoveAssignment(BlockCheck2ManualAssignmentItem item)
    {
        int index = ManualAssignments.IndexOf(item);
        if (index < 0)
        {
            return;
        }
        acknowledgedManualIssueKeys.Clear();
        ManualAssignments.RemoveAt(index);
        RebuildManualPreset(updateDraft: true);
    }

    public bool MoveAssignment(BlockCheck2ManualAssignmentItem item, int offset)
    {
        int oldIndex = ManualAssignments.IndexOf(item);
        int newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= ManualAssignments.Count)
        {
            return false;
        }
        acknowledgedManualIssueKeys.Clear();
        ManualAssignments.Move(oldIndex, newIndex);
        RebuildManualPreset(updateDraft: true);
        return true;
    }

    public void ClearManualAssignments()
    {
        acknowledgedManualIssueKeys.Clear();
        ManualAssignments.Clear();
        RebuildManualPreset(updateDraft: true);
    }

    public IReadOnlyList<string> GetSiteListPaths(BlockCheck2StrategyEvidenceItem item) =>
        FindCompatibleSiteListTargets(item)
            .SelectMany(target => target.HostListPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    public bool CanAddForSiteList(BlockCheck2StrategyEvidenceItem item) =>
        FindCompatibleSiteListTargets(item).Any();

    public bool ApplyManualIssueQuickFix(BlockCheck2ManualIssueItem item)
    {
        if (!item.CanQuickFix || !ManualIssues.Contains(item))
        {
            return false;
        }

        acknowledgedManualIssueKeys.Add(item.Key);
        ManualIssues.Remove(item);
        NotifyManualState();
        return true;
    }

    public OperationResultModel<string> GetFullStrategyArguments(
        BlockCheck2StrategyEvidenceItem item)
    {
        return manualTestService.GetFullStrategyArguments(
            item.Evidence.Strategy,
            item.Evidence.Target,
            new BlockCheckManualStrategyTestOptions
            {
                Attempts = Math.Max(1, session.RunOptions.Scan.AttemptsPerTarget),
            });
    } 

    public async Task<BlockCheckManualStrategyTestResult> TestStrategyAsync(
        BlockCheck2StrategyEvidenceItem item,
        CancellationToken cancellationToken = default)
    {
        if (session == null)
        {
            return new BlockCheckManualStrategyTestResult
            {
                Issues =
                [
                    new BlockCheckIssue(
                        BlockCheckIssueSeverity.Error,
                        "MANUAL_SESSION_UNAVAILABLE",
                        "The original BlockCheck session is unavailable; this report cannot launch a live strategy test."),
                ],
            };
        }

        BlockCheckManualStrategyTestResult result = await manualTestService
            .TestWithCdpiuiAdaptersAsync(
                item.Evidence.Strategy,
                item.Evidence.Target,
                new BlockCheckManualStrategyTestOptions
                {
                    Attempts = Math.Max(1, session.RunOptions.Scan.AttemptsPerTarget),
                },
                new CdpiuiZapret2StrategyRunnerOptions
                {
                    StartupGracePeriod = session.StartupGracePeriod,
                },
                new CurlBlockCheckProbeRunnerOptions
                {
                    ConnectTimeout = session.ConnectTimeout,
                    RequestTimeout = session.RequestTimeout,
                },
                cancellationToken)
            .ConfigureAwait(true);
        if (result.ProbeResult != null)
        {
            manualTestResults.RemoveAll(probe =>
                string.Equals(probe.StrategyId, result.ProbeResult.StrategyId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(probe.TargetId, result.ProbeResult.TargetId, StringComparison.Ordinal));
            manualTestResults.Add(result.ProbeResult);
            item.ApplyTestResult(result.ProbeResult);
            RebuildManualPreset(updateDraft: !Draft.IsAutomatic);
        }
        return result;
    }

    private bool AddAssignment(BlockCheckManualAssignment assignment)
    {
        if (session == null)
        {
            return false;
        }
        bool duplicate = ManualAssignments.Any(item =>
            string.Equals(item.Assignment.StrategyId, assignment.StrategyId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Assignment.AnchorTargetId, assignment.AnchorTargetId, StringComparison.Ordinal) &&
            item.Assignment.ScopeKind == assignment.ScopeKind &&
            string.Equals(item.Assignment.SiteListPath, assignment.SiteListPath, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            return false;
        }

        StrategyDefinition? strategy = session.Catalog.Strategies.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, assignment.StrategyId, StringComparison.OrdinalIgnoreCase));
        BlockCheckTarget? target = session.Targets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, assignment.AnchorTargetId, StringComparison.Ordinal));
        if (strategy == null || target == null)
        {
            return false;
        }
        acknowledgedManualIssueKeys.Clear();
        AddAssignmentCore(assignment, strategy, target);
        RebuildManualPreset(updateDraft: true);
        return true;
    }

    private void AddAssignmentCore(
        BlockCheckManualAssignment assignment,
        StrategyDefinition strategy,
        BlockCheckTarget target)
    {
        ManualAssignments.Add(new BlockCheck2ManualAssignmentItem(
            assignment,
            strategy,
            target,
            new Zapret2ConfigWriter().FormatStrategyActions(strategy)));
    }

    private void RebuildManualPreset(bool updateDraft)
    {
        ManualIssues.Clear();
        if (session == null || ManualAssignments.Count == 0)
        {
            manualPreset = null;
            RefreshAssignmentIndexes();
            if (updateDraft)
            {
                Draft.ApplyStructuredChange(string.Empty, []);
            }
            RefreshEvidenceSelections();
            NotifyManualState();
            return;
        }

        IEnumerable<ProbeResult> evidence = session.RunResult.Scan.ProbeResults
            .Where(result => !manualTestResults.Any(test =>
                string.Equals(test.StrategyId, result.StrategyId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(test.TargetId, result.TargetId, StringComparison.Ordinal)))
            .Concat(manualTestResults);
        manualPreset = manualPresetService.Build(
            session.Catalog,
            session.Targets.Where(target => !session.RunResult.Scan.IgnoredTargetIds.Contains(target.Id)),
            ManualAssignments.Select(item => item.Assignment),
            evidence,
            ManualOptions(session));
        foreach (var group in manualPreset.Issues.GroupBy(issue => new
        {
            issue.Severity,
            issue.Code,
            issue.Message,
        }))
        {
            BlockCheck2ManualIssueItem item = new(
                group.Key.Severity,
                group.Key.Code,
                group.Key.Message,
                group.Count(),
                group.Select(issue => DescribeManualIssueSubject(issue.SubjectId)));
            if (!acknowledgedManualIssueKeys.Contains(item.Key))
            {
                ManualIssues.Add(item);
            }
        }
        if (updateDraft)
        {
            Draft.ApplyStructuredChange(
                manualPreset.Configuration.CommandLine,
                manualPreset.Profiles);
        }
        RefreshAssignmentIndexes();
        RefreshEvidenceSelections();
        NotifyManualState();
    }

    private string DescribeManualIssueSubject(string? subjectId)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return string.Empty;
        }

        BlockCheck2ManualAssignmentItem? scopeAssignment = ManualAssignments.FirstOrDefault(item =>
            string.Equals(ManualScopeKey(item), subjectId, StringComparison.OrdinalIgnoreCase));
        if (scopeAssignment != null)
        {
            return $"{scopeAssignment.Scope} · {scopeAssignment.ConnectionDetails}";
        }

        BlockCheck2ManualAssignmentItem? assignment = ManualAssignments.FirstOrDefault(item =>
            string.Equals(item.Assignment.StrategyId, subjectId, StringComparison.OrdinalIgnoreCase));
        if (assignment != null)
        {
            return DescribeStrategySubject(assignment, subjectId);
        }

        string[] strategyIds = subjectId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (strategyIds.Length > 1)
        {
            string[] strategyNames = strategyIds
                .Select(strategyId =>
                {
                    BlockCheck2ManualAssignmentItem? item = ManualAssignments.FirstOrDefault(candidate =>
                        string.Equals(
                            candidate.Assignment.StrategyId,
                            strategyId,
                            StringComparison.OrdinalIgnoreCase));
                    return item == null
                        ? strategyId
                        : DescribeStrategySubject(item, strategyId);
                })
                .ToArray();
            return string.Join(" / ", strategyNames);
        }

        return subjectId;
    }

    private static string DescribeStrategySubject(
        BlockCheck2ManualAssignmentItem assignment,
        string strategyId) =>
        string.Equals(
            assignment.StrategyName,
            strategyId,
            StringComparison.CurrentCultureIgnoreCase)
            ? strategyId
            : $"{assignment.StrategyName} ({strategyId})";

    private static string ManualScopeKey(BlockCheck2ManualAssignmentItem item) =>
        $"{item.Target.IpVersion}|{item.Target.Transport}|{item.Target.Port}|{item.Target.Layer7Protocol}|" +
        (item.Assignment.ScopeKind == BlockCheckManualScopeKind.Site
            ? $"site:{BlockCheckTarget.NormalizeHost(item.Target.Host)}"
            : $"list:{Path.GetFullPath(item.Assignment.SiteListPath ?? string.Empty)}");

    private IEnumerable<BlockCheckTarget> FindCompatibleSiteListTargets(
        BlockCheck2StrategyEvidenceItem item)
    {
        if (session == null)
        {
            return [];
        }

        BlockCheckTarget source = item.Evidence.Target;
        string host = BlockCheckTarget.NormalizeHost(source.Host);
        return session.Targets.Where(target =>
            !session.RunResult.Scan.IgnoredTargetIds.Contains(target.Id) &&
            target.HostListPaths.Count > 0 &&
            string.Equals(
                BlockCheckTarget.NormalizeHost(target.Host),
                host,
                StringComparison.OrdinalIgnoreCase) &&
            target.IpVersion == source.IpVersion &&
            target.Transport == source.Transport &&
            target.Port == source.Port &&
            string.Equals(
                target.Layer7Protocol,
                source.Layer7Protocol,
                StringComparison.Ordinal));
    }

    private void SeedAutomaticAssignments(BlockCheckResultSession value)
    {
        acknowledgedManualIssueKeys.Clear();
        ManualAssignments.Clear();
        Dictionary<string, StrategyDefinition> strategies = value.Catalog.Strategies
            .ToDictionary(strategy => strategy.Id, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BlockCheckTarget> targets = value.Targets
            .ToDictionary(target => target.Id, StringComparer.Ordinal);

        foreach (BlockCheckReportProfile profile in value.Report.Profiles)
        {
            BlockCheckTarget[] profileTargets = profile.TargetIds
                .Select(id => targets.GetValueOrDefault(id))
                .Where(target => target != null)
                .Cast<BlockCheckTarget>()
                .ToArray();
            IEnumerable<(BlockCheckTarget Target, BlockCheckManualScopeKind Scope, string? Path)> scopes =
                profile.HostListPaths.Count > 0
                    ? profile.HostListPaths.Select(path => (
                        profileTargets.FirstOrDefault(target => target.HostListPaths.Contains(
                            path,
                            StringComparer.OrdinalIgnoreCase)) ?? profileTargets.FirstOrDefault(),
                        BlockCheckManualScopeKind.SiteList,
                        (string?)path))
                        .Where(scope => scope.Item1 != null)
                        .Select(scope => (scope.Item1!, scope.Item2, scope.Item3))
                    : profileTargets
                        .DistinctBy(target => BlockCheckTarget.NormalizeHost(target.Host), StringComparer.OrdinalIgnoreCase)
                        .Select(target => (target, BlockCheckManualScopeKind.Site, (string?)null));

            foreach ((BlockCheckTarget target, BlockCheckManualScopeKind scope, string? path) in scopes)
            {
                foreach (string strategyId in new[] { profile.PrimaryStrategyId }.Concat(profile.FallbackStrategyIds))
                {
                    if (!strategies.TryGetValue(strategyId, out StrategyDefinition? strategy))
                    {
                        continue;
                    }
                    BlockCheckManualAssignment assignment = new(strategyId, target.Id, scope, path);
                    bool duplicate = ManualAssignments.Any(item => item.Assignment == assignment);
                    if (!duplicate)
                    {
                        AddAssignmentCore(assignment, strategy, target);
                        automaticAssignmentKeys.Add(AssignmentKey(assignment));
                    }
                }
            }
        }

        RebuildManualPreset(updateDraft: false);
        Draft.LoadAutomatic(value.Report.PresetArguments, value.Report.Profiles);
        RefreshAssignmentIndexes();
        RefreshEvidenceSelections();
    }

    private void RefreshEvidenceSelections()
    {
        foreach (BlockCheck2StrategyEvidenceItem evidence in allStrategyEvidence)
        {
            BlockCheck2ManualAssignmentItem[] selectedAssignments = ManualAssignments
                .Where(item => AssignmentMatchesEvidence(item, evidence))
                .ToArray();
            bool selected = selectedAssignments.Length > 0;
            bool automatic = selectedAssignments.Any(item =>
                automaticAssignmentKeys.Contains(AssignmentKey(item.Assignment)));
            evidence.SetSelection(selected, automatic);
        }
    }

    private static bool AssignmentMatchesEvidence(
        BlockCheck2ManualAssignmentItem item,
        BlockCheck2StrategyEvidenceItem evidence) =>
        string.Equals(item.Assignment.StrategyId, evidence.StrategyId, StringComparison.OrdinalIgnoreCase) &&
        (item.Assignment.ScopeKind == BlockCheckManualScopeKind.Site
            ? string.Equals(
                BlockCheckTarget.NormalizeHost(item.Target.Host),
                BlockCheckTarget.NormalizeHost(evidence.Evidence.Target.Host),
                StringComparison.OrdinalIgnoreCase)
            : evidence.Evidence.Target.HostListPaths.Contains(
                  item.Assignment.SiteListPath ?? string.Empty,
                  StringComparer.OrdinalIgnoreCase) ||
              (string.Equals(
                   BlockCheckTarget.NormalizeHost(item.Target.Host),
                   BlockCheckTarget.NormalizeHost(evidence.Evidence.Target.Host),
                   StringComparison.OrdinalIgnoreCase) &&
               item.Target.IpVersion == evidence.Evidence.Target.IpVersion &&
               item.Target.Transport == evidence.Evidence.Target.Transport &&
               item.Target.Port == evidence.Evidence.Target.Port &&
               string.Equals(
                   item.Target.Layer7Protocol,
                   evidence.Evidence.Target.Layer7Protocol,
                   StringComparison.Ordinal)));

    private static string AssignmentKey(BlockCheckManualAssignment assignment) =>
        $"{assignment.StrategyId}|{assignment.AnchorTargetId}|{assignment.ScopeKind}|{assignment.SiteListPath}";

    private void RefreshAssignmentIndexes()
    {
        for (int index = 0; index < ManualAssignments.Count; index++)
        {
            ManualAssignments[index].SetOrder(index + 1);
        }
    }

    private void NotifyManualState()
    {
        Notify(nameof(HasStrategyEvidence));
        Notify(nameof(HasManualAssignments));
        Notify(nameof(CanUseConfig));
        Notify(nameof(ManualPresetArguments));
        Notify(nameof(EffectivePresetArguments));
        Notify(nameof(StrategyEvidenceCount));
        Notify(nameof(VisibleStrategyEvidenceCount));
        Notify(nameof(ManualAssignmentCount));
    }

    private static BlockCheckManualPresetOptions ManualOptions(BlockCheckResultSession session) => new()
    {
        MinimumAttempts = Math.Max(1, session.RunOptions.Synthesis.MinimumAttempts),
        MinimumSuccessRate = session.RunOptions.Synthesis.MinimumSuccessRate,
    };

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class BlockCheck2ResultProbeItem
{
    public BlockCheck2ResultProbeItem(
        BlockCheckReportProbe probe,
        BlockCheckReportTarget? target = null)
    {
        Kind = probe.Kind;
        StrategyId = string.IsNullOrWhiteSpace(probe.StrategyId) ? "baseline" : probe.StrategyId;
        TargetId = probe.TargetId;
        TargetUrl = target == null
            ? probe.TargetId
            : BlockCheckTargetDisplayFormatter.FormatUrl(
                target.Host,
                target.Path,
                target.Protocol,
                target.Port);
        ConnectionDetails = target == null
            ? string.Empty
            : BlockCheckTargetDisplayFormatter.FormatConnectionDetails(
                target.Protocol,
                target.IpVersion,
                target.Transport,
                target.Port);
        Attempts = probe.AttemptCount;
        Successes = probe.SuccessCount;
        SuccessRate = $"{probe.SuccessRate:P0}";
        Median = probe.MedianTimeToFirstByteMs.HasValue
            ? $"{probe.MedianTimeToFirstByteMs.Value:0} ms"
            : "—";
        string status = probe.HttpStatusCodes.Count > 0
            ? $" · HTTP {string.Join('/', probe.HttpStatusCodes)}"
            : string.Empty;
        string failures = probe.FailureCodes.Count > 0
            ? $" · {string.Join(", ", probe.FailureCodes)}"
            : string.Empty;
        Summary = $"{Successes}/{Attempts} · {SuccessRate} · {Median}{status}{failures}";
    }

    public string Kind { get; }
    public string StrategyId { get; }
    public string TargetId { get; }
    public string TargetUrl { get; }
    public string ConnectionDetails { get; }
    public int Attempts { get; }
    public int Successes { get; }
    public string SuccessRate { get; }
    public string Median { get; }
    public string Summary { get; }
}

public sealed class BlockCheck2ResultProfileItem
{
    public BlockCheck2ResultProfileItem(BlockCheckReportProfile profile)
    {
        Name = profile.Name;
        Route = $"{profile.Layer7Protocol}/{profile.Transport}/{profile.IpVersion}:{profile.Port}";
        Domains = string.Join(", ", profile.Domains);
        SiteLists = string.Join(", ", profile.HostListPaths);
        Sources = string.Join(", ", profile.HostListPaths.Concat(profile.Domains));
        PrimaryStrategy = profile.PrimaryStrategyId;
        FallbackStrategies = string.Join(", ", profile.FallbackStrategyIds);
        TargetCount = profile.TargetIds.Count;
        IsBestEffort = profile.IsBestEffort;
    }

    public string Name { get; }
    public string Route { get; }
    public string Domains { get; }
    public string SiteLists { get; }
    public string Sources { get; }
    public string PrimaryStrategy { get; }
    public string FallbackStrategies { get; }
    public int TargetCount { get; }
    public bool IsBestEffort { get; }
}
