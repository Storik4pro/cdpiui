using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Controls.Default;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.AddOns.ConfigImport;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUI3Localizer;

namespace CDPIUI.Views.ConfigImportUtil;

public enum ImportUtilitySteps
{
    Welcome,
    Working,
    ResolveMissingFiles,
    Done
}

public sealed partial class MainPage : TemplatePage
{
    private readonly ObservableCollection<DatabaseStoreItem> components = [];
    private readonly ObservableCollection<ConfigImportPresetViewModel> results = [];
    private readonly ObservableCollection<ConfigImportMissingFileViewModel> missingFiles = [];
    private readonly ConfigImportService importService = new();
    private readonly ConfigImportInstaller installer = new();
    private readonly ConfigImportAutoCorrector autoCorrector = new();
    private readonly ILocalizer localizer = Localizer.Get();

    private string requestedTargetId = string.Empty;
    private ConfigImportPresetViewModel activeTestItem;
    private string activeTestComponentId = string.Empty;
    private ConfigImportTestPlaceholder activeTestPlaceholder;

    public MainPage()
    {
        InitializeComponent();
        LoadComponents();
        ResultItemsControl.ItemsSource = results;
        MissingFilesItemsControl.ItemsSource = missingFiles;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        requestedTargetId = Parameter.Get("componentId") ?? string.Empty;
    }

    private void LoadComponents()
    {
        components.Clear();
        foreach (DatabaseStoreItem item in DatabaseHelper.Instance.GetItemsByType("component")
                     .Where(IsImportTarget)
                     .OrderBy(item => item.ShortName ?? item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            item.ShortName = string.IsNullOrWhiteSpace(item.ShortName) ? item.Name : item.ShortName;
            components.Add(item);
        }
    }

    private static bool IsImportTarget(DatabaseStoreItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) ||
            string.IsNullOrWhiteSpace(item.Directory) ||
            string.IsNullOrWhiteSpace(item.Executable))
        {
            return false;
        }

