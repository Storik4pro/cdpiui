using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2SiteStrategyGroup : ObservableCollection<BlockCheck2StrategyEvidenceItem>
{
    public BlockCheck2SiteStrategyGroup(string siteUrl)
    {
        SiteUrl = siteUrl;
    }

    public string SiteUrl { get; }
}

public sealed class BlockCheck2ManualIssueItem
{
    public BlockCheck2ManualIssueItem(
        BlockCheckIssueSeverity severity,
        string code,
        string message,
        int count,
        IEnumerable<string> subjects)
    {
        Severity = severity;
        Code = code;
        Message = message;
        Count = Math.Max(1, count);
        string[] materializedSubjects = subjects
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(subject => subject, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        SubjectText = string.Join(", ", materializedSubjects);
        Key = $"{Severity}|{Code}|{Message}|{string.Join('|', materializedSubjects)}";
    }

    public BlockCheckIssueSeverity Severity { get; }
    public string Code { get; }
    public string DisplayCode => Count > 1 ? $"{Code} ×{Count}" : Code;
    public string Message { get; }
    public int Count { get; }
    public string SubjectText { get; }
    public bool HasSubjects => SubjectText.Length > 0;
    public string Key { get; }
    public bool CanQuickFix => string.Equals(
        Code,
        "MANUAL_SCOPE_OVERLAP",
        StringComparison.OrdinalIgnoreCase);
}

public sealed class BlockCheck2StrategyEvidenceItem : INotifyPropertyChanged
{
    private ProbeSummary summary;
    private BlockCheckEvidenceStatus status;
    private string httpStatusText;
    private string failureText;
    private bool isSelectedForPreset;
    private bool isAutomaticSelection;

