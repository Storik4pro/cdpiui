using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.Store.Database;
using CDPIUI.Default;
using CDPIUI.Helper.UserExperience;
using CDPIUI.Helper.WindowHelper;
using CDPIUI.ViewModels;
using CDPIUI.Views.BlockCheck2;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using WinUI3Localizer;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;
using WpfSize = System.Windows.Size;

namespace CDPIUI;

public sealed partial class BlockCheck2ResultWindow : TemplateWindow
{
    private readonly ILocalizer localizer = Localizer.Get();
    private readonly BlockCheck2ResultViewModel viewModel = new();
    private readonly BlockCheckReportSerializer reportSerializer = new();
    private bool modeChoiceShown;
    private IStatusNotificationSource? notificationSource;
    private readonly BlockCheckReportImportService importService = new();

    private readonly ObservableCollection<string> StatusBarItems = [];

    public BlockCheck2ResultWindow()
    {
        InitializeComponent();

        WindowTitle = Localizer.Get().GetLocalizedString("BlockCheck2ResultWindowTitle");
        IconUri = @"Assets/Icons/GoodCheck.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        MainFrame = ContentFrame;
        WindowMinSize = new WpfSize(960, 640);

        WindowsPositionHelper.TrySetMicaBackdrop(true, this, MainGrid);

        ContentFrame.Navigated += ContentFrame_Navigated;
        ContentFrame.Loaded += ContentFrame_Loaded;
        viewModel.ManualAssignments.CollectionChanged += (_, _) => UpdateChrome();
        viewModel.Draft.PropertyChanged += (_, _) => UpdateChrome();
        ContentFrame.Navigate(typeof(ResultPage), viewModel);

        StatusBarListView.ItemsSource = StatusBarItems;
    }

    public void ShowReport(BlockCheckReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        viewModel.Load(report);
        OpenBuilder();
        QueueModeChoice();
    }

    private bool ReportOpenRequested = false;
    private string ReportUri = string.Empty;

    public void ShowReport(string filePath)
    {
        LoadingGrid.Visibility = Visibility.Visible;
        ContentFrame.Visibility = Visibility.Collapsed;
        ReportOpenRequested = true;
        ReportUri = filePath;
    }

    private async Task LoadReport(string filePath)
    {
        try
        {
            BlockCheckReportImportResult imported = await importService.LoadAsync(filePath);
            ShowSession(imported.Session);
        }
        catch (Exception exception)
        {
            NotificationCenter.Show(InfoBarSeverity.Error, Text("BlockCheck2ReportOpenFailedTitle"), exception.Message);
        }
    }

    public void ShowSession(BlockCheckResultSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        DispatcherQueue.TryEnqueue(() =>
        {
            viewModel.Load(session);
            OpenBuilder();
            QueueModeChoice();
        });
    }

    private void OpenBuilder()
    {
        modeChoiceShown = false;
        ContentFrame.BackStack.Clear();
        if (ContentFrame.Content is ResultPage page)
        {
            page.LoadViewModel(viewModel);
            UpdateChrome();
            return;
        }
        ContentFrame.Navigate(typeof(ResultPage), viewModel);
    }

    private void QueueModeChoice()
    {
        DispatcherQueue.TryEnqueue(async () => await ShowModeChoiceAsync());
    }

    private void ContentFrame_Loaded(object sender, RoutedEventArgs e)
    {
        if (ReportOpenRequested)
        {
            Load();
        }

        if (!modeChoiceShown && viewModel.HasReport)
        {
            QueueModeChoice();
        }
    }

    private async void Load()
    {
        await Task.Run(() => LoadReport(ReportUri));
        LoadingGrid.Visibility = Visibility.Collapsed;
        ContentFrame.Visibility = Visibility.Visible;
        ReportOpenRequested = false;
    }

    private async System.Threading.Tasks.Task ShowModeChoiceAsync()
    {
        if (modeChoiceShown || ContentFrame.XamlRoot == null)
        {
            return;
        }
        modeChoiceShown = true;
        ContentDialog dialog = new()
        {
            XamlRoot = ContentFrame.XamlRoot,
            Title = Text("BlockCheck2ResultModeDialogTitle"),
            Content = Text("BlockCheck2ResultModeDialogMessage"),
            PrimaryButtonText = Text("BlockCheck2ResultModeAutomaticButton"),
            SecondaryButtonText = Text("BlockCheck2ResultModeManualButton"),
            CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
            DefaultButton = viewModel.CanUseConfig
                ? ContentDialogButton.Primary
                : ContentDialogButton.Secondary,
            IsPrimaryButtonEnabled = viewModel.CanUseConfig,
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            OpenPresetReview();
        }
    }

    private void OpenPresetReview()
    {
        if (!viewModel.CanUseConfig || ContentFrame.Content is PresetReviewPage)
        {
            return;
        }
        ContentFrame.Navigate(typeof(PresetReviewPage), viewModel);
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (notificationSource != null)
        {
            notificationSource.StatusNotificationRequested -= StatusNotificationSource_Requested;
        }
        notificationSource = e.Content as IStatusNotificationSource;
        if (notificationSource != null)
        {
            notificationSource.StatusNotificationRequested += StatusNotificationSource_Requested;
        }
        UpdateChrome();
    }