        return Directory.Exists(item.Directory) &&
               File.Exists(Path.Combine(item.Directory, GetExecutableFileName(item.Executable)));
    }

    private async Task<bool> ImportConfigsAsync()
    {
        var result = ConfigImportHelper.OpenFileSelectionDialog(true);
        if (!result.Success) return false;
        string[] filePaths = result.Result;

        if (filePaths.Length == 0)
            return false;
        
        UtilityButtonContols.IsLoading = true;

        MainContent.GoTo(ProgressStep);

        results.Clear();
        missingFiles.Clear();

        await Task.Run(() => ImportWork(filePaths));

        await PrepareMissingFilesAsync();

        if (missingFiles.Count > 0)
            ShowMissingFiles();
        else
            ShowCompletion();

        return true;
    }

    private async Task ImportWork(string[] filePaths)
    {
        List<ConfigImportTarget> targets = [.. components.Select(CreateTarget)];
        for (int index = 0; index < filePaths.Length; index++)
        {
            
            string text = string.Format(
                localizer.GetLocalizedString("ConfigImportProgressCounter"),
                index + 1,
                filePaths.Length,
                Path.GetFileName(filePaths[index]));

            DispatcherQueue.TryEnqueue(() =>
            {
                UtilityButtonContols.LoadingStateText = text;
            });

            AnalyzedImport analyzed = await Task.Run(() => AnalyzeFile(Path.GetFullPath(filePaths[index]), targets));
            DatabaseStoreItem initialComponent = GetInitialComponent(analyzed.Result.Target.ComponentId);

            DispatcherQueue.TryEnqueue(() =>
            {
                results.Add(new ConfigImportPresetViewModel(
                analyzed.Result,
                components,
                initialComponent,
                analyzed.TargetWasDetected));
            });
        }
    }

    private async Task PrepareMissingFilesAsync()
    {
        foreach (ConfigImportPresetViewModel item in results.Where(item => item.Result.IsSuccessful))
        {
            foreach (string missingPath in item.Result.MissingReferencedFiles)
            {
                bool suggestEmptyFile = autoCorrector.ShouldSuggestEmptyFile(missingPath);
                string suggestion = suggestEmptyFile
                    ? null
                    : await Task.Run(() => autoCorrector.FindReplacement(item.Result, missingPath));
                missingFiles.Add(new ConfigImportMissingFileViewModel(
                    item,
                    missingPath,
                    suggestion,
                    suggestEmptyFile));
                
            }
        }
    }

    private void ShowMissingFiles()
    {
        UtilityButtonContols.IsLoading = false;

        MainContent.GoTo(MissingFilesStep);

        NextButton.Content = localizer.GetLocalizedString("ConfigImportContinueButton");
        UpdateMissingFilesNextState();
    }

    private AnalyzedImport AnalyzeFile(string filePath, IReadOnlyList<ConfigImportTarget> targets)
    {
        IReadOnlyList<ConfigImportTarget> matches = importService.FindMatchingTargets(filePath, targets);
        ConfigImportTarget target = matches.FirstOrDefault()
            ?? FindRequestedTarget(targets)
            ?? targets.FirstOrDefault()
            ?? new ConfigImportTarget(string.Empty, string.Empty, string.Empty, null);
        return new AnalyzedImport(
            importService.Import(filePath, target),
            matches.Count > 0);
    }

    private ConfigImportTarget FindRequestedTarget(IReadOnlyList<ConfigImportTarget> targets) =>
        targets.FirstOrDefault(target =>
            string.Equals(target.ComponentId, requestedTargetId, StringComparison.OrdinalIgnoreCase));

    private DatabaseStoreItem GetInitialComponent(string conversionTargetId)
    {
        DatabaseStoreItem requested = components.FirstOrDefault(item =>
            string.Equals(item.Id, requestedTargetId, StringComparison.OrdinalIgnoreCase));
        if (requested != null)
            return requested;

        return components.FirstOrDefault(item =>
            string.Equals(item.Id, conversionTargetId, StringComparison.OrdinalIgnoreCase));
    }

    private void ShowCompletion()
    {
        UtilityButtonContols.IsLoading = false;
        NextButton.IsEnabled = true;

        NextButton.Content = localizer.GetLocalizedString("SaveAll");

        MainContent.GoTo(CompletedStep);

        int successful = results.Count(item => item.Result.IsSuccessful);
        if (successful == results.Count)
        {
            CompletionInfoBar.Severity = InfoBarSeverity.Success;
            CompletionInfoBar.Title = localizer.GetLocalizedString("ConfigImportCompletedSuccessTitle");
            CompletionInfoBar.Message = localizer.GetLocalizedString("ConfigImportCompletedSuccessMessage");
        }
        else if (successful > 0)
        {
            CompletionInfoBar.Severity = InfoBarSeverity.Warning;
            CompletionInfoBar.Title = localizer.GetLocalizedString("ConfigImportCompletedPartialTitle");
            CompletionInfoBar.Message = string.Format(
                localizer.GetLocalizedString("ConfigImportCompletedPartialMessage"),
                successful,
                results.Count);
        }
        else
        {
            CompletionInfoBar.Severity = InfoBarSeverity.Error;
            CompletionInfoBar.Title = localizer.GetLocalizedString("ConfigImportCompletedFailedTitle");
            CompletionInfoBar.Message = localizer.GetLocalizedString("ConfigImportCompletedFailedMessage");
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConfigImportPresetViewModel item } ||
            item.SelectedComponent == null ||
            !item.Result.IsSuccessful)
        {
            return;
        }
        if (await SaveItemAsync(item))
            results.Remove(item);

        if (results.Count == 0)
        {
            NextButton.Visibility = Visibility.Collapsed;
            MainContent.GoTo(WorkDoneStep);
        }
    }

    private async Task<bool> SaveItemAsync(ConfigImportPresetViewModel item)
    {
        if (ReferenceEquals(activeTestItem, item))
            await StopActiveTestAsync();

        item.SetSaving(true);
        try
        {
            ConfigImportResult selectedResult = GetSelectedResult(item);
            EnsureSuccessfulRetarget(selectedResult);
            ConfigImportInstallResult installResult = await installer.InstallAsync(selectedResult, item.Name);
            if (!installResult.IsSuccessful)
            {
                item.SetOperationError(installResult.ErrorCode ?? localizer.GetLocalizedString("ConfigImportUnknownError"));
                return false;
            }

            ComponentItemsLoaderHelper.Instance
                .GetComponentHelperFromId(item.SelectedComponent.Id!)
                ?.ReInitConfigs();
            item.SetSaved();
            return true;
        }
        catch (Exception exception)
        {
            item.SetOperationError(exception.Message);
            return false;
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConfigImportPresetViewModel item } ||
            item.SelectedComponent == null ||
            !item.Result.IsSuccessful)
        {
            return;
        }

        if (ReferenceEquals(activeTestItem, item))
        {
            await StopActiveTestAsync();
            return;
        }

        await StopActiveTestAsync();
        try
        {
            ConfigImportResult selectedResult = GetSelectedResult(item);
            EnsureSuccessfulRetarget(selectedResult);
            selectedResult.Config!.packId = Path.GetDirectoryName(item.SourcePath)!;
            string arguments = ConfigurationService.GetStartupParametersByConfigItem(selectedResult.Config);
            if (string.IsNullOrWhiteSpace(arguments))
                throw new InvalidOperationException(localizer.GetLocalizedString("ConfigImportTestEmptyArguments"));

            activeTestPlaceholder = ConfigImportTestPlaceholder.Create(
                arguments,
                selectedResult.SourcePath,
                selectedResult.MissingReferencedFiles,
                selectedResult.MissingFileResolutions,
                selectedResult.GeneratedFiles);
            arguments = activeTestPlaceholder.Arguments;

            activeTestItem = item;
            activeTestComponentId = item.SelectedComponent.Id!;
            await ComponentTasksManager.Instance.StopTask(activeTestComponentId);
            item.SetTesting(true);
            await ComponentTasksManager.Instance.CreateAndRunNewTask(activeTestComponentId, arguments);
            ComponentTasksManager.Instance.TaskStateUpdated += ComponentTasksManager_TaskStateUpdated;
            if (!await ComponentTasksManager.Instance.IsTaskRunned(activeTestComponentId))
                throw new InvalidOperationException(localizer.GetLocalizedString("ConfigImportTestProcessExited"));
        }
        catch (Exception exception)
        {
            await StopActiveTestAsync();
            item.SetOperationError(exception.Message);
        }
    }

    private ConfigImportResult GetSelectedResult(ConfigImportPresetViewModel item)
    {
        ConfigImportResult resolved = importService.ApplyMissingFileResolutions(
            item.Result,
            item.MissingFileResolutions);
        return importService.Retarget(resolved, CreateTarget(item.SelectedComponent!));
    }

    private void UseSuggestedFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConfigImportMissingFileViewModel item })
        {
            item.UseSuggestion();
            missingFiles.Remove(item);
            UpdateMissingFilesNextState();
        }
    }

    private void ChooseReplacementButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConfigImportMissingFileViewModel item })
            return;

        string extension = Path.GetExtension(item.MissingPath);
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = localizer.GetLocalizedString("ConfigImportChooseReplacementButton.Content"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Filter = string.IsNullOrWhiteSpace(extension)
                ? $"{localizer.GetLocalizedString("AllSupported")} (*.*)|*.*"
                : $"{extension.TrimStart('.').ToUpperInvariant()} (*{extension})|*{extension}|{localizer.GetLocalizedString("AllSupported")} (*.*)|*.*",
            FilterIndex = 1,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        item.UseReplacement(dialog.FileName);
        missingFiles.Remove(item);
        UpdateMissingFilesNextState();
    }

    private void UseEmptyFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ConfigImportMissingFileViewModel item })
        {
            item.UseEmptyFile();
            missingFiles.Remove(item);
            UpdateMissingFilesNextState();
        }
    }

    private void UpdateMissingFilesNextState() =>
        NextButton.IsEnabled = 
        missingFiles.Count == 0 
        || (missingFiles.Count > 0 && missingFiles.All(item => item.IsResolved))
        || (missingFiles.Count > 0 && missingFiles.All(item => item.HasSuggestion));

    private static void EnsureSuccessfulRetarget(ConfigImportResult result)
    {
        if (result.IsSuccessful && result.Config != null)
            return;

        string errors = string.Join(
            Environment.NewLine,
            result.Issues
                .Where(issue => issue.Severity == ConfigImportIssueSeverity.Error)
                .Select(issue => issue.Message));
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(errors)
            ? "The imported Config could not be converted for the selected component."
            : errors);
    }

    private void ComponentTasksManager_TaskStateUpdated(Tuple<string, bool> state)
    {
        if (!string.Equals(state.Item1, activeTestComponentId, StringComparison.OrdinalIgnoreCase) || state.Item2)
            return;

        DispatcherQueue.TryEnqueue(CompleteTestSession);
    }

    private async Task StopActiveTestAsync()
    {
        ComponentTasksManager.Instance.TaskStateUpdated -= ComponentTasksManager_TaskStateUpdated;
        string componentId = activeTestComponentId;
        if (!string.IsNullOrWhiteSpace(componentId))
        {
            try
            {
                await ComponentTasksManager.Instance.StopTask(componentId);
            }
            catch
            {
            }
        }
        CompleteTestSession();
    }

    private void CompleteTestSession()
    {
        ComponentTasksManager.Instance.TaskStateUpdated -= ComponentTasksManager_TaskStateUpdated;
        activeTestItem?.SetTesting(false);
        activeTestPlaceholder?.Dispose();
        activeTestPlaceholder = null;
        activeTestItem = null;
        activeTestComponentId = string.Empty;
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not HyperlinkButton { Tag: ConfigImportPresetViewModel item } button)
            return;

        var detailsPanel = new StackPanel { Spacing = 10, Width = 350 };

        string titleText = item.HasDetails
            ? string.Format(localizer.GetLocalizedString("ConfigImportErrorView"), item.FileName)
            : localizer.GetLocalizedString("ConfigImportSuccess");

        detailsPanel.Children.Add(new TextBlock
        {
            Text = titleText,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (ConfigImportIssue issue in item.Result.Issues)
        {
            string location = issue.LineNumber.HasValue
                ? string.Format(localizer.GetLocalizedString("ConfigImportIssueLine"), issue.LineNumber.Value)
                : string.Empty;
            detailsPanel.Children.Add(new TextBlock
            {
                Text = $"[{issue.Code}]{location} {issue.Message}",
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!string.IsNullOrWhiteSpace(item.OperationError))
        {
            detailsPanel.Children.Add(new TextBlock
            {
                Text = item.OperationError,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        var flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
            Content = new ScrollViewer
            {
                MaxHeight = 420,
                Content = detailsPanel,
            },
        };
        flyout.ShowAt(button);
    }

    private async void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        await StopActiveTestAsync();
    }

    private static ConfigImportTarget CreateTarget(DatabaseStoreItem item) => new(
        item.Id!,
        item.ShortName ?? item.Name ?? item.Id!,
        item.Executable!,
        item.CurrentVersion,
        item.Directory);

    private static string GetExecutableFileName(string executable) =>
        executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? executable
            : $"{executable}.exe";

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).GetCurrentWindowFromType<ConfigImportUtilWindow>().Close();
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainContent.SelectedItem == SelectionStep)
        {
            NextButton.Visibility = Visibility.Collapsed;
            if (await ImportConfigsAsync())
            {
                NextButton.Visibility = Visibility.Collapsed;
            }
            NextButton.Visibility = Visibility.Visible;
        }
        else if (MainContent.SelectedItem == MissingFilesStep)
        {
            NextButton.Visibility = Visibility.Visible;
            bool flg = false;
            foreach (var item in missingFiles)
            {
                if (!item.IsResolved && item.HasSuggestion)
                {
                    item.UseSuggestion();
                }
                if (!item.IsResolved && !item.HasSuggestion)
                {
                    NextButton.IsEnabled = false;
                    flg = true;
                }
            }
            if (!flg) ShowCompletion();
        }
        else if (MainContent.SelectedItem == CompletedStep)
        {
            NextButton.Visibility = Visibility.Collapsed;
            ResultItemsControl.IsEnabled = false;

            UtilityButtonContols.IsLoading = true;
            UtilityButtonContols.LoadingStateText = localizer.GetLocalizedString("Saving");

            List<ConfigImportPresetViewModel> itemsToRemove = [];
            foreach (var item in results.ToList())
            {
                if (item.CanSave)
                {
                    if (await SaveItemAsync(item))
                        itemsToRemove.Add(item);
                }
            }

            foreach (var item in itemsToRemove)
            {
                if (string.IsNullOrEmpty(item.OperationError))
                {
                    results.Remove(item);
                }
            }

            itemsToRemove.Clear();

            UtilityButtonContols.IsLoading = false;
            ResultItemsControl.IsEnabled = true;

            if (results.Count == 0)
            {
                MainContent.GoTo(WorkDoneStep);
            }
        }
        
    }

    private sealed record AnalyzedImport(ConfigImportResult Result, bool TargetWasDetected);
}