    public BlockCheck2StrategyEvidenceItem(BlockCheckStrategyEvidence evidence)
    {
        Evidence = evidence;
        summary = evidence.Summary;
        status = evidence.Status;
        SiteUrl = BlockCheckTargetDisplayFormatter.FormatUrl(evidence.Target);
        ConnectionDetails = BlockCheckTargetDisplayFormatter.FormatConnectionDetails(evidence.Target);
        StrategyName = string.IsNullOrWhiteSpace(evidence.Strategy.DisplayName)
            ? evidence.Strategy.Id
            : evidence.Strategy.DisplayName;
        httpStatusText = FormatStatuses(evidence.HttpStatusCodes);
        failureText = string.Join(", ", evidence.FailureCodes);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public BlockCheckStrategyEvidence Evidence { get; }
    public string TargetId => Evidence.Target.Id;
    public string StrategyId => Evidence.Strategy.Id;
    public string StrategyName { get; }
    public string StrategyArguments => Evidence.StrategyArguments;
    public string SiteUrl { get; }
    public string ConnectionDetails { get; }
    public BlockCheckEvidenceStatus Status => status;
    public int AttemptCount => summary.AttemptCount;
    public int SuccessCount => summary.SuccessCount;
    public double SuccessRate => summary.SuccessRate;
    public double ResponseTimeSortValue =>
        double.IsFinite(summary.MedianTimeToFirstByteMs)
            ? summary.MedianTimeToFirstByteMs
            : double.PositiveInfinity;
    public string ResultText => $"{SuccessCount}/{AttemptCount} ({SuccessRate:P0})";
    public bool HasPartialScopeWarning =>
        status == BlockCheckEvidenceStatus.Partial && SuccessCount == AttemptCount;
    public string ResponseTimeText =>
        double.IsFinite(summary.MedianTimeToFirstByteMs)
            ? $"{summary.MedianTimeToFirstByteMs:0} ms"
            : "—";
    public string HttpStatusText => httpStatusText;
    public string FailureText => failureText;
    public string BaselineText =>
        $"{Evidence.BaselineSummary.SuccessCount}/{Evidence.BaselineSummary.AttemptCount} " +
        $"({Evidence.BaselineSummary.SuccessRate:P0})";
    public string EvidenceDetails => string.Join(" · ", new[]
    {
        ResultText,
        ResponseTimeText,
        string.IsNullOrWhiteSpace(HttpStatusText) ? null : $"HTTP {HttpStatusText}",
        string.IsNullOrWhiteSpace(FailureText) ? null : FailureText,
    }.Where(value => value != null));

    public bool IsSelectedForPreset => isSelectedForPreset;
    public bool IsAutomaticSelection => isAutomaticSelection;
    public string SelectionText => !isSelectedForPreset
        ? string.Empty
        : isAutomaticSelection
            ? "Auto"
            : "Manual";

    public void SetSelection(bool selected, bool automatic)
    {
        if (isSelectedForPreset == selected && isAutomaticSelection == automatic)
        {
            return;
        }
        isSelectedForPreset = selected;
        isAutomaticSelection = automatic;
        Notify(nameof(IsSelectedForPreset));
        Notify(nameof(IsAutomaticSelection));
        Notify(nameof(SelectionText));
    }

    public void ApplyTestResult(ProbeResult result)
    {
        ProbeAttempt[] attempts = result.Attempts.ToArray();
        summary = ProbeSummary.FromAttempts(attempts);
        status = summary.AttemptCount == 0
            ? BlockCheckEvidenceStatus.Untested
            : summary.SuccessCount == summary.AttemptCount
                ? BlockCheckEvidenceStatus.Successful
                : summary.SuccessCount > 0
                    ? BlockCheckEvidenceStatus.Partial
                    : BlockCheckEvidenceStatus.Failed;
        httpStatusText = FormatStatuses(attempts
            .Select(attempt => attempt.HttpStatusCode)
            .Where(code => code > 0)
            .Distinct()
            .Order());
        failureText = string.Join(", ", attempts
            .Select(attempt => attempt.FailureCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        Notify(nameof(Status));
        Notify(nameof(AttemptCount));
        Notify(nameof(SuccessCount));
        Notify(nameof(SuccessRate));
        Notify(nameof(ResponseTimeSortValue));
        Notify(nameof(ResultText));
        Notify(nameof(HasPartialScopeWarning));
        Notify(nameof(ResponseTimeText));
        Notify(nameof(HttpStatusText));
        Notify(nameof(FailureText));
        Notify(nameof(EvidenceDetails));
    }

    private static string FormatStatuses(System.Collections.Generic.IEnumerable<int> statuses) =>
        string.Join("/", statuses);

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class BlockCheck2ManualAssignmentItem : INotifyPropertyChanged
{
    private int order;

    public BlockCheck2ManualAssignmentItem(
        BlockCheckManualAssignment assignment,
        StrategyDefinition strategy,
        BlockCheckTarget target,
        string strategyArguments)
    {
        Assignment = assignment;
        Strategy = strategy;
        Target = target;
        StrategyArguments = strategyArguments;
        Scope = assignment.ScopeKind == BlockCheckManualScopeKind.Site
            ? BlockCheckTargetDisplayFormatter.FormatUrl(target)
            : Path.GetFileName(assignment.SiteListPath) ?? assignment.SiteListPath ?? string.Empty;
        ConnectionDetails = BlockCheckTargetDisplayFormatter.FormatConnectionDetails(target);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public BlockCheckManualAssignment Assignment { get; }
    public StrategyDefinition Strategy { get; }
    public BlockCheckTarget Target { get; }
    public int Order => order;
    public string Scope { get; }
    public string ConnectionDetails { get; }
    public string StrategyArguments { get; }
    public string StrategyName => string.IsNullOrWhiteSpace(Strategy.DisplayName)
        ? Strategy.Id
        : Strategy.DisplayName;
    public string ScopeKind => Assignment.ScopeKind.ToString();

    public void SetOrder(int value)
    {
        if (order == value)
        {
            return;
        }
        order = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Order)));
    }
}
