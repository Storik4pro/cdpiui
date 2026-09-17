using CDPIUI.AddOns.BlockCheck2;
using CDPIUI.AddOns.BlockCheck2.Execution;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Data;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2ViewModel : INotifyPropertyChanged
{
    private const string SettingsGroup = "BLOCKCHECK";
    private const int MinimumRequestsPerDomain = 1;
    private const int MaximumRequestsPerDomain = 20;

    private readonly BlockCheckAddOnService addOnService;
    private readonly BlockCheckSessionPreparationService preparationService;
    private readonly BlockCheckReportBuilder reportBuilder;
    private readonly BlockCheckReportSerializer reportSerializer;
    private readonly BlockCheckReportHistoryService reportHistoryService;
    private readonly BlockCheckMaximumDurationEstimator maximumDurationEstimator = new();
    private readonly BlockCheckRemainingTimeEstimator remainingTimeEstimator = new();
    private readonly Stopwatch runStopwatch = new();
    private string generatedSiteListRoot = CreateGeneratedSiteListRoot();
    private CancellationTokenSource? cancellation;
    private string targetInput = string.Empty;
    private BlockCheckRunPreset selectedRunPreset = BlockCheckRunPreset.Balanced;
    private bool includeHttp;
    private bool includeTls12 = true;
    private bool includeTls13;
    private bool includeTlsAuto = true;
    private bool includeQuic;
    private bool includeIpv4 = true;
    private bool includeIpv6;
    private BlockCheckSessionState state = BlockCheckSessionState.Idle;
    private BlockCheckScanPhase? currentPhase;
    private int progressCurrent;
    private int progressTotal;
    private TimeSpan? estimatedRemainingTime;
    private TimeSpan? estimatedOverallRemainingTime;
    private BlockCheckScanPhase? estimatorPhase;
    private int lastObservedCheckCount;
    private TimeSpan lastObservedCheckElapsed;
    private double? secondsPerCheck;
    private int successfulChecks;
    private int failedChecks;
    private int successfulStrategies;
    private string currentTargetId = string.Empty;
    private string currentStrategyId = string.Empty;
    private string? operationError;
    private string? historySaveError;
    private IReadOnlyList<BlockCheckIssue> issues = [];
    private IReadOnlyList<BlockCheckTarget> targets = [];
    private BlockCheckRunResult? lastResult;
    private BlockCheckReport? lastReport;
    private BlockCheckResultSession? lastSession;
    private bool siteListsLoaded;
    private bool isSiteListLoading;
    private string? siteListLoadError;
    private double connectTimeoutSeconds = 5d;
    private double requestTimeoutSeconds = 10d;
    private double startupGraceSeconds = 0.75d;
    private bool testAllStrategies;
    private double requestsPerDomain = 3d;

    public BlockCheck2ViewModel(
        BlockCheckAddOnService? addOnService = null,
        BlockCheckSessionPreparationService? preparationService = null,
        BlockCheckReportBuilder? reportBuilder = null,
        BlockCheckReportSerializer? reportSerializer = null,
        BlockCheckReportHistoryService? reportHistoryService = null)
    {
        this.addOnService = addOnService ?? new BlockCheckAddOnService();
        this.preparationService = preparationService ?? new BlockCheckSessionPreparationService();
        this.reportBuilder = reportBuilder ?? new BlockCheckReportBuilder();
        this.reportSerializer = reportSerializer ?? new BlockCheckReportSerializer();
        this.reportHistoryService = reportHistoryService ?? new BlockCheckReportHistoryService(this.reportSerializer);
        LoadSettings();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<BlockCheckRunPreset> AvailableRunPresets { get; } =
        Enum.GetValues<BlockCheckRunPreset>();
    public ObservableCollection<BlockCheck2SiteListItem> AvailableSiteLists { get; } = [];
    public ObservableCollection<BlockCheck2SiteListItem> SelectedSiteLists { get; } = [];

    public string TargetInput
    {
        get => targetInput;
        set
        {
            if (targetInput == value)
            {
                return;
            }

            targetInput = value ?? string.Empty;
            Notify();
            Notify(nameof(DirectTargetInputCount));
            Notify(nameof(HasTargetSources));
            Notify(nameof(CanStart));
        }
    }

    public BlockCheckRunPreset SelectedRunPreset
    {
        get => selectedRunPreset;
        set
        {
            if (selectedRunPreset == value)
            {
                return;
            }

            selectedRunPreset = value;
            Notify();
            SaveSetting(nameof(SelectedRunPreset), (int)value);
            RequestsPerDomain = BlockCheckRunPresetFactory.GetRecommendedAttemptsPerTarget(value);
        }
    }

    public bool IncludeHttp
    {
        get => includeHttp;
        set => SetProtocolSelection(ref includeHttp, value);
    }

    public bool IncludeTls12
    {
        get => includeTls12;
        set => SetProtocolSelection(ref includeTls12, value);
    }

    public bool IncludeTls13
    {
        get => includeTls13;
        set => SetProtocolSelection(ref includeTls13, value);
    }

    public bool IncludeTlsAuto
    {
        get => includeTlsAuto;
        set => SetProtocolSelection(ref includeTlsAuto, value);
    }

    public bool IncludeQuic
    {
        get => includeQuic;
        set => SetProtocolSelection(ref includeQuic, value);
    }

    public bool IsHttpOnly => includeHttp && !includeTls12 && !includeTls13 && !includeTlsAuto && !includeQuic;

    public bool IncludeIpv4
    {
        get => includeIpv4;
        set => SetIpVersionSelection(ref includeIpv4, value);
    }

    public bool IncludeIpv6
    {
        get => includeIpv6;
        set => SetIpVersionSelection(ref includeIpv6, value);
    }

    public BlockCheckSessionState State => state;
    public BlockCheckScanPhase? CurrentPhase => currentPhase;
    public int ProgressCurrent => progressCurrent;
    public int ProgressTotal => progressTotal;
    public TimeSpan? EstimatedRemainingTime => estimatedRemainingTime;
    public TimeSpan? EstimatedOverallRemainingTime => estimatedOverallRemainingTime;
    public bool IsProgressIndeterminate => progressTotal <= 0;
    public int SuccessfulChecks => successfulChecks;
    public int FailedChecks => failedChecks;
    public int CompletedChecks => successfulChecks + failedChecks;
    public int SuccessfulStrategies => successfulStrategies;
    public double ProgressValue => progressTotal <= 0
        ? 0d
        : Math.Clamp((double)progressCurrent / progressTotal, 0d, 1d);
    public string CurrentTargetId => currentTargetId;
    public string CurrentTargetUrl
    {
        get
        {
            BlockCheckTarget? target = CurrentTarget();
            return target == null
                ? currentTargetId
                : BlockCheckTargetDisplayFormatter.FormatUrl(target);
        }
    }
    public string CurrentTargetConnectionDetails
    {
        get
        {
            BlockCheckTarget? target = CurrentTarget();
            return target == null
                ? string.Empty
                : BlockCheckTargetDisplayFormatter.FormatConnectionDetails(target);
        }
    }
    public string CurrentTargetDisplay
    {
        get
        {
            BlockCheckTarget? target = CurrentTarget();
            return target == null
                ? currentTargetId
                : BlockCheckTargetDisplayFormatter.Format(target);
        }
    }
    public string CurrentStrategyId => currentStrategyId;
    public string? OperationError => operationError;
    public string? HistorySaveError => historySaveError;
    public bool IsSiteListLoading => isSiteListLoading;
    public string? SiteListLoadError => siteListLoadError;
    public int SelectedSiteListCount => SelectedSiteLists.Count;
    public int SelectedDomainCount => SelectedSiteLists.Sum(item => item.DomainCount);
    public int DirectTargetInputCount => targetInput
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    public bool HasTargetSources => DirectTargetInputCount > 0 || SelectedSiteLists.Count > 0;
    public bool HasProtocolSelection =>
        includeHttp || includeTls12 || includeTls13 || includeTlsAuto || includeQuic;
    public bool HasIpVersionSelection => includeIpv4 || includeIpv6;
    public bool CanContinueFromTargets => !IsRunning && HasProtocolSelection && HasIpVersionSelection;
    public IReadOnlyList<BlockCheckIssue> Issues => issues;
    public IReadOnlyList<BlockCheckTarget> Targets => targets;
    public int TargetCount => targets.Count;
    public BlockCheckRunResult? LastResult => lastResult;
    public BlockCheckReport? LastReport => lastReport;
    public BlockCheckResultSession? LastSession => lastSession;
    public bool NoBypassRequired => lastReport?.Issues.Any(issue =>
        string.Equals(issue.Code, "NO_BYPASS_REQUIRED", StringComparison.Ordinal)) == true;
    public bool LastReportIsHttpOnly => lastReport?.Targets.Count > 0 &&
        lastReport.Targets.All(target => target.Protocol == BlockCheckProtocol.Http);
    public string ReportJson => lastReport == null ? string.Empty : reportSerializer.SerializeJson(lastReport);
    public string ReportText => lastReport == null ? string.Empty : reportSerializer.SerializeText(lastReport);
    public string PresetArguments => lastReport?.PresetArguments ?? string.Empty;

    public double ConnectTimeoutSeconds
    {
        get => connectTimeoutSeconds;
        set => SetAdvancedOption(ref connectTimeoutSeconds, value);
    }

    public double RequestTimeoutSeconds
    {
        get => requestTimeoutSeconds;
        set => SetAdvancedOption(ref requestTimeoutSeconds, value);
    }

    public double StartupGraceSeconds
    {
        get => startupGraceSeconds;
        set => SetAdvancedOption(ref startupGraceSeconds, value);
    }

    public bool TestAllStrategies
    {
        get => testAllStrategies;
        set
        {
            if (testAllStrategies == value)
            {
                return;
            }

            testAllStrategies = value;
            Notify();
            SaveSetting(nameof(TestAllStrategies), value);
        }
    }

    public double RequestsPerDomain
    {
        get => requestsPerDomain;
        set
        {
            if (requestsPerDomain.Equals(value))
            {
                return;
            }

            requestsPerDomain = value;
            Notify();
            Notify(nameof(AdvancedOptionsValid));
            Notify(nameof(CanStart));
            if (IsValidRequestsPerDomain(value))
            {
                SaveSetting(nameof(RequestsPerDomain), (int)value);
            }
        }
    }

    public bool AdvancedOptionsValid =>
        double.IsFinite(connectTimeoutSeconds) && connectTimeoutSeconds is >= 1d and <= 60d &&
        double.IsFinite(requestTimeoutSeconds) && requestTimeoutSeconds is >= 2d and <= 180d &&
        requestTimeoutSeconds >= connectTimeoutSeconds &&
        double.IsFinite(startupGraceSeconds) && startupGraceSeconds is >= 0.25d and <= 10d &&
        IsValidRequestsPerDomain(requestsPerDomain);

    public bool IsRunning => state is BlockCheckSessionState.Running or BlockCheckSessionState.Canceling;
    public bool CanStart => !IsRunning &&
                            HasTargetSources &&
                            HasProtocolSelection &&
                            HasIpVersionSelection &&
                            AdvancedOptionsValid;
    public bool CanCancel => state == BlockCheckSessionState.Running;
    public bool CanExportReport => lastReport != null;
    public bool CanExportPreset => (lastReport?.Success == true || lastReport?.IsBestEffort == true) &&
                                   !string.IsNullOrWhiteSpace(lastReport.PresetArguments);

    public async Task<BlockCheckRunResult?> StartAsync()
    {
        if (!CanStart)
        {
            return null;
        }

        if (lastReport != null)
        {
            generatedSiteListRoot = CreateGeneratedSiteListRoot();
        }
        ResetResultState();
        remainingTimeEstimator.Reset();
        runStopwatch.Restart();
        try
        {
            BlockCheckSessionPreparationResult prepared = preparationService.Prepare(CreateSessionRequest());
            targets = prepared.Targets;
            issues = prepared.Issues;
            Notify(nameof(Targets));
            Notify(nameof(TargetCount));
            Notify(nameof(Issues));
            if (!prepared.Success)
            {
                SetState(BlockCheckSessionState.InputInvalid);
                return null;
            }

            cancellation = new CancellationTokenSource();
            SetState(BlockCheckSessionState.Running);
            Progress<BlockCheckScanProgress> progress = new(UpdateProgress);
            BlockCheckRunResult runResult = await addOnService.RunWithCdpiuiAdaptersAsync(
                prepared.Catalog,
                prepared.Targets,
                prepared.RunOptions,
                progress,
                strategyRunnerOptions: new CdpiuiZapret2StrategyRunnerOptions
                {
                    StartupGracePeriod = TimeSpan.FromSeconds(startupGraceSeconds),
                },
                probeRunnerOptions: new CurlBlockCheckProbeRunnerOptions
                {
                    ConnectTimeout = TimeSpan.FromSeconds(connectTimeoutSeconds),
                    RequestTimeout = TimeSpan.FromSeconds(requestTimeoutSeconds),
                },
                cancellationToken: cancellation.Token);

            BlockCheckRunResult result = new()
            {
                PreflightIssues = prepared.Issues
                    .Concat(runResult.PreflightIssues)
                    .DistinctBy(issue => (issue.Severity, issue.Code, issue.Message, issue.SubjectId))
                    .ToArray(),
                Scan = runResult.Scan,
                Synthesis = runResult.Synthesis,
                Validation = runResult.Validation,
                WasCanceled = runResult.WasCanceled,
            };

            lastResult = result;
            lastReport = reportBuilder.Build(
                prepared.Catalog,
                selectedRunPreset,
                prepared.Targets,
                result);
            lastSession = new BlockCheckResultSession
            {
                Catalog = prepared.Catalog,
                Targets = prepared.Targets,
                RunOptions = prepared.RunOptions,
                RunResult = result,
                Report = lastReport,
                ConnectTimeout = TimeSpan.FromSeconds(connectTimeoutSeconds),
                RequestTimeout = TimeSpan.FromSeconds(requestTimeoutSeconds),
                StartupGracePeriod = TimeSpan.FromSeconds(startupGraceSeconds),
            };
            issues = lastReport.Issues;
            NotifyResult();
            CompleteDisplayedProgress();
            SetState(result.WasCanceled
                ? BlockCheckSessionState.Canceled
                : result.Success
                    ? BlockCheckSessionState.Completed
                    : result.Synthesis?.IsBestEffort == true
                        ? BlockCheckSessionState.CompletedWithWarnings
                        : BlockCheckSessionState.Failed);
            try
            {
                await reportHistoryService.SaveAsync(lastReport);
            }
            catch (Exception exception)
            {
                historySaveError = exception.Message;
                Notify(nameof(HistorySaveError));
                Logger.Instance.CreateWarningLog(
                    nameof(BlockCheck2ViewModel),
                    $"Cannot save BlockCheck2 report to history: {exception.Message}");
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            SetState(BlockCheckSessionState.Canceled);
            return null;
        }
        catch (Exception exception)
        {
            operationError = exception.Message;
            Notify(nameof(OperationError));
            SetState(BlockCheckSessionState.Failed);
            return null;
        }
        finally
        {
            runStopwatch.Stop();
            estimatedRemainingTime = null;
            estimatedOverallRemainingTime = null;
            Notify(nameof(EstimatedRemainingTime));
            Notify(nameof(EstimatedOverallRemainingTime));
            cancellation?.Dispose();
            cancellation = null;
            CleanupGeneratedSiteListsIfUnviewable();
            NotifyActionState();
        }
    }

    public void Cancel()
    {
        if (!CanCancel || cancellation == null)
        {
            return;
        }

        SetState(BlockCheckSessionState.Canceling);
        cancellation.Cancel();
    }

    public async Task LoadSiteListsAsync()
    {
        if (siteListsLoaded || isSiteListLoading)
        {
            return;
        }

        isSiteListLoading = true;
        siteListLoadError = null;
        Notify(nameof(IsSiteListLoading));
        Notify(nameof(SiteListLoadError));
        try
        {
            List<SiteListElement> items = await SiteListHelper.GetAllAvailableSiteListTemplatesAsync();
            AvailableSiteLists.Clear();
            foreach (SiteListElement item in items
                         .Where(item => !string.IsNullOrWhiteSpace(item.Directory))
                         .Where(item => !IsLegacyGeneratedSiteList(item.Directory!))
                         .OrderBy(item => item.PackName, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var siteListItem = new BlockCheck2SiteListItem(
                    item.Name ?? System.IO.Path.GetFileName(item.Directory!) ?? "site-list.txt",
                    item.PackName ?? string.Empty,
                    item.Directory!,
                    individualSiteListRoot: generatedSiteListRoot);
                if (siteListItem.DomainCount > 0 && !siteListItem.DisplayName.Contains("exclude")) AvailableSiteLists.Add(siteListItem);

            }
            siteListsLoaded = true;
        }
        catch (Exception exception)
        {
            siteListLoadError = exception.Message;
            Notify(nameof(SiteListLoadError));
        }
        finally
        {
            isSiteListLoading = false;
            Notify(nameof(IsSiteListLoading));
        }
    }

    public BlockCheck2SiteListItem AddCustomSiteList(string filePath, string sourceName)
    {
        string fullPath = Path.GetFullPath(filePath);
        BlockCheck2SiteListItem? existing = AvailableSiteLists
            .Concat(SelectedSiteLists)
            .FirstOrDefault(
            item => string.Equals(item.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing;
        }

        BlockCheck2SiteListItem added = new(
            Path.GetFileName(fullPath),
            sourceName,
            fullPath,
            individualSiteListRoot: generatedSiteListRoot);
        return added;
    }

    public async Task<BlockCheck2SiteListItem> UpdateCustomSiteListAsync(
        string content,
        string displayName,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        string normalizedContent = string.Join(
            Environment.NewLine,
            (content ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase));
        if (normalizedContent.Length == 0)
        {
            throw new InvalidDataException("The custom site list is empty.");
        }

        BlockCheck2SiteListItem? item = SelectedSiteLists.FirstOrDefault(candidate => candidate.IsCustom);
        string filePath = item?.FilePath ?? CreateCustomSiteListPath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(
            filePath,
            normalizedContent + Environment.NewLine,
            new UTF8Encoding(false),
            cancellationToken);

        if (item == null)
        {
            item = new BlockCheck2SiteListItem(
                displayName,
                sourceName,
                filePath,
                isCustom: true,
                editableContent: normalizedContent,
                individualSiteListRoot: generatedSiteListRoot);
            SelectedSiteLists.Insert(0, item);
        }
        else
        {
            item.UpdateCustomContent(normalizedContent);
        }

        NotifySiteListSelectionChanged();
        return item;
    }

    public void SelectSiteList(BlockCheck2SiteListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (SelectedSiteLists.Any(selected => string.Equals(
                selected.FilePath,
                item.FilePath,
                StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedSiteLists.Add(item);
        NotifySiteListSelectionChanged();
    }

    public void RemoveSiteList(BlockCheck2SiteListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (SelectedSiteLists.Remove(item))
        {
            NotifySiteListSelectionChanged();
        }
    }

    public void SetSelectedSiteLists(IEnumerable<BlockCheck2SiteListItem> selected)
    {
        SelectedSiteLists.Clear();
        foreach (BlockCheck2SiteListItem item in selected.DistinctBy(
                     item => item.FilePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            SelectedSiteLists.Add(item);
        }
        NotifySiteListSelectionChanged();
    }

    public async Task<BlockCheck2PreparationPreview> BuildPreparationPreviewAsync(
        CancellationToken cancellationToken = default)
    {
        BlockCheckSessionRequest request = CreateSessionRequest();
        double requestTimeout = requestTimeoutSeconds;
        double startupGrace = startupGraceSeconds;
        BlockCheck2PreparationPreview preview = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            BlockCheckSessionPreparationResult prepared = preparationService.Prepare(request);
            BlockCheckMaximumDurationEstimate? estimate = prepared.Success
                ? maximumDurationEstimator.Estimate(
                    prepared.Catalog,
                    prepared.Targets,
                    prepared.RunOptions,
                    TimeSpan.FromSeconds(requestTimeout),
                    TimeSpan.FromSeconds(startupGrace))
                : null;
            return new BlockCheck2PreparationPreview(
                prepared.Targets.Count,
                estimate,
                prepared.Issues);
        }, cancellationToken);

        if (!preview.Success)
        {
            CleanupGeneratedSiteListsIfUnviewable();
        }

        return preview;
    }

    public void CleanupGeneratedSiteListsIfUnviewable()
    {
        if (lastReport == null)
        {
            CleanupGeneratedSiteLists();
        }
    }

    private static string CreateCustomSiteListPath()
    {
        string directory = Path.Combine(
            Directories.StoreLocalUserItemDirectory,
            SharedConstants.LocalUserItemSiteListsFolder,
            "BlockCheck2",
            "Custom");
        return Path.Combine(directory, $"custom_sites_{Guid.NewGuid():N}.txt");
    }

    private void NotifySiteListSelectionChanged()
    {
        Notify(nameof(SelectedSiteListCount));
        Notify(nameof(SelectedDomainCount));
        Notify(nameof(HasTargetSources));
        Notify(nameof(CanStart));
    }

    public void ResetAdvancedOptions()
    {
        ConnectTimeoutSeconds = 5d;
        RequestTimeoutSeconds = 10d;
        StartupGraceSeconds = 0.75d;
        TestAllStrategies = false;
        RequestsPerDomain = BlockCheckRunPresetFactory.GetRecommendedAttemptsPerTarget(selectedRunPreset);
    }

    private void LoadSettings()
    {
        int presetValue = ReadIntSetting(
            nameof(SelectedRunPreset),
            (int)BlockCheckRunPreset.Balanced);
        selectedRunPreset = Enum.IsDefined(typeof(BlockCheckRunPreset), presetValue)
            ? (BlockCheckRunPreset)presetValue
            : BlockCheckRunPreset.Balanced;

        includeHttp = ReadBoolSetting(nameof(IncludeHttp), defaultValue: false);
        includeTls12 = ReadBoolSetting(nameof(IncludeTls12), defaultValue: true);
        includeTls13 = ReadBoolSetting(nameof(IncludeTls13), defaultValue: false);
        includeTlsAuto = ReadBoolSetting(nameof(IncludeTlsAuto), defaultValue: true);
        includeQuic = ReadBoolSetting(nameof(IncludeQuic), defaultValue: false);
        includeIpv4 = ReadBoolSetting(nameof(IncludeIpv4), defaultValue: true);
        includeIpv6 = ReadBoolSetting(nameof(IncludeIpv6), defaultValue: false);

        double savedConnectTimeout = ReadDoubleSetting(nameof(ConnectTimeoutSeconds), 5d);
        connectTimeoutSeconds = double.IsFinite(savedConnectTimeout) && savedConnectTimeout is >= 1d and <= 60d
            ? savedConnectTimeout
            : 5d;

        double savedRequestTimeout = ReadDoubleSetting(nameof(RequestTimeoutSeconds), 10d);
        requestTimeoutSeconds = double.IsFinite(savedRequestTimeout) &&
                                savedRequestTimeout is >= 2d and <= 180d &&
                                savedRequestTimeout >= connectTimeoutSeconds
            ? savedRequestTimeout
            : Math.Max(10d, connectTimeoutSeconds);

        double savedStartupGrace = ReadDoubleSetting(nameof(StartupGraceSeconds), 0.75d);
        startupGraceSeconds = double.IsFinite(savedStartupGrace) && savedStartupGrace is >= 0.25d and <= 10d
            ? savedStartupGrace
            : 0.75d;

        testAllStrategies = ReadBoolSetting(nameof(TestAllStrategies), defaultValue: false);
        int recommendedAttempts = BlockCheckRunPresetFactory.GetRecommendedAttemptsPerTarget(selectedRunPreset);
        int savedAttempts = ReadIntSetting(nameof(RequestsPerDomain), recommendedAttempts);
        requestsPerDomain = IsValidRequestsPerDomain(savedAttempts)
            ? savedAttempts
            : recommendedAttempts;
    }

    public Task SaveReportJsonAsync(string filePath, CancellationToken cancellationToken = default) =>
        reportSerializer.SaveJsonAsync(
            filePath,
            lastReport ?? throw new InvalidOperationException("No BlockCheck2 report is available."),
            cancellationToken);

    public Task SaveReportTextAsync(string filePath, CancellationToken cancellationToken = default) =>
        reportSerializer.SaveTextAsync(
            filePath,
            lastReport ?? throw new InvalidOperationException("No BlockCheck2 report is available."),
            cancellationToken);

    public Task SavePresetArgumentsAsync(string filePath, CancellationToken cancellationToken = default) =>
        reportSerializer.SavePresetArgumentsAsync(
            filePath,
            lastReport ?? throw new InvalidOperationException("No BlockCheck2 report is available."),
            cancellationToken);

    public void Reset()
    {
        if (IsRunning)
        {
            return;
        }

        if (lastReport == null)
        {
            CleanupGeneratedSiteLists();
        }
        generatedSiteListRoot = CreateGeneratedSiteListRoot();
        ResetResultState();
        targets = [];
        Notify(nameof(Targets));
        Notify(nameof(TargetCount));
        SetState(BlockCheckSessionState.Idle);
    }

    private BlockCheckSessionRequest CreateSessionRequest() => new()
    {
        TargetInput = targetInput,
        SiteLists = SelectedSiteLists.Select(item => item.CreateInput(generatedSiteListRoot)).ToArray(),
        RunPreset = selectedRunPreset,
        TestAllStrategies = testAllStrategies,
        AttemptsPerTarget = (int)requestsPerDomain,
        Protocols = SelectedProtocols(),
        IpVersions = SelectedIpVersions(),
    };

    private IReadOnlySet<BlockCheckProtocol> SelectedProtocols()
    {
        HashSet<BlockCheckProtocol> selected = [];
        if (includeHttp) selected.Add(BlockCheckProtocol.Http);
        if (includeTls12) selected.Add(BlockCheckProtocol.Tls12);
        if (includeTls13) selected.Add(BlockCheckProtocol.Tls13);
        if (includeTlsAuto) selected.Add(BlockCheckProtocol.TlsAuto);
        if (includeQuic) selected.Add(BlockCheckProtocol.Quic);
        return selected;
    }

    private IReadOnlySet<BlockCheckIpVersion> SelectedIpVersions()
    {
        HashSet<BlockCheckIpVersion> selected = [];
        if (includeIpv4) selected.Add(BlockCheckIpVersion.IPv4);
        if (includeIpv6) selected.Add(BlockCheckIpVersion.IPv6);
        return selected;
    }

    private void UpdateProgress(BlockCheckScanProgress progress)
    {
        if (!estimatorPhase.HasValue ||
            GetEstimatorStage(estimatorPhase.Value) != GetEstimatorStage(progress.Phase))
        {
            remainingTimeEstimator.Reset();
            runStopwatch.Restart();
            estimatorPhase = progress.Phase;
            lastObservedCheckCount = progress.CompletedChecks;
            lastObservedCheckElapsed = TimeSpan.Zero;
        }

        currentPhase = progress.Phase;
        progressCurrent = progress.CompletedStrategyTargets;
        progressTotal = progress.TotalStrategyTargets;
        currentTargetId = progress.TargetId;
        currentStrategyId = progress.StrategyId;
        successfulChecks = progress.SuccessfulChecks;
        failedChecks = progress.FailedChecks;
        successfulStrategies = progress.SuccessfulStrategies;
        estimatedRemainingTime = remainingTimeEstimator.Update(
            progress.CompletedStrategyTargets,
            progress.TotalStrategyTargets,
            runStopwatch.Elapsed);
        UpdateObservedCheckDuration(progress.CompletedChecks, runStopwatch.Elapsed);
        estimatedOverallRemainingTime = EstimateOverallRemainingTime(progress.Phase);
        Notify(nameof(CurrentPhase));
        Notify(nameof(ProgressCurrent));
        Notify(nameof(ProgressTotal));
        Notify(nameof(ProgressValue));
        Notify(nameof(IsProgressIndeterminate));
        Notify(nameof(CurrentTargetId));
        Notify(nameof(CurrentTargetUrl));
        Notify(nameof(CurrentTargetConnectionDetails));
        Notify(nameof(CurrentTargetDisplay));
        Notify(nameof(CurrentStrategyId));
        Notify(nameof(EstimatedRemainingTime));
        Notify(nameof(EstimatedOverallRemainingTime));
        Notify(nameof(SuccessfulChecks));
        Notify(nameof(FailedChecks));
        Notify(nameof(CompletedChecks));
        Notify(nameof(SuccessfulStrategies));
    }

    private void ResetResultState()
    {
        operationError = null;
        historySaveError = null;
        issues = [];
        lastResult = null;
        lastReport = null;
        lastSession = null;
        currentPhase = null;
        progressCurrent = 0;
        progressTotal = 0;
        estimatedRemainingTime = null;
        estimatedOverallRemainingTime = null;
        estimatorPhase = null;
        lastObservedCheckCount = 0;
        lastObservedCheckElapsed = TimeSpan.Zero;
        secondsPerCheck = null;
        successfulChecks = 0;
        failedChecks = 0;
        successfulStrategies = 0;
        currentTargetId = string.Empty;
        currentStrategyId = string.Empty;
        Notify(nameof(OperationError));
        Notify(nameof(HistorySaveError));
        NotifyResult();
        Notify(nameof(CurrentPhase));
        Notify(nameof(ProgressCurrent));
        Notify(nameof(ProgressTotal));
        Notify(nameof(ProgressValue));
        Notify(nameof(IsProgressIndeterminate));
        Notify(nameof(EstimatedRemainingTime));
        Notify(nameof(EstimatedOverallRemainingTime));
        Notify(nameof(SuccessfulChecks));
        Notify(nameof(FailedChecks));
        Notify(nameof(CompletedChecks));
        Notify(nameof(SuccessfulStrategies));
        Notify(nameof(CurrentTargetId));
        Notify(nameof(CurrentTargetUrl));
        Notify(nameof(CurrentTargetConnectionDetails));
        Notify(nameof(CurrentTargetDisplay));
        Notify(nameof(CurrentStrategyId));
    }

    private void NotifyResult()
    {
        Notify(nameof(Issues));
        Notify(nameof(LastResult));
        Notify(nameof(LastReport));
        Notify(nameof(LastSession));
        Notify(nameof(NoBypassRequired));
        Notify(nameof(LastReportIsHttpOnly));
        Notify(nameof(ReportJson));
        Notify(nameof(ReportText));
        Notify(nameof(PresetArguments));
        Notify(nameof(CanExportReport));
        Notify(nameof(CanExportPreset));
    }

    private void CompleteDisplayedProgress()
    {
        if (progressTotal <= 0)
        {
            return;
        }

        progressCurrent = progressTotal;
        Notify(nameof(ProgressCurrent));
        Notify(nameof(ProgressValue));
    }

    private void SetProtocolSelection(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Notify(propertyName);
        SaveSetting(propertyName, value);
        Notify(nameof(IsHttpOnly));
        Notify(nameof(HasProtocolSelection));
        Notify(nameof(CanContinueFromTargets));
        Notify(nameof(CanStart));
    }

    private void SetIpVersionSelection(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Notify(propertyName);
        SaveSetting(propertyName, value);
        Notify(nameof(HasIpVersionSelection));
        Notify(nameof(CanContinueFromTargets));
        Notify(nameof(CanStart));
    }

    private void SetAdvancedOption(
        ref double field,
        double value,
        [CallerMemberName] string? propertyName = null)
    {
        if (field.Equals(value))
        {
            return;
        }

        field = value;
        Notify(propertyName);
        if (double.IsFinite(value))
        {
            SaveSetting(propertyName, value);
        }
        Notify(nameof(AdvancedOptionsValid));
        Notify(nameof(CanStart));
    }

    private BlockCheckTarget? CurrentTarget() => targets.FirstOrDefault(
        item => string.Equals(item.Id, currentTargetId, StringComparison.Ordinal));

    private void UpdateObservedCheckDuration(int completedChecks, TimeSpan elapsed)
    {
        if (completedChecks <= lastObservedCheckCount)
        {
            return;
        }

        int completedDelta = completedChecks - lastObservedCheckCount;
        double elapsedDelta = Math.Max(0d, (elapsed - lastObservedCheckElapsed).TotalSeconds);
        double sample = elapsedDelta / completedDelta;
        if (sample > 0d)
        {
            secondsPerCheck = secondsPerCheck.HasValue
                ? 0.2d * sample + 0.8d * secondsPerCheck.Value
                : sample;
        }

        lastObservedCheckCount = completedChecks;
        lastObservedCheckElapsed = elapsed;
    }

    private TimeSpan? EstimateOverallRemainingTime(BlockCheckScanPhase phase)
    {
        if (phase == BlockCheckScanPhase.BaselineProbing || !estimatedRemainingTime.HasValue)
        {
            return null;
        }

        if (GetEstimatorStage(phase) != 1 || !secondsPerCheck.HasValue)
        {
            return estimatedRemainingTime;
        }

        int validationAttempts = (int)requestsPerDomain;
        double validationSeconds = targets.Count * validationAttempts * secondsPerCheck.Value +
                                   startupGraceSeconds;
        return estimatedRemainingTime.Value + TimeSpan.FromSeconds(validationSeconds);
    }

    private static int GetEstimatorStage(BlockCheckScanPhase phase) => phase switch
    {
        BlockCheckScanPhase.BaselineProbing => 0,
        BlockCheckScanPhase.StartingStrategy or
            BlockCheckScanPhase.Probing or
            BlockCheckScanPhase.StrategyCompleted => 1,
        BlockCheckScanPhase.ValidatingPreset => 2,
        BlockCheckScanPhase.RepairingPreset => 3,
        _ => 4,
    };

    private void SetState(BlockCheckSessionState value)
    {
        if (state == value)
        {
            return;
        }

        state = value;
        Notify(nameof(State));
        NotifyActionState();
    }

    private void NotifyActionState()
    {
        Notify(nameof(IsRunning));
        Notify(nameof(CanContinueFromTargets));
        Notify(nameof(CanStart));
        Notify(nameof(CanCancel));
        Notify(nameof(CanExportReport));
        Notify(nameof(CanExportPreset));
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static bool IsValidRequestsPerDomain(double value) =>
        double.IsFinite(value) &&
        value is >= MinimumRequestsPerDomain and <= MaximumRequestsPerDomain &&
        Math.Abs(value - Math.Round(value)) < 0.000001d;

    private static int ReadIntSetting(string key, int defaultValue)
    {
        try
        {
            return SettingsManager.Instance.GetValueOrDefault(
                SettingsGroup,
                key,
                defaultValue: defaultValue);
        }
        catch (Exception exception)
        {
            LogSettingsFailure(key, exception);
            return defaultValue;
        }
    }

    private static double ReadDoubleSetting(string key, double defaultValue)
    {
        try
        {
            return SettingsManager.Instance.GetValueOrDefault(
                SettingsGroup,
                key,
                defaultValue: defaultValue);
        }
        catch (Exception exception)
        {
            LogSettingsFailure(key, exception);
            return defaultValue;
        }
    }

    private static bool ReadBoolSetting(string key, bool defaultValue)
    {
        try
        {
            return SettingsManager.Instance.GetValueOrDefault(
                SettingsGroup,
                key,
                defaultValue: defaultValue);
        }
        catch (Exception exception)
        {
            LogSettingsFailure(key, exception);
            return defaultValue;
        }
    }

    private static void SaveSetting<T>(string? key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        try
        {
            SettingsManager.Instance.SetValue(SettingsGroup, key, value);
        }
        catch (Exception exception)
        {
            LogSettingsFailure(key, exception);
        }
    }

    private static void LogSettingsFailure(string key, Exception exception) =>
        Logger.Instance.CreateWarningLog(
            nameof(BlockCheck2ViewModel),
            $"Cannot read or save BlockCheck2 setting '{key}': {exception.Message}");

    private static string CreateGeneratedSiteListRoot() => Path.Combine(
        Directories.DataDirectory,
        "BlockCheck2",
        "GeneratedSiteLists",
        Guid.NewGuid().ToString("N"));

    private static bool IsLegacyGeneratedSiteList(string filePath)
    {
        string legacyRoot = Path.GetFullPath(Path.Combine(
            Directories.StoreLocalUserItemDirectory,
            SharedConstants.LocalUserItemSiteListsFolder,
            "BlockCheck2",
            "PerSite"));
        string fullPath = Path.GetFullPath(filePath);
        return fullPath.StartsWith(
            legacyRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private void CleanupGeneratedSiteLists()
    {
        try
        {
            string generatedBase = Path.GetFullPath(Path.Combine(
                    Directories.DataDirectory,
                    "BlockCheck2",
                    "GeneratedSiteLists"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(generatedSiteListRoot);
            if (!target.StartsWith(generatedBase, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Instance.CreateWarningLog(
                    nameof(BlockCheck2ViewModel),
                    $"Refusing to remove a generated site-list path outside BlockCheck2 storage: {target}");
                return;
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.Instance.CreateWarningLog(
                nameof(BlockCheck2ViewModel),
                $"Cannot remove generated BlockCheck2 site lists: {exception.Message}");
        }
    }
}

public sealed record BlockCheck2PreparationPreview(
    int TargetCount,
    BlockCheckMaximumDurationEstimate? MaximumDuration,
    IReadOnlyList<BlockCheckIssue> Issues)
{
    public bool Success => Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
}

public sealed class BlockCheck2SiteListItem : INotifyPropertyChanged
{
    private int processingModeIndex;
    private string editableContent;
    private int domainCount;

    public BlockCheck2SiteListItem(
        string displayName,
        string sourceName,
        string filePath,
        bool isCustom = false,
        string? editableContent = null,
        string? individualSiteListRoot = null)
    {
        DisplayName = displayName;
        SourceName = sourceName;
        FilePath = Path.GetFullPath(filePath);
        IsCustom = isCustom;
        this.editableContent = editableContent ?? string.Empty;
        domainCount = CountDomains(this.editableContent, FilePath);
        IndividualSiteListRoot = individualSiteListRoot;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName { get; }
    public string SourceName { get; }
    public string FilePath { get; }
    public bool IsCustom { get; }
    public string EditableContent => editableContent;
    public int DomainCount => domainCount;
    private string? IndividualSiteListRoot { get; }

    public int ProcessingModeIndex
    {
        get => processingModeIndex;
        set
        {
            int normalized = value == 1 ? 1 : 0;
            if (processingModeIndex == normalized)
            {
                return;
            }

            processingModeIndex = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessingModeIndex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessingMode)));
        }
    }

    public BlockCheckSiteListProcessingMode ProcessingMode => processingModeIndex == 1
        ? BlockCheckSiteListProcessingMode.EachSite
        : BlockCheckSiteListProcessingMode.WholeList;

    public BlockCheckSiteListInput CreateInput(string? individualSiteListRoot = null) => new(
        DisplayName,
        FilePath,
        ProcessingMode,
        ProcessingMode == BlockCheckSiteListProcessingMode.EachSite
            ? CreateIndividualSiteListDirectory(
                FilePath,
                individualSiteListRoot ?? IndividualSiteListRoot ?? CreateFallbackIndividualSiteListRoot())
            : null);

    public void UpdateCustomContent(string content)
    {
        editableContent = content ?? string.Empty;
        domainCount = CountDomains(editableContent, FilePath);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditableContent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DomainCount)));
    }

    private static int CountDomains(string content, string filePath)
    {
        try
        {
            IEnumerable<string> lines = string.IsNullOrWhiteSpace(content)
                ? File.ReadLines(filePath)
                : content.Split(['\r', '\n']);
            return lines
                .Select(line => (line ?? string.Empty).Trim().TrimStart('\uFEFF'))
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string CreateIndividualSiteListDirectory(string filePath, string rootDirectory)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(filePath).ToUpperInvariant()));
        string id = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
        return Path.Combine(Path.GetFullPath(rootDirectory), id);
    }

    private static string CreateFallbackIndividualSiteListRoot() => Path.Combine(
        Directories.DataDirectory,
        "BlockCheck2",
        "GeneratedSiteLists",
        "Unscoped");
}
