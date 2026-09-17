using CDPIUI.AddOns.BlockCheck2.Presentation;
using CDPIUI.AddOns.BlockCheck2.Reporting;
using CDPIUI.Controls.Default;
using CDPIUI.Helper.AddOns.BlockCheck2;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed class BlockCheck2ReportHistoryItemViewModel
{
    public BlockCheckReportHistoryEntry Entry { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed partial class ReportHistoryPage : TemplatePage
{
    private readonly ILocalizer localizer = Localizer.Get();
    private readonly BlockCheckReportHistoryService historyService = new();
    private bool isLoaded;

    public ObservableCollection<BlockCheck2ReportHistoryItemViewModel> HistoryItems { get; } = [];

    public ReportHistoryPage()
    {
        InitializeComponent();
        Loaded += ReportHistoryPage_Loaded;
        Unloaded += ReportHistoryPage_Unloaded;
    }

    private async void ReportHistoryPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!isLoaded)
        {
            localizer.LanguageChanged += Localizer_LanguageChanged;
            isLoaded = true;
        }

        await LoadHistoryAsync();
    }

    private void ReportHistoryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!isLoaded)
        {
            return;
        }

        localizer.LanguageChanged -= Localizer_LanguageChanged;
        isLoaded = false;
    }

    private async void Localizer_LanguageChanged(object? sender, LanguageChangedEventArgs e) =>
        await LoadHistoryAsync();

    private async Task LoadHistoryAsync()
    {
        if (LoadingProgressRing == null)
        {
            return;
        }

        LoadingProgressRing.IsActive = true;
        LoadingProgressRing.Visibility = Visibility.Visible;
        ReportsListView.Visibility = Visibility.Collapsed;
        EmptyHistoryTextBlock.Visibility = Visibility.Collapsed;
        try
        {
            var entries = await historyService.LoadAsync();
            HistoryItems.Clear();
            foreach (BlockCheckReportHistoryEntry entry in entries)
            {
                HistoryItems.Add(CreateViewModel(entry));
            }

            ReportsListView.Visibility = HistoryItems.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            EmptyHistoryTextBlock.Visibility = HistoryItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, Text("BlockCheck2ReportOpenFailedTitle"), exception.Message);
            EmptyHistoryTextBlock.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingProgressRing.IsActive = false;
            LoadingProgressRing.Visibility = Visibility.Collapsed;
        }
    }

    private BlockCheck2ReportHistoryItemViewModel CreateViewModel(BlockCheckReportHistoryEntry entry)
    {
        BlockCheckReport report = entry.Report;
        string status = report.Success
            ? Text("BlockCheck2HistoryStatusSuccess")
            : report.IsBestEffort
                ? Text("BlockCheck2HistoryStatusBestEffort")
                : Text("BlockCheck2HistoryStatusFailed");
        string mode = report.RunPreset switch
        {
            BlockCheckRunPreset.Quick => Text("BlockCheck2QuickModeName"),
            BlockCheckRunPreset.Exhaustive => Text("BlockCheck2ExhaustiveModeName"),
            _ => Text("BlockCheck2BalancedModeName"),
        };
        double sizeKilobytes = Math.Max(1d, entry.FileSize / 1024d);
        return new BlockCheck2ReportHistoryItemViewModel
        {
            Entry = entry,
            Title = string.Format(
                Text("BlockCheck2HistoryItemTitleFormat"),
                report.CreatedAtUtc.ToLocalTime()),
            Description = string.Format(
                Text("BlockCheck2HistoryItemDescriptionFormat"),
                mode,
                status,
                report.Targets.Count,
                report.Profiles.Count,
                sizeKilobytes),
        };
    }

    private async void ReportSettingsCard_Click(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: BlockCheck2ReportHistoryItemViewModel item })
        {
            await OpenReportAsync(item.Entry.FilePath);
        }
    }

    private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true,
            Filter = Text("BlockCheck2ReportFileFilter"),
        };
        if (dialog.ShowDialog() == true)
        {
            await OpenReportAsync(dialog.FileName);
        }
    }

    private async Task OpenReportAsync(string filePath)
    {
        try
        {
            BlockCheck2ResultWindow window = await ((App)Application.Current)
                .SafeCreateNewWindow<BlockCheck2ResultWindow>();
            window.ShowReport(filePath);

            ((App)Application.Current).GetCurrentWindowFromType<CreateConfigUtilWindow>()?.Close();
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, Text("BlockCheck2ReportOpenFailedTitle"), exception.Message);
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(BlockCheckReportHistoryService.HistoryDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = BlockCheckReportHistoryService.HistoryDirectory,
            UseShellExecute = true,
        });
    }

    private void ShowFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BlockCheck2ReportHistoryItemViewModel item })
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{item.Entry.FilePath}\"",
            UseShellExecute = true,
        });
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BlockCheck2ReportHistoryItemViewModel item })
        {
            return;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = Text("BlockCheck2HistoryDeleteDialogTitle"),
            Content = Text("BlockCheck2HistoryDeleteDialogMessage"),
            PrimaryButtonText = Text("BlockCheck2HistoryDeleteButtonText"),
            CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            historyService.Delete(item.Entry.FilePath);
            HistoryItems.Remove(item);
            ReportsListView.Visibility = HistoryItems.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            EmptyHistoryTextBlock.Visibility = HistoryItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            ShowStatus(InfoBarSeverity.Error, Text("BlockCheck2ActionFailedTitle"), exception.Message);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await LoadHistoryAsync();

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
            return;
        }

        if (CreateConfigUtilWindow.Instance?.MainFrame == Frame)
        {
            CreateConfigUtilWindow.Instance.Close();
        }
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusInfoBar.Severity = severity;
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;
    }

    private string Text(string key) => localizer.GetLocalizedString(key);
}