    private void StatusNotificationSource_Requested(
        object? sender,
        StatusNotificationRequestedEventArgs e) =>
        NotificationCenter.Show(e.Severity, e.Title, e.Message);

    private void UpdateChrome()
    {
        StatusBarItems.Clear();
        bool review = ContentFrame.Content is PresetReviewPage;
        SaveToApplicationMenuItem.Visibility = review ? Visibility.Visible : Visibility.Collapsed;
        SaveToApplicationMenuItem.IsEnabled = review && viewModel.Draft.CanUseConfig;
        BackToBuilderMenuItem.Visibility = review ? Visibility.Visible : Visibility.Collapsed;

        StatusBarItems.Clear();
        if (review)
        {
            StatusBarItems.Add(Text("BlockCheck2ReviewStatusFormat"));
            StatusBarItems.Add(string.Format(Text("BlockCheck2ReviewStatusFormatGroups"), viewModel.Draft.Groups.Count));
            StatusBarItems.Add(string.Format(Text("BlockCheck2ReviewStatusFormatFiles"), viewModel.Draft.Files.Count));
        }
        else
        {
            StatusBarItems.Add(Text("BlockCheck2BuilderStatusFormat"));
            StatusBarItems.Add(RunPresetName());
            StatusBarItems.Add(string.Format(Text("BlockCheck2BuilderStatusTargetCount"), viewModel?.TargetCount.ToString() ?? "%~"));
            StatusBarItems.Add(string.Format(Text("BlockCheck2BuilderStatusFormatStrategies"), viewModel.StrategyEvidenceCount));
            if (!string.IsNullOrEmpty(viewModel?.CreatedAtLocalText)) StatusBarItems.Add(string.Format(Text("BlockCheck2BuilderStatusTime"), viewModel.CreatedAtLocalText));
            StatusBarItems.Add(string.Format(Text("BlockCheck2BuilderStatusFormatSelected"), viewModel.ManualAssignmentCount));

        }
    }

    private string RunPresetName() => viewModel.Report?.RunPreset switch
    {
        BlockCheckRunPreset.Quick => Text("BlockCheck2QuickModeName"),
        BlockCheckRunPreset.Exhaustive => Text("BlockCheck2ExhaustiveModeName"),
        _ => Text("BlockCheck2BalancedModeName"),
    };

    private async void SaveJsonReportItem_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.Report == null)
        {
            return;
        }
        Microsoft.Win32.SaveFileDialog dialog = CreateSaveDialog(
            "blockcheck2-report.json",
            Text("BlockCheck2JsonFileFilter"));
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await reportSerializer.SaveJsonAsync(dialog.FileName, viewModel.Report);
                ShowNotification(
                    InfoBarSeverity.Success,
                    Text("BlockCheck2ActionCompletedTitle"),
                    Text("BlockCheck2FileSavedMessage"));
            }
            catch (Exception exception)
            {
                ShowNotification(
                    InfoBarSeverity.Error,
                    Text("BlockCheck2ActionFailedTitle"),
                    exception.Message);
            }
        }
    }

    private async void SaveTextReportItem_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.Report == null)
        {
            return;
        }
        Microsoft.Win32.SaveFileDialog dialog = CreateSaveDialog(
            "blockcheck2-report.txt",
            Text("BlockCheck2TextFileFilter"));
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await reportSerializer.SaveTextAsync(dialog.FileName, viewModel.Report);
                ShowNotification(
                    InfoBarSeverity.Success,
                    Text("BlockCheck2ActionCompletedTitle"),
                    Text("BlockCheck2FileSavedMessage"));
            }
            catch (Exception exception)
            {
                ShowNotification(
                    InfoBarSeverity.Error,
                    Text("BlockCheck2ActionFailedTitle"),
                    exception.Message);
            }
        }
    }

    private Microsoft.Win32.SaveFileDialog CreateSaveDialog(string fileName, string filter) => new()
    {
        Title = Text("BlockCheck2SaveReportDialogTitle"),
        FileName = fileName,
        Filter = filter,
        OverwritePrompt = true,
        RestoreDirectory = true,
    };

    private async void SaveToApplicationItem_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.CanUseConfig || ContentFrame.Content is not PresetReviewPage page)
        {
            return;
        }
        SaveToApplicationMenuItem.IsEnabled = false;
        try
        {
            await page.SaveToApplicationAsync();
        }
        finally
        {
            UpdateChrome();
        }
    }

    private async void BackToBuilderItem_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.Content is PresetReviewPage page)
        {
            await page.NavigateBackToBuilderAsync();
        }
    }

    private void CloseItem_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowNotification(InfoBarSeverity severity, string title, string message) =>
        NotificationCenter.Show(severity, title, message);

    private string Text(string key) => localizer.GetLocalizedString(key);
}
