using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Dialogs.BlockCheck2;
using CDPIUI.Core.Data;
using CDPIUI.Core.System;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinUI3Localizer;
using TextDecorations = Windows.UI.Text.TextDecorations;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class MainPage : TemplatePage
{
    private readonly ILocalizer localizer = Localizer.Get();
    private bool handlersConnected;
    private BlockCheck2PreparationPreview? preparationPreview;

    public BlockCheck2ViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        RadioButton selectedModeButton = ViewModel.SelectedRunPreset switch
        {
            BlockCheckRunPreset.Quick => QuickModeRadioButton,
            BlockCheckRunPreset.Exhaustive => ExhaustiveModeRadioButton,
            _ => BalancedModeRadioButton,
        };
        selectedModeButton.IsChecked = true;
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = UtilityButtons;
    }

    public void CancelRunningSession() => ViewModel.Cancel();

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!handlersConnected)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            localizer.LanguageChanged += Localizer_LanguageChanged;
            handlersConnected = true;
        }

        UpdateLocalizedText();
        UpdateNavigationButtons();
        await ViewModel.LoadSiteListsAsync();
        if (!string.IsNullOrWhiteSpace(ViewModel.SiteListLoadError))
        {
            InputInfoBar.Title = Text("BlockCheck2SiteListsLoadFailedTitle");
            InputInfoBar.Message = ViewModel.SiteListLoadError;
            InputInfoBar.IsOpen = true;
        }
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        bool wasRunning = ViewModel.IsRunning;
        CancelRunningSession();
        if (!wasRunning)
        {
            ViewModel.CleanupGeneratedSiteListsIfUnviewable();
        }
        if (!handlersConnected)
        {
            return;
        }

        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        localizer.LanguageChanged -= Localizer_LanguageChanged;
        handlersConnected = false;
    }

    private void Localizer_LanguageChanged(object? sender, LanguageChangedEventArgs e) =>
        UpdateLocalizedText();

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BlockCheck2ViewModel.CurrentPhase) or
            nameof(BlockCheck2ViewModel.ProgressCurrent) or
            nameof(BlockCheck2ViewModel.ProgressTotal) or
            nameof(BlockCheck2ViewModel.EstimatedRemainingTime) or
            nameof(BlockCheck2ViewModel.EstimatedOverallRemainingTime) or
            nameof(BlockCheck2ViewModel.State))
        {
            UpdateProgressText();
        }

        if (e.PropertyName is nameof(BlockCheck2ViewModel.CanStart) or
            nameof(BlockCheck2ViewModel.CanContinueFromTargets) or
            nameof(BlockCheck2ViewModel.CanCancel) or
            nameof(BlockCheck2ViewModel.IsRunning) or
            nameof(BlockCheck2ViewModel.LastReport) or
            nameof(BlockCheck2ViewModel.State))
        {
            UpdateNavigationButtons();
        }
    }

    private void UpdateLocalizedText()
    {
        ProgressViewMoreText.Text = Text(AdditionalProgressInfo.Visibility == Visibility.Visible
            ? "ViewLess"
            : "ViewMore");
        UpdateNavigationButtons();
        UpdateProgressText();
        if (MainContent.SelectedItem == ReviewStep && preparationPreview != null)
        {
            UpdateReviewSummary(preparationPreview);
        }
    }

    private void UpdateProgressText()
    {
        ProgressPhaseTextBlock.Text = ViewModel.CurrentPhase switch
        {
            BlockCheckScanPhase.BaselineProbing => Text("BlockCheck2PhaseBaseline"),
            BlockCheckScanPhase.StartingStrategy => Text("BlockCheck2PhaseStartingStrategy"),
            BlockCheckScanPhase.Probing => Text("BlockCheck2PhaseProbing"),
            BlockCheckScanPhase.StrategyCompleted => Text("BlockCheck2PhaseStrategyCompleted"),
            BlockCheckScanPhase.ValidatingPreset => Text("BlockCheck2PhaseValidatingConfig"),
            BlockCheckScanPhase.RepairingPreset => Text("BlockCheck2PhaseRepairingConfig"),
            _ => Text("BlockCheck2PhasePreparing"),
        };
        ProgressCounterTextBlock.Text = string.Format(
            Text("BlockCheck2ProgressCounterFormat"),
            ViewModel.ProgressCurrent,
            ViewModel.ProgressTotal);
        StageRemainingTimeTextBlock.Text = FormatRemainingTime();
        OverallRemainingTimeTextBlock.Text = FormatOverallRemainingTime();
    }

    private string FormatRemainingTime()
    {
        if (!ViewModel.IsRunning && ViewModel.CurrentPhase.HasValue)
        {
            return "—";
        }

        TimeSpan? remaining = ViewModel.EstimatedRemainingTime;
        if (!remaining.HasValue)
        {
            return Text("BlockCheck2RemainingTimeCalculating");
        }

        if (remaining.Value < TimeSpan.FromMinutes(1))
        {
            return Text("BlockCheck2RemainingTimeUnderMinute");
        }

        int totalMinutes = Math.Max(1, (int)Math.Ceiling(remaining.Value.TotalMinutes));
        if (totalMinutes < 60)
        {
            return string.Format(Text("BlockCheck2RemainingTimeMinutesFormat"), totalMinutes);
        }

        return string.Format(
            Text("BlockCheck2RemainingTimeHoursFormat"),
            totalMinutes / 60,
            totalMinutes % 60);
    }

    private string FormatOverallRemainingTime()
    {
        if (!ViewModel.IsRunning && ViewModel.CurrentPhase.HasValue)
        {
            return "—";
        }

        TimeSpan? remaining = ViewModel.EstimatedOverallRemainingTime;
        if (!remaining.HasValue)
        {
            return Text(ViewModel.CurrentPhase == BlockCheckScanPhase.BaselineProbing
                ? "BlockCheck2OverallTimeCalculating"
                : "BlockCheck2RemainingTimeCalculating");
        }

        if (remaining.Value < TimeSpan.FromMinutes(1))
        {
            return Text("BlockCheck2RemainingTimeUnderMinute");
        }

        int totalMinutes = Math.Max(1, (int)Math.Ceiling(remaining.Value.TotalMinutes));
        if (totalMinutes < 60)
        {
            return string.Format(Text("BlockCheck2RemainingTimeMinutesFormat"), totalMinutes);
        }

        return string.Format(
            Text("BlockCheck2RemainingTimeHoursFormat"),
            totalMinutes / 60,
            totalMinutes % 60);
    }

    private void ProgressViewMoreButton_Click(object sender, RoutedEventArgs e)
    {
        AdditionalProgressInfo.Visibility = AdditionalProgressInfo.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        ProgressViewMoreText.Text = Text(AdditionalProgressInfo.Visibility == Visibility.Visible
            ? "ViewLess"
            : "ViewMore");
        ProgressViewMoreText.TextDecorations = TextDecorations.Underline;
    }

    private void ProgressViewMoreButton_PointerEntered(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        ProgressViewMoreText.TextDecorations = TextDecorations.None;

    private void ProgressViewMoreButton_PointerExited(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        ProgressViewMoreText.TextDecorations = TextDecorations.Underline;

    private void RunModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, QuickModeRadioButton))
        {
            ViewModel.SelectedRunPreset = BlockCheckRunPreset.Quick;
        }
        else if (ReferenceEquals(sender, ExhaustiveModeRadioButton))
        {
            ViewModel.SelectedRunPreset = BlockCheckRunPreset.Exhaustive;
        }
        else
        {
            ViewModel.SelectedRunPreset = BlockCheckRunPreset.Balanced;
        }
    }

    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems.First() is BlockCheck2SiteListItem item)
        {
            ViewModel.SelectSiteList(item);
            SelectSiteListFlyout.Hide();
            InputInfoBar.IsOpen = false;
            UpdateNavigationButtons();

            SiteListBox.SelectedIndex = -1;
        }
    }

    private async void EditCustomSiteListButton_Click(object sender, RoutedEventArgs e)
    {
        SelectSiteListFlyout.Hide();
        await EditCustomSiteListAsync(
            ViewModel.SelectedSiteLists.FirstOrDefault(item => item.IsCustom));
    }

    private async void EditSiteListButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BlockCheck2SiteListItem item })
        {
            return;
        }

        if (item.IsCustom)
        {
            await EditCustomSiteListAsync(item);
            return;
        }

        ShellHelper.OpenFileInDefaultApp(item.FilePath);
    }

    private void RemoveSiteListButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BlockCheck2SiteListItem item })
        {
            ViewModel.RemoveSiteList(item);
            UpdateNavigationButtons();
        }
    }

    private async System.Threading.Tasks.Task EditCustomSiteListAsync(
        BlockCheck2SiteListItem? existing)
    {
        CustomSiteListContentDialog dialog = new(existing?.EditableContent)
        {
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await ViewModel.UpdateCustomSiteListAsync(
                dialog.SiteListContent,
                Text("BlockCheck2CustomSiteListName"),
                Text("BlockCheck2LocalSiteListSource"));
            InputInfoBar.IsOpen = false;
            UpdateNavigationButtons();
        }
        catch (Exception exception)
        {
            ShowInputIssue(exception.Message);
        }
    }

    private async void ChooseSiteListFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = Text("BlockCheck2SiteListFileFilter"),
            Multiselect = false,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (!await TryImportSiteListFileAsync(dialog.FileName, dialog.SafeFileName))
            {
                InputInfoBar.Title = Text("BlockCheck2SiteListInvalidTitle");
                InputInfoBar.Message = Text("BlockCheck2SiteListInvalidMessage");
                InputInfoBar.IsOpen = true;
                return;
            }
        }
        catch (Exception exception)
        {
            InputInfoBar.Title = Text("BlockCheck2SiteListsLoadFailedTitle");
            InputInfoBar.Message = exception.Message;
            InputInfoBar.IsOpen = true;
            return;
        }

        SelectSiteListFlyout.Hide();
        InputInfoBar.IsOpen = false;
        UpdateNavigationButtons();
    }

    private void SiteListDropArea_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.None;
            SiteListDropBorder.Opacity = 0;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = Text("BlockCheck2SiteListDropCaption");
        e.DragUIOverride.IsCaptionVisible = true;
        SiteListDropBorder.Opacity = 1;
        e.Handled = true;
    }

    private void SiteListDropArea_DragLeave(object sender, DragEventArgs e) =>
        SiteListDropBorder.Opacity = 0;

    private async void SiteListDropArea_Drop(object sender, DragEventArgs e)
    {
        SiteListDropBorder.Opacity = 0;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        IReadOnlyList<IStorageItem> droppedItems;
        try
        {
            droppedItems = await e.DataView.GetStorageItemsAsync();
        }
        catch (Exception exception)
        {
            InputInfoBar.Title = Text("BlockCheck2SiteListsLoadFailedTitle");
            InputInfoBar.Message = exception.Message;
            InputInfoBar.IsOpen = true;
            return;
        }
        int imported = 0;
        int skipped = 0;
        List<string> errors = [];
        foreach (IStorageItem droppedItem in droppedItems)
        {
            if (droppedItem is not StorageFile file ||
                !string.Equals(file.FileType, ".txt", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(file.Path))
            {
                skipped++;
                continue;
            }

            try
            {
                if (await TryImportSiteListFileAsync(file.Path, file.Name))
                {
                    imported++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (Exception exception)
            {
                errors.Add($"{file.Name}: {exception.Message}");
            }
        }

        UpdateNavigationButtons();
        if (skipped > 0 || errors.Count > 0)
        {
            InputInfoBar.Title = Text("BlockCheck2SiteListDropPartialTitle");
            InputInfoBar.Message = string.Format(
                Text("BlockCheck2SiteListDropPartialFormat"),
                imported,
                skipped + errors.Count,
                errors.Count == 0
                    ? string.Empty
                    : Environment.NewLine + string.Join(Environment.NewLine, errors.Take(3)));
            InputInfoBar.IsOpen = true;
        }
        else
        {
            InputInfoBar.IsOpen = false;
        }
    }

    private async System.Threading.Tasks.Task<bool> TryImportSiteListFileAsync(
        string filePath,
        string displayName)
    {
        BlockCheckSiteListLoadResult validation = await System.Threading.Tasks.Task.Run(() =>
            new BlockCheckSiteListLoader().Load(
                [new BlockCheckSiteListInput(displayName, filePath)],
                new BlockCheckTargetInputOptions
                {
                    Protocols = new HashSet<BlockCheckProtocol> { BlockCheckProtocol.TlsAuto },
                    IpVersions = new HashSet<BlockCheckIpVersion> { BlockCheckIpVersion.IPv4 },
                }));
        bool valid = validation.Targets.Count > 0 &&
                     validation.Issues.All(issue => issue.Severity != BlockCheckIssueSeverity.Error);
        if (!valid)
        {
            return false;
        }

        string targetFolder = Path.Combine(
            Directories.StoreItemsDirectory,
            SharedConstants.LocalUserItemsId,
            SharedConstants.LocalUserItemSiteListsFolder,
            "BlockCheck2",
            "Imported");
        string sourcePath = Path.GetFullPath(filePath);
        string targetRoot = Path.GetFullPath(targetFolder)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string storedPath = sourcePath.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase)
            ? sourcePath
            : FileSystemService.CopyTxtWithUniqueName(sourcePath, targetFolder);
        BlockCheck2SiteListItem item = ViewModel.AddCustomSiteList(
            storedPath,
            Text("BlockCheck2LocalSiteListSource"));
        ViewModel.SelectSiteList(item);
        return true;
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainContent.SelectedItem == SiteListsStep)
        {
            if (!ViewModel.CanContinueFromTargets)
            {
                ShowInputIssue(Text("BlockCheck2ConnectionSelectionRequired"));
                return;
            }
            if (!ViewModel.HasTargetSources)
            {
                ShowInputIssue(Text("BlockCheck2TargetSourceRequired"));
                return;
            }

            preparationPreview = await ViewModel.BuildPreparationPreviewAsync();
            if (!preparationPreview.Success)
            {
                ShowInputIssue(FormatIssues(preparationPreview.Issues));
                return;
            }

            InputInfoBar.IsOpen = false;
            MainContent.GoTo(ModeStep);
            UpdateNavigationButtons();
            return;
        }

        if (MainContent.SelectedItem == ModeStep)
        {
            MainContent.GoTo(AdvancedStep);
            UpdateNavigationButtons();
            return;
        }
        
        if (MainContent.SelectedItem == AdvancedStep)
        {
            preparationPreview = await ViewModel.BuildPreparationPreviewAsync();
            if (!preparationPreview.Success)
            {
                MainContent.GoTo(SiteListsStep);
                ShowInputIssue(FormatIssues(preparationPreview.Issues));
                UpdateNavigationButtons();
                return;
            }

            UpdateReviewSummary(preparationPreview);
            MainContent.GoTo(ReviewStep);
            UpdateNavigationButtons();
            return;
        }

        if (MainContent.SelectedItem != ReviewStep || !ViewModel.CanStart)
        {
            return;
        }

        MainContent.GoTo(ProgressStep);
        ProgressInfoBar.IsOpen = false;
        UpdateNavigationButtons();

        await ViewModel.StartAsync();
        HandleRunCompleted();
    }

    private void ResetAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetAdvancedOptions();
        UpdateNavigationButtons();
    }

    private void UpdateReviewSummary(BlockCheck2PreparationPreview preview)
    {
        ReviewSourcesTextBlock.Text = string.Format(
            Text("BlockCheck2ReviewSourcesFormat"),
            ViewModel.SelectedSiteListCount,
            ViewModel.SelectedDomainCount,
            preview.TargetCount);

        string[] protocols =
        [
            .. new[]
            {
                ViewModel.IncludeHttp ? "HTTP" : null,
                ViewModel.IncludeTls12 ? "TLS 1.2" : null,
                ViewModel.IncludeTls13 ? "TLS 1.3" : null,
                ViewModel.IncludeTlsAuto ? Text("BlockCheck2TlsAutoShortName") : null,
                ViewModel.IncludeQuic ? "QUIC / HTTP/3" : null,
            }.OfType<string>(),
        ];
        string[] ipVersions =
        [
            .. new[]
            {
                ViewModel.IncludeIpv4 ? "IPv4" : null,
                ViewModel.IncludeIpv6 ? "IPv6" : null,
            }.OfType<string>(),
        ];
        ReviewConnectionsTextBlock.Text = string.Format(
            Text("BlockCheck2ReviewConnectionsFormat"),
            string.Join(", ", protocols),
            string.Join(", ", ipVersions));
        string modeName = ViewModel.SelectedRunPreset switch
        {
            BlockCheckRunPreset.Quick => Text("BlockCheck2QuickModeName"),
            BlockCheckRunPreset.Exhaustive => Text("BlockCheck2ExhaustiveModeName"),
            _ => Text("BlockCheck2BalancedModeName"),
        };
        bool testsAllStrategies = ViewModel.TestAllStrategies ||
                                  ViewModel.SelectedRunPreset == BlockCheckRunPreset.Exhaustive;
        ReviewModeTextBlock.Text = testsAllStrategies
            ? $"{modeName}. {Text("BlockCheck2ReviewAllStrategies")}" 
            : modeName;
        ReviewRequestsTextBlock.Text = string.Format(
            Text("BlockCheck2ReviewRequestsFormat"),
            ViewModel.RequestsPerDomain);
        ReviewTimeoutsTextBlock.Text = string.Format(
            Text("BlockCheck2ReviewTimeoutsFormat"),
            ViewModel.ConnectTimeoutSeconds,
            ViewModel.RequestTimeoutSeconds,
            ViewModel.StartupGraceSeconds);
        ReviewMaximumTimeTextBlock.Text = preview.MaximumDuration == null
            ? Text("BlockCheck2ReviewMaximumTimeUnavailable")
            : string.Format(
                Text("BlockCheck2ReviewMaximumTimeFormat"),
                FormatDuration(preview.MaximumDuration.Duration),
                preview.MaximumDuration.EstimatedStrategyJobs,
                preview.MaximumDuration.EstimatedRequests);
    }

    private string FormatDuration(TimeSpan duration)
    {
        int totalMinutes = Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes));
        int days = totalMinutes / (24 * 60);
        int hours = totalMinutes / 60 % 24;
        int minutes = totalMinutes % 60;
        if (days > 0)
        {
            return string.Format(Text("BlockCheck2DurationDaysFormat"), days, hours, minutes);
        }
        if (hours > 0)
        {
            return string.Format(Text("BlockCheck2DurationHoursFormat"), hours, minutes);
        }
        return string.Format(Text("BlockCheck2DurationMinutesFormat"), minutes);
    }

    private void HandleRunCompleted()
    {
        if (ViewModel.State == BlockCheckSessionState.InputInvalid)
        {
            MainContent.GoTo(SiteListsStep);
            ShowInputIssue(FormatIssues());
            UpdateNavigationButtons();
            return;
        }

        MainContent.GoTo(WorkComplete);

        ProgressInfoBar.IsOpen = true;
        if (ViewModel.State == BlockCheckSessionState.Completed && ViewModel.NoBypassRequired)
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Informational;
            ProgressInfoBar.Title = Text("BlockCheck2NoBypassRequiredTitle");
            ProgressInfoBar.Message = Text(ViewModel.LastReportIsHttpOnly
                ? "BlockCheck2HttpOnlyNoBypassRequiredMessage"
                : "BlockCheck2NoBypassRequiredMessage");
        }
        else if (ViewModel.State == BlockCheckSessionState.Completed)
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Success;
            ProgressInfoBar.Title = Text("BlockCheck2CompletedTitle");
            ProgressInfoBar.Message = Text("BlockCheck2CompletedMessage");
        }
        else if (ViewModel.State == BlockCheckSessionState.CompletedWithWarnings)
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Warning;
            ProgressInfoBar.Title = Text("BlockCheck2BestEffortCompletedTitle");
            ProgressInfoBar.Message = Text("BlockCheck2BestEffortCompletedMessage");
        }
        else if (ViewModel.State == BlockCheckSessionState.Canceled)
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Informational;
            ProgressInfoBar.Title = Text("BlockCheck2CanceledTitle");
            ProgressInfoBar.Message = Text("BlockCheck2CanceledMessage");
        }
        else
        {
            ProgressInfoBar.Severity = InfoBarSeverity.Error;
            ProgressInfoBar.Title = Text("BlockCheck2FailedTitle");
            ProgressInfoBar.Message = string.IsNullOrWhiteSpace(ViewModel.OperationError)
                ? FormatIssues()
                : ViewModel.OperationError;
        }

        if (!string.IsNullOrWhiteSpace(ViewModel.HistorySaveError))
        {
            ProgressInfoBar.Message = string.Concat(
                ProgressInfoBar.Message,
                Environment.NewLine,
                Text("BlockCheck2HistorySaveFailedMessage"),
                " ",
                ViewModel.HistorySaveError);
        }

        UpdateNavigationButtons();
    }

    private void ShowInputIssue(string message)
    {
        InputInfoBar.Title = Text("BlockCheck2InputIssueTitle");
        InputInfoBar.Message = message;
        InputInfoBar.IsOpen = true;
    }

    private string FormatIssues() => FormatIssues(ViewModel.Issues);

    private string FormatIssues(IEnumerable<BlockCheckIssue> sourceIssues)
    {
        string[] messages = sourceIssues
            .Select(issue => issue.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.CurrentCulture)
            .Take(4)
            .ToArray();
        return messages.Length == 0
            ? Text("BlockCheck2UnknownFailure")
            : string.Join(Environment.NewLine, messages);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsRunning)
        {
            return;
        }

        if (MainContent.SelectedItem == SiteListsStep)
        {
            Debug.WriteLine(Frame.BackStackDepth);
            if (Frame.CanGoBack)
            {
                PrepareToConnectedBackwardAnimate(UtilityButtons);
                Frame.GoBack();
            }
            else
            {
                CreateConfigUtilWindow.Instance.Close();
            }
            return;
        }
        else if (MainContent.SelectedItem == ModeStep)
        {
            MainContent.GoTo(SiteListsStep);
        }
        else if (MainContent.SelectedItem == AdvancedStep)
        {
            MainContent.GoTo(ModeStep);
        }
        else if (MainContent.SelectedItem == ReviewStep)
        {
            MainContent.GoTo(AdvancedStep);
        }
        else if (MainContent.SelectedItem == ProgressStep || MainContent.SelectedItem == WorkComplete)
        {
            ViewModel.Reset();
            ProgressInfoBar.IsOpen = false;
            MainContent.GoTo(SiteListsStep);
        }

        UpdateNavigationButtons();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Cancel();
        CancelButton.IsEnabled = false;
    }

    private async void ViewResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LastReport == null)
        {
            return;
        }

        BlockCheck2ResultWindow window = await ((App)Application.Current)
            .SafeCreateNewWindow<BlockCheck2ResultWindow>();
        if (ViewModel.LastSession != null)
        {
            window.ShowSession(ViewModel.LastSession);
        }
        else
        {
            window.ShowReport(ViewModel.LastReport);
        }

        CreateConfigUtilWindow.Instance?.Close();
    }

    private void UpdateNavigationButtons()
    {
        bool running = ViewModel.IsRunning;
        bool onSiteLists = MainContent.SelectedItem == null || MainContent.SelectedItem == SiteListsStep;
        bool onMode = MainContent.SelectedItem == ModeStep;
        bool onAdvanced = MainContent.SelectedItem == AdvancedStep;
        bool onReview = MainContent.SelectedItem == ReviewStep;
        bool onProgress = MainContent.SelectedItem == ProgressStep;
        bool onComplete = MainContent.SelectedItem == WorkComplete;

        UtilityButtons.SetButtonVisibilities(
            (ExitButton, onComplete ? Visibility.Visible : Visibility.Collapsed),
            (BackButton, !running ? Visibility.Visible : Visibility.Collapsed),
            (CancelButton, running ? Visibility.Visible : Visibility.Collapsed),
            (ViewResultButton, !running && onComplete && ViewModel.LastReport != null
                ? Visibility.Visible
                : Visibility.Collapsed),
            (NextButton, !running && !onProgress && !onComplete
                ? Visibility.Visible
                : Visibility.Collapsed));

        CancelButton.IsEnabled = ViewModel.CanCancel;
        NextButton.IsEnabled = onSiteLists
            ? ViewModel.HasTargetSources 
            : onAdvanced
                ? ViewModel.AdvancedOptionsValid && ViewModel.CanContinueFromTargets
                : onMode || (onReview && ViewModel.CanStart);
        NextButton.Content = onReview
            ? Text("BlockCheck2StartButton")
            : Text("BlockCheck2NextButton");
    }

    private string Text(string key) => localizer.GetLocalizedString(key);

    private void RadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is RadioButton radioButton)
        {
            radioButton.IsChecked = true;
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        CreateConfigUtilWindow.Instance.Close();
    }
}
