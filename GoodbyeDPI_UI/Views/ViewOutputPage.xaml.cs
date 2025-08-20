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
    private const int MaxLines = 5000;

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
        if (model != null && model.Output != null)
        {
            foreach (string line in model.Output.Split("\n"))
            {
                AppendLogLine(line);
            }
        }
        GoodCheckProcessHelper.Instance.OperationOutputAdded += (tuple) =>
        {
            if (tuple.Item1 == CurrentId)
            {
                AppendLogLine(tuple.Item2);
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

            if (_logLines.Count > 0)
            {
                var last = _logLines[_logLines.Count - 1];
                LogListView.ScrollIntoView(last);
            }
        });
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _flushTimer?.Stop();
        _flushTimer?.Dispose();
    }
}
