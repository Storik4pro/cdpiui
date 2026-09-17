using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.Helper.UserExperience;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class ResultPage : TemplatePage, IStatusNotificationSource, INotifyPropertyChanged
{
    private readonly ILocalizer localizer = Localizer.Get();
    private readonly BlockCheckReportSerializer reportSerializer = new();
    private bool languageHandlerConnected;
    private bool strategyTestRunning;

    public BlockCheck2ResultViewModel ViewModel { get; private set; } = new();
    public event EventHandler<StatusNotificationRequestedEventArgs>? StatusNotificationRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ConfigMakerUserControl PresetPreviewMenuTarget => PresetPreviewEditor;

    public bool CanOpenPresetReview => ViewModel.CanUseConfig;
    public bool CanCopySelectedStrategy => SelectedStrategy != null;
    public bool CanTestSelectedStrategy => TestStrategyButton.IsEnabled;
    public bool CanToggleComponentManualTest => ComponentManualTestButton.IsEnabled;
    public bool IsComponentManualTestRunning => isManualTestRunning;
    public bool CanAddSelectedStrategyForSite => AddForSiteButton.IsEnabled;
    public bool CanAddSelectedStrategyForList => AddForListButton.IsEnabled;
    public bool IsStrategyOverviewViewActive => !StrategySemanticZoom.IsZoomedInViewActive;
    public bool IsStrategyDetailsViewActive => StrategySemanticZoom.IsZoomedInViewActive;
    public bool CanSavePresetPreviewText => ViewModel.CanUseConfig;
    public bool IsPresetPreviewCommandPanelVisible => PresetPreviewEditor.IsCommandPanelVisible;
    public bool IsPresetPreviewBottomPanelVisible => PresetPreviewEditor.IsBottomPanelVisible;
    public bool IsPresetPreviewFilesPanelVisible => PresetPreviewEditor.IsPresetFilesPanelVisible;
    public bool HasPresetPreviewFiles => PresetPreviewEditor.HasPresetFiles;
    public bool HasPresetPreviewGroups => PresetPreviewEditor.HasPresetGroups;
    public bool CanStartPresetPreviewTest => !PresetPreviewEditor.IsTesting;
    public bool CanStopPresetPreviewTest => PresetPreviewEditor.IsTesting;

    public ResultPage()
    {
        InitializeComponent();
        DataContext = this;
        PresetPreviewEditor.ComponentId = HardcodedItemIds.ComponentIds[Components.Zapret2];
        PresetPreviewEditor.SetCommandPanelVisible(false);
        PresetPreviewEditor.SetBottomPanelVisible(false);
        PresetPreviewEditor.UseInlineStatusMessages = false;
        PresetPreviewEditor.StatusNotificationRequested += PresetPreviewEditor_StatusNotificationRequested;
        PresetPreviewEditor.TestStateChanged += PresetPreviewEditor_MenuStateChanged;
        PresetPreviewEditor.EditorReadOnlyChanged += PresetPreviewEditor_MenuStateChanged;
        PresetPreviewEditor.PanelStateChanged += PresetPreviewEditor_MenuStateChanged;
        BindViewModelCollections();
        Loaded += ResultPage_Loaded;
        Unloaded += ResultPage_Unloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        switch (e.Parameter)
        {
            case BlockCheck2ResultViewModel viewModel:
                LoadViewModel(viewModel);
                break;
            case BlockCheckResultSession session:
                LoadSession(session);
                break;
            case BlockCheckReport report:
                LoadReport(report);
                break;
        }
    }

    public void LoadReport(BlockCheckReport report)
    {
        ViewModel.Load(report);
        UpdateView();
    }

    public void LoadViewModel(BlockCheck2ResultViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        BindViewModelCollections();
        UpdateView();
        if (ViewModel.StrategyEvidence.Count > 0 && StrategyListView.SelectedIndex < 0)
        {
            StrategyListView.SelectedIndex = 0;
        }
    }

    public void LoadSession(BlockCheckResultSession session)
    {
        ViewModel.Load(session);
        UpdateView();
        if (ViewModel.StrategyEvidence.Count > 0)
        {
            StrategyListView.SelectedIndex = 0;
        }
    }

    private void ResultPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!languageHandlerConnected)
        {
            localizer.LanguageChanged += Localizer_LanguageChanged;
            languageHandlerConnected = true;
        }
        UpdateView();
    }

    private void ResultPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!languageHandlerConnected)
        {
            return;
        }
        localizer.LanguageChanged -= Localizer_LanguageChanged;
        languageHandlerConnected = false;
    }

    private void Localizer_LanguageChanged(object? sender, LanguageChangedEventArgs e) => UpdateView();

    private void BindViewModelCollections()
    {
        GroupedStrategiesSource.Source = ViewModel.StrategyGroups;
        ManualAssignmentsListView.ItemsSource = ViewModel.ManualAssignments;
        ManualIssuesListView.ItemsSource = ViewModel.ManualIssues;
        IssuesListView.ItemsSource = ViewModel.Issues;
        ProbesListView.ItemsSource = ViewModel.Probes;
    }

    private void UpdateView()
    {
        if (!ViewModel.HasReport)
        {
            ShowActionMessage(InfoBarSeverity.Error, Text("BlockCheck2ResultEmptyTitle"), Text("BlockCheck2ResultEmptyMessage"));
        }
        else
        {
            var severity = ViewModel.NoBypassRequired
                ? InfoBarSeverity.Informational
                : ViewModel.Success
                    ? InfoBarSeverity.Success
                    : InfoBarSeverity.Warning;
            var title = ViewModel.NoBypassRequired
                ? Text("BlockCheck2NoBypassRequiredTitle")
                : ViewModel.Success
                    ? Text("BlockCheck2ResultSuccessTitle")
                    : ViewModel.IsBestEffort
                        ? Text("BlockCheck2ResultBestEffortTitle")
                        : Text("BlockCheck2ResultPartialTitle");
            var message = ViewModel.NoBypassRequired
                ? Text(ViewModel.IsHttpOnly
                    ? "BlockCheck2HttpOnlyNoBypassRequiredMessage"
                    : "BlockCheck2NoBypassRequiredMessage")
                : ViewModel.Success
                    ? Text("BlockCheck2ResultSuccessMessage")
                    : ViewModel.IsBestEffort
                        ? Text("BlockCheck2ResultBestEffortMessage")
                        : Text("BlockCheck2ResultPartialMessage");

            ShowActionMessage(severity, title, message);
        }

        bool hasEvidence = ViewModel.StrategyEvidence.Count > 0;
        NoStrategiesPanel.Visibility = hasEvidence ? Visibility.Collapsed : Visibility.Visible;
        StrategySemanticZoom.Visibility = hasEvidence ? Visibility.Visible : Visibility.Collapsed;
        StrategyDetailsGrid.IsHitTestVisible = StrategyListView.SelectedItem != null && ViewModel.HasSession;
        NoIssuesTextBlock.Visibility = ViewModel.IssueCount == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        IssuesListView.Visibility = ViewModel.IssueCount == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        UpdateStrategyCount();
        UpdateAssignmentView();
        UpdateActionButtons();
        UpdateSelectedStrategy();
    }

    private void StrategySearchBox_TextChanged(
        AutoSuggestBox sender,
        AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ApplyStrategyView();
        }
    }

    private void StrategyViewOption_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyStrategyView();

    private void ApplyStrategyView()
    {
        if (StrategyListView == null || StrategyCountTextBlock == null || NoStrategiesPanel == null)
        {
            return;
        }

        ViewModel.ApplyStrategyView(
            StrategySearchBox?.Text,
            Math.Max(0, StrategySortComboBox?.SelectedIndex ?? 0),
            Math.Max(0, StrategyFilterComboBox?.SelectedIndex ?? 0));
        UpdateStrategyCount();
        if (ViewModel.StrategyEvidence.Count > 0)
        {
            StrategyListView.SelectedIndex = 0;
        }
        else
        {
            UpdateSelectedStrategy();
        }
        NoStrategiesPanel.Visibility = ViewModel.StrategyEvidence.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrategySemanticZoom.Visibility = ViewModel.StrategyEvidence.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void StrategiesPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (StrategyTableBorder == null || StrategyToolbar == null || e.NewSize.Height <= 0)
        {
            return;
        }

        const double minimumTableHeight = 180;
        const double minimumDetailsHeight = 150;
        const double splitterHeight = 16;
        double maximumTableHeight = Math.Max(
            minimumTableHeight,
            e.NewSize.Height - StrategyToolbar.ActualHeight - 8 - splitterHeight - minimumDetailsHeight);
        StrategyTableBorder.MaxHeight = maximumTableHeight;
        if (StrategyTableBorder.Height > maximumTableHeight)
        {
            StrategyTableBorder.Height = maximumTableHeight;
        }
        else
        {
            StrategyTableBorder.Height = maximumTableHeight - 100;
        }
    }

    private void StrategyGroupHeader_Click(object sender, RoutedEventArgs e)
    {
        StrategySemanticZoom.IsZoomedInViewActive = false;
        RaiseMenuStateChanged();
    }

    private void SitesOverviewListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ICollectionViewGroup collectionGroup ||
            collectionGroup.Group is not BlockCheck2SiteStrategyGroup siteGroup ||
            siteGroup.Count == 0)
        {
            return;
        }

        BlockCheck2StrategyEvidenceItem firstItem = siteGroup[0];
        StrategySemanticZoom.IsZoomedInViewActive = true;
        RaiseMenuStateChanged();
        DispatcherQueue.TryEnqueue(() =>
        {
            StrategyListView.SelectedItem = firstItem;
            StrategyListView.ScrollIntoView(firstItem, ScrollIntoViewAlignment.Leading);
        });
    }

    private void StrategyListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateSelectedStrategy();

    private void UpdateSelectedStrategy()
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        bool selected = item != null && ViewModel.HasSession;
        StrategyDetailsGrid.IsHitTestVisible = selected && !strategyTestRunning;
        TestStrategyButton.IsEnabled = selected && !strategyTestRunning;
        ComponentManualTestButton.IsEnabled = selected && (!strategyTestRunning || isManualTestRunning);
        ComponentManualTestButton.IsChecked = selected && isManualTestRunning;

        ComponentManualTestButton.Label = ComponentManualTestButton.IsChecked == true
            ? Text("StopTest")
            : Text("PresetTestConfirmButton");
        TestGlyph.Glyph = ComponentManualTestButton.IsChecked == true
            ? "\uE769"
            : "\uE768";

        AddForSiteButton.IsEnabled = selected && !strategyTestRunning;
        AddForListButton.IsEnabled = selected &&
            ViewModel.CanAddForSiteList(item!) &&
            !strategyTestRunning;
        RaiseMenuStateChanged();
        if (item == null)
        {
            SelectedStrategyNameTextBlock.Text = Text("BlockCheck2SelectStrategyText");
            SelectedStrategyIdTextBlock.Text = string.Empty;
            SelectedStrategyArgumentsTextBox.Text = string.Empty;
            SelectedSiteTextBlock.Text = string.Empty;
            SelectedConnectionTextBlock.Text = string.Empty;
            SelectedEvidenceTextBlock.Text = string.Empty;
            SelectedBaselineTextBlock.Text = string.Empty;
            return;
        }

        SelectedStrategyNameTextBlock.Text = item.StrategyName;
        SelectedStrategyIdTextBlock.Text = item.StrategyId;
        SelectedStrategyArgumentsTextBox.Text = item.StrategyArguments;
        SelectedSiteTextBlock.Text = item.SiteUrl;
        SelectedConnectionTextBlock.Text = item.ConnectionDetails;
        SelectedEvidenceTextBlock.Text = item.EvidenceDetails;
        SelectedBaselineTextBlock.Text = string.Format(
            Text("BlockCheck2BaselineEvidenceFormat"),
            item.BaselineText);
    }

    private async void TestStrategyButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null || strategyTestRunning)
        {
            return;
        }

        strategyTestRunning = true;
        StrategyTestProgressRing.Visibility = Visibility.Visible;
        UpdateSelectedStrategy();
        try
        {
            BlockCheckManualStrategyTestResult result = await ViewModel.TestStrategyAsync(item);
            UpdateSelectedStrategy();
            if (result.ProbeResult == null)
            {
                ShowActionError(result.Issues.FirstOrDefault()?.Message ??
                    Text("BlockCheck2StrategyTestFailedMessage"));
                return;
            }

            ProbeSummary summary = ProbeSummary.FromAttempts(result.ProbeResult.Attempts);
            string message = string.Format(
                Text("BlockCheck2StrategyTestResultFormat"),
                summary.SuccessCount,
                summary.AttemptCount,
                summary.SuccessRate);
            if (result.Issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
            {
                ShowActionError(result.Issues.First(issue =>
                    issue.Severity == BlockCheckIssueSeverity.Error).Message);
            }
            else
            {
                ShowActionMessage(
                    summary.SuccessCount == summary.AttemptCount
                        ? InfoBarSeverity.Success
                        : InfoBarSeverity.Warning,
                    Text("BlockCheck2StrategyTestCompletedTitle"),
                    message);
            }
        }
        catch (Exception exception)
        {
            ShowActionError(exception.Message);
        }
        finally
        {
            strategyTestRunning = false;
            StrategyTestProgressRing.Visibility = Visibility.Collapsed;
            UpdateSelectedStrategy();
            UpdateAssignmentView();
        }
    }

    private async void ComponentManualTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (ComponentManualTestButton.IsChecked == true)
        {
            await StartTestAsync();
        }
        else
        {
            await StopTestAsync(true);
        }
    }

    #region ManualTesting

    private bool isStartingTest = false;
    private bool isManualTestRunning = false;
    private bool restoreComponentAfterTest = false;

    public async Task StopTestAsync(bool showCompletionMessage = false)
    {
        UpdateSelectedStrategy();
        if (!isManualTestRunning)
        {
            return;
        }

        bool restore = restoreComponentAfterTest;
        isManualTestRunning = false;
        isStartingTest = false;
        ComponentTasksManager.Instance.TaskStateUpdated -= ComponentTasksManager_TaskStateUpdated;
        

        if (!string.IsNullOrWhiteSpace(HardcodedItemIds.ComponentIds[Components.Zapret2]))
        {
            await ComponentTasksManager.Instance.StopTask(HardcodedItemIds.ComponentIds[Components.Zapret2]);
            if (restore)
            {
                await ComponentTasksManager.Instance.CreateAndRunNewTask(HardcodedItemIds.ComponentIds[Components.Zapret2]);
            }
        }

        restoreComponentAfterTest = false;
        if (showCompletionMessage)
        {
            ShowActionMessage(
                InfoBarSeverity.Informational,
                Text("ManualTesting"),
                Text("ConfigMakerTestStoppedMessage"));
        }

        UpdateSelectedStrategy();
        UpdateAssignmentView();
    }

    public async Task StartTestAsync()
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null || isManualTestRunning || strategyTestRunning)
        {
            return;
        }

        UpdateSelectedStrategy();

        var result = ViewModel.GetFullStrategyArguments(item);

        if (!result.Success || string.IsNullOrEmpty(result.Result))
        {
            ShowActionMessage(
                InfoBarSeverity.Warning,
                Text("ManualTesting"),
                result.ErrorHappens? result.Error.ErrorCode : Text("ConfigMakerEmptyArgumentsMessage"));
            return;
        }

        await StopTestAsync();
        bool componentWasRunning = await ComponentTasksManager.Instance.IsTaskRunned(HardcodedItemIds.ComponentIds[Components.Zapret2]);
        if (componentWasRunning)
        {
            ContentDialog confirmation = new()
            {
                XamlRoot = XamlRoot,
                Title = localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentTitle"),
                Content = localizer.GetLocalizedString("ConfigMakerReplaceRunningComponentMessage"),
                PrimaryButtonText = localizer.GetLocalizedString("Continue"),
                CloseButtonText = localizer.GetLocalizedString("Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        restoreComponentAfterTest = componentWasRunning;
        await ComponentTasksManager.Instance.StopTask(HardcodedItemIds.ComponentIds[Components.Zapret2]);

        isManualTestRunning = true;
        isStartingTest = true;
        ComponentTasksManager.Instance.TaskStateUpdated += ComponentTasksManager_TaskStateUpdated;
        UpdateSelectedStrategy();

        ShowActionMessage(
            InfoBarSeverity.Informational,
            Text("ManualTesting"),
            Text("ConfigMakerTestStartedMessage"));

        try
        {
            await ComponentTasksManager.Instance.CreateAndRunNewTask(HardcodedItemIds.ComponentIds[Components.Zapret2], result.Result);
            isStartingTest = false;
        }
        catch (Exception exception)
        {
            isStartingTest = false;
            ShowActionMessage(
                InfoBarSeverity.Error,
                Text("ManualTesting"),
                string.Format(
                    Text("ConfigMakerTestStartFailedMessage"),
                    exception.Message));
            await StopTestAsync();
        }
    }

    private void ComponentTasksManager_TaskStateUpdated(Tuple<string, bool> state)
    {
        if (!isManualTestRunning || isStartingTest ||
            !string.Equals(state.Item1, HardcodedItemIds.ComponentIds[Components.Zapret2], StringComparison.OrdinalIgnoreCase) ||
            state.Item2)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(async () =>
        {
            ShowActionMessage(
                InfoBarSeverity.Warning,
                Text("ManualTesting"),
                localizer.GetLocalizedString("ConfigMakerTestExitedMessage"));
            await StopTestAsync();
        });
    }
    #endregion

    private void AddForSiteButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null)
        {
            return;
        }
        ShowAssignmentResult(ViewModel.AddForSite(item));
    }

    private async void AddForListButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null)
        {
            return;
        }
        string[] paths = ViewModel.GetSiteListPaths(item).ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        string selectedPath = paths[0];
        if (paths.Length > 1)
        {
            ComboBox selector = new()
            {
                ItemsSource = paths,
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = Text("BlockCheck2ChooseSiteListTitle"),
                Content = selector,
                PrimaryButtonText = Text("BlockCheck2AddButtonText"),
                CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || selector.SelectedItem is not string path)
            {
                return;
            }
            selectedPath = path;
        }
        ShowAssignmentResult(ViewModel.AddForSiteList(item, selectedPath));
    }

    private void ShowAssignmentResult(bool added)
    {
        if (added)
        {
            ShowActionMessage(
                InfoBarSeverity.Success,
                Text("BlockCheck2AssignmentAddedTitle"),
                Text("BlockCheck2AssignmentAddedMessage"));
        }
        else
        {
            ShowActionMessage(
                InfoBarSeverity.Warning,
                Text("BlockCheck2AssignmentNotAddedTitle"),
                Text("BlockCheck2AssignmentDuplicateMessage"));
        }
        UpdateAssignmentView();
        UpdateActionButtons();
    }

    private void ManualAssignmentsListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateAssignmentButtons();

    private void MoveAssignmentUpButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2ManualAssignmentItem? selected = SelectedAssignment;
        if (selected != null && ViewModel.MoveAssignment(selected, -1))
        {
            UpdateAssignmentView();
            ManualAssignmentsListView.SelectedItem = selected;
        }
    }

    private void MoveAssignmentDownButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2ManualAssignmentItem? selected = SelectedAssignment;
        if (selected != null && ViewModel.MoveAssignment(selected, 1))
        {
            UpdateAssignmentView();
            ManualAssignmentsListView.SelectedItem = selected;
        }
    }

    private void RemoveAssignmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAssignment == null)
        {
            return;
        }
        ViewModel.RemoveAssignment(SelectedAssignment);
        UpdateAssignmentView();
    }

    private void ClearAssignmentsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearManualAssignments();
        UpdateAssignmentView();
    }

    private void ManualIssueQuickFixButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            element.DataContext is not BlockCheck2ManualIssueItem issue ||
            !ViewModel.ApplyManualIssueQuickFix(issue))
        {
            return;
        }

        ShowActionMessage(
            InfoBarSeverity.Success,
            Text("BlockCheck2ManualIssueQuickFixCompletedTitle"),
            Text("BlockCheck2ManualScopeOverlapAcceptedMessage"));
        UpdateAssignmentView();
    }

    private void UpdateAssignmentView()
    {
        bool hasAssignments = ViewModel.ManualAssignments.Count > 0;
        EmptyAssignmentsPanel.Visibility = hasAssignments ? Visibility.Collapsed : Visibility.Visible;
        ManualAssignmentsListView.Visibility = hasAssignments ? Visibility.Visible : Visibility.Collapsed;
        ManualIssuesListView.Visibility = ViewModel.ManualIssues.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        NoManualIssuesTextBlock.Visibility = ViewModel.ManualIssues.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        PresetPreviewEditor.CommandText = ComponentCommandLineFormatter.FormatByFlags(
            ViewModel.EffectivePresetArguments);
        UpdateAssignmentButtons();
        UpdateActionButtons();
    }

    private void UpdateAssignmentButtons()
    {
        int index = ManualAssignmentsListView.SelectedIndex;
        MoveAssignmentUpButton.IsEnabled = index > 0;
        MoveAssignmentDownButton.IsEnabled = index >= 0 && index < ViewModel.ManualAssignments.Count - 1;
        RemoveAssignmentButton.IsEnabled = index >= 0;
    }

    private void UpdateStrategyCount()
    {
        StrategyCountTextBlock.Text = string.Format(
            Text("BlockCheck2StrategyCountFormat"),
            ViewModel.VisibleStrategyEvidenceCount,
            ViewModel.StrategyEvidenceCount);
    }

    private void UpdateActionButtons()
    {
        ReviewPresetButton.IsEnabled = ViewModel.CanUseConfig;
        RaiseMenuStateChanged();
    }

    public void NavigateToReview()
    {
        if (ViewModel.CanUseConfig)
        {
            Frame.Navigate(typeof(PresetReviewPage), ViewModel);
        }
    }

    public void OpenPresetBuilder() => ResultSelector.SelectIndex(1);

    public void SaveJsonReportFromMenu() => SaveJsonReportButton_Click(this, new RoutedEventArgs());

    public void SaveTextReportFromMenu() => SaveTextReportButton_Click(this, new RoutedEventArgs());

    private void ReviewPresetButton_Click(object sender, RoutedEventArgs e) => NavigateToReview();

    private void ShowStrategyOverviewMenuItem_Click(object sender, RoutedEventArgs e)
    {
        StrategySemanticZoom.IsZoomedInViewActive = false;
        RaiseMenuStateChanged();
    }

    private void ShowStrategyDetailsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        StrategySemanticZoom.IsZoomedInViewActive = true;
        RaiseMenuStateChanged();
    }

    private async void ComponentManualTestMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (isManualTestRunning)
        {
            await StopTestAsync(showCompletionMessage: true);
        }
        else
        {
            await StartTestAsync();
        }
    }

    private async void PresetPreviewStartTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await PresetPreviewEditor.StartTestAsync();

    private async void PresetPreviewStopTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await PresetPreviewEditor.StopTestAsync(showCompletionMessage: true);


    private async void SaveJsonReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Report == null)
        {
            return;
        }
        SaveFileDialog dialog = CreateSaveDialog(
            Text("BlockCheck2SaveReportDialogTitle"),
            "blockcheck2-report.json",
            Text("BlockCheck2JsonFileFilter"));
        if (dialog.ShowDialog() == true)
        {
            await SaveAsync(() => reportSerializer.SaveJsonAsync(dialog.FileName, ViewModel.Report));
        }
    }

    private async void SaveTextReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Report == null)
        {
            return;
        }
        SaveFileDialog dialog = CreateSaveDialog(
            Text("BlockCheck2SaveReportDialogTitle"),
            "blockcheck2-report.txt",
            Text("BlockCheck2TextFileFilter"));
        if (dialog.ShowDialog() == true)
        {
            await SaveAsync(() => reportSerializer.SaveTextAsync(dialog.FileName, ViewModel.Report));
        }
    }

    private async Task SaveAsync(Func<Task> saveAction)
    {
        try
        {
            await saveAction();
            ShowActionMessage(
                InfoBarSeverity.Success,
                Text("BlockCheck2ActionCompletedTitle"),
                Text("BlockCheck2FileSavedMessage"));
        }
        catch (Exception exception)
        {
            ShowActionError(exception.Message);
        }
    }

    private static SaveFileDialog CreateSaveDialog(string title, string fileName, string filter) => new()
    {
        Title = title,
        FileName = fileName,
        Filter = filter,
        OverwritePrompt = true,
        RestoreDirectory = true,
    };

    private void ShowActionError(string message) => ShowActionMessage(
        InfoBarSeverity.Error,
        Text("BlockCheck2ActionFailedTitle"),
        message);

    private void ShowActionMessage(InfoBarSeverity severity, string title, string message)
    {
        StatusNotificationRequested?.Invoke(
            this,
            new StatusNotificationRequestedEventArgs(severity, title, message));
    }

    private void PresetPreviewEditor_StatusNotificationRequested(
        object? sender,
        StatusNotificationRequestedEventArgs e) =>
        StatusNotificationRequested?.Invoke(this, e);

    private void PresetPreviewEditor_MenuStateChanged(object? sender, EventArgs e) =>
        RaiseMenuStateChanged();

    private void RaiseMenuStateChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    

    private BlockCheck2StrategyEvidenceItem? SelectedStrategy =>
        StrategyListView.SelectedItem as BlockCheck2StrategyEvidenceItem;

    private BlockCheck2ManualAssignmentItem? SelectedAssignment =>
        ManualAssignmentsListView.SelectedItem as BlockCheck2ManualAssignmentItem;

    private string Text(string key) => localizer.GetLocalizedString(key);

    private void CopyViewAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null)
        {
            return;
        }

        Copy(item.Evidence.StrategyArguments);
    }

    private void CopyFullAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        BlockCheck2StrategyEvidenceItem? item = SelectedStrategy;
        if (item == null)
        {
            return;
        }
        var result = ViewModel.GetFullStrategyArguments(item);
        if (result.Success) Copy(result.Result ?? string.Empty);
    }

    private void Copy(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);

        ShowActionMessage(InfoBarSeverity.Informational, Text("CopyComplete"), string.Empty);
    }
}
