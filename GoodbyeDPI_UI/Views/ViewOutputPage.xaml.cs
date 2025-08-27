using GoodbyeDPI_UI.DataModel;
using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Timers;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Text;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification = false;

    public void AddRange(IEnumerable<T> items)
    {
        if (items == null) return;
        _suppressNotification = true;
        foreach (var it in items) base.Add(it);
        _suppressNotification = false;

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suppressNotification) return;
        base.OnCollectionChanged(e);
    }
}

public sealed partial class ViewOutputPage : Page
{
    public int CurrentId { get; private set; }

    private readonly BulkObservableCollection<string> _logLines = new();
    private readonly List<string> _pending = new();
    private readonly object _lock = new();
    private readonly Timer _flushTimer;
    private const int FlushIntervalMs = 200; 
    private const int MaxLines = 500;

    private bool autoScroll = true;

    private bool _isLogging = false;

    public ViewOutputPage()
    {
        InitializeComponent();
        
        LogListView.ItemsSource = _logLines;

        _flushTimer = new Timer(FlushIntervalMs);
        _flushTimer.Elapsed += FlushTimer_Elapsed;
        _flushTimer.AutoReset = true;
    }

    

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is int id)
        {
            CurrentId = id;
            ConnectHandlers();
        }
    }

    private void ConnectHandlers()
    {
        GoodCheckOperationModel model = GoodCheckProcessHelper.Instance.GetOperationById(CurrentId);

        if (model!=null && model.OperationType == GoodCheckOperationType.WorkInProgress || model.OperationType == GoodCheckOperationType.Wait)
        {
            if (model.Output != null)
            {
                foreach (string line in model.Output.Split("\n"))
                {
                    AppendLogLine(line);
                }
            }
            StartLogging();
        }
        else
        {
            StopLogging();
        }
        GoodCheckProcessHelper.Instance.OperationOutputAdded += (tuple) =>
        {
            if (tuple.Item1 == CurrentId)
            {
                AppendLogLine(tuple.Item2);
            }
        };
        GoodCheckProcessHelper.Instance.OperationTypeChanged += (tuple) =>
        {
            if (tuple.Item1 == CurrentId)
            {
                if (tuple.Item2 != GoodCheckOperationType.WorkInProgress && tuple.Item2 != GoodCheckOperationType.Wait)
                {
                    StopLogging();
                }
            }
        };
    }

    public void AppendLogLine(string line)
    {
        if (line == null) return;
        lock (_lock)
        {
            _pending.Add(line);
        }
    }

    public void StartLogging()
    {
        _isLogging = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            TargetScrollViewer.Visibility = Visibility.Visible;
            OutputScrollViewer.Visibility = Visibility.Collapsed;

            OutputParagraph.Inlines.Clear();
        });

        _flushTimer.Start();
    }

    public void StopLogging()
    {
        _isLogging = false;
        _flushTimer.Stop();

        FlushPendingToUi();

        DispatcherQueue.TryEnqueue(() =>
        {
            TargetScrollViewer.Visibility = Visibility.Collapsed;
            OutputScrollViewer.Visibility = Visibility.Visible;
            try
            {
                _logLines.Clear();

                GoodCheckOperationModel model = GoodCheckProcessHelper.Instance.GetOperationById(CurrentId);
                if (model != null && model.Output != null)
                {
                    AppendToRichTextBlock(model.Output);

                }
            }
            catch { }
        });
    }

    public void AppendToRichTextBlock(string text)
    {
        if (text == null) return;

        string[] lines = text.Replace("\r", "").Split('\n');
        lock (_lock)
        {
            foreach (var l in lines)
            {
                Run run = new Run
                {
                    Text = $"{l}\n",
                    Foreground = new SolidColorBrush(Colors.LightGray)
                };

                OutputParagraph.Inlines.Add(run);
            }
        }
    }

    private void FlushTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        FlushPendingToUi();
    }

    private void FlushPendingToUi()
    {
        List<string> batch;
        lock (_lock)
        {
            if (_pending.Count == 0) return;
            batch = new List<string>(_pending);
            _pending.Clear();
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            _logLines.AddRange(batch);

            if (_logLines.Count > MaxLines)
            {
                int remove = _logLines.Count - MaxLines;
                for (int i = 0; i < remove; i++)
                {
                    _logLines.RemoveAt(0);
                }
            }

            if (_logLines.Count > 0 && autoScroll)
            {
                TargetScrollViewer.UpdateLayout();
                TargetScrollViewer.ScrollToVerticalOffset(TargetScrollViewer.ScrollableHeight);

            }
        });
    }


    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _flushTimer?.Stop();
        _flushTimer?.Dispose();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var _dialog = new Microsoft.Win32.SaveFileDialog
        {
            OverwritePrompt = true,
            FileName = "PseudoConsoleLog.txt",
            DefaultExt = ".txt",
            Filter = "Text Files|*.txt"
        };
        var result = _dialog.ShowDialog();
        if (result.HasValue && result.Value)
        {
            string filename = _dialog.FileName;

            string text = GoodCheckProcessHelper.Instance.GetOperationById(CurrentId).Output;
            try
            {
                File.WriteAllText(filename, text);

            }
            catch (Exception ex)
            {
                ErrorContentDialog dialog = new ErrorContentDialog { };
                await dialog.ShowErrorDialogAsync(content: $"File {_dialog.FileName} couldn't be saved.\nFILE_SAVE_ERROR",
                    errorDetails: $"{ex}",
                    xamlRoot: this.Content.XamlRoot);
            }

        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        var _window = ((App)Application.Current).GetCurrentWindowFromType<ViewGoodCheckOutputWindow>();

        if (_window == null)
            return;

        _flushTimer?.Stop();
        _flushTimer?.Dispose();

        OutputParagraph.Inlines.Clear();

        _window.Close();
    }

    private void ProcessControl_Click(object sender, RoutedEventArgs e)
    {
        GoodCheckProcessHelper.Instance.RemoveFromQueueOrStopOperation(CurrentId);
    }

    private void ProcessExit_Click(object sender, RoutedEventArgs e)
    {
        GoodCheckProcessHelper.Instance.Stop();
    }

    private void EnableAutoScrollButton_Click(object sender, RoutedEventArgs e)
    {
        autoScroll = true;
    }

    private void DisableAutoScrollButton_Click(object sender, RoutedEventArgs e)
    {
        autoScroll = false;
    }

    private void ChangeFont_Click(object sender, RoutedEventArgs e)
    {

    }

    private async void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Storik4pro/goodbyeDPI-UI/issues/"));
    }
}
