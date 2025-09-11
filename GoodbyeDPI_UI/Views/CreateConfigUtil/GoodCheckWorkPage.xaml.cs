using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using GoodbyeDPI_UI.Helper.Static;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TextDecorations = Windows.UI.Text.TextDecorations;
using static System.Net.Mime.MediaTypeNames;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Views.CreateConfigUtil;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GoodCheckWorkPage : Page
{
    private DateTime _lastProgressTime = DateTime.MinValue;
    private int _lastProgressCount = 0;
    private double _speedElementsPerSec = 0.0; 
    private const double _emaAlpha = 0.2;

    private double _initialEstimatedSecondsPerOperation = 0.0;

    private readonly DispatcherQueue _uiDispatcher;

    private const string AddOnId = "ASGKOI001";

    private string FilePath = string.Empty;

    public GoodCheckWorkPage()
    {
        InitializeComponent();

        _uiDispatcher = DispatcherQueue.GetForCurrentThread();

        CreateConfigUtilWindow.Instance.ToggleLoadingState(Microsoft.WindowsAPICodePack.Taskbar.TaskbarProgressBarState.Indeterminate);
        GoodCheckProcessHelper.Instance.AllComplete += AllCompletedActions;

        SiteListProgressText.Text = GoodCheckProcessHelper.Instance.CurrentSiteList;
        GoodStrategyCount.Text = GoodCheckProcessHelper.Instance.CorrectCount.ToString();
        BadStrategyCount.Text += GoodCheckProcessHelper.Instance.IncorrectCount.ToString();


        ConnectHandlers();
    }

    private void AllCompletedActions(string filepath)
    {
        FilePath = filepath;
        _uiDispatcher.TryEnqueue(() =>
        {
            ContentGrid.Visibility = Visibility.Collapsed;
            LoadingStateGrid.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;

            EndWork.Visibility = Visibility.Visible;
            ForwardButton.Visibility = Visibility.Visible;
        });

        CreateConfigUtilWindow.Instance.ToggleLoadingState(Microsoft.WindowsAPICodePack.Taskbar.TaskbarProgressBarState.NoProgress);
    }

    private void ConnectHandlers()
    {
        GoodCheckProcessHelper.Instance.CurrentSiteListChanged += (name) =>
        {
            CurrentSiteListText(name);

            _lastProgressTime = DateTime.MinValue;
            _lastProgressCount = 0;
            _speedElementsPerSec = 0.0;
            _initialEstimatedSecondsPerOperation = 0.0;

        };
        GoodCheckProcessHelper.Instance.ProgressChanged += (tuple) =>
        {
            int.TryParse(tuple.Item1, out int current);
            int.TryParse(tuple.Item2, out int all);
            string strategy = tuple.Item3;

            if (all == 0)
            {
                SetSiteListProgressText($"ERR%");
                return;
            }

            double percent = (double)current / (double)all * 100.0;
            CalcSpeed(current, all);

            CreateConfigUtilWindow.Instance.ToggleLoadingState(Microsoft.WindowsAPICodePack.Taskbar.TaskbarProgressBarState.Normal, current, all);
            SetSiteListProgressText($"{percent:F0}% [{current}/{all}]");
        };
        GoodCheckProcessHelper.Instance.CorrectCountChanged += (count) =>
        {
            SetCorrectCountText(count.ToString());
        };
        GoodCheckProcessHelper.Instance.IncorrectCountChanged += (count) =>
        {
            SetIncorrectCountText(count.ToString());
        };
    }

    
    private void CurrentSiteListText(string text)
    {
        if (!_uiDispatcher.HasThreadAccess)
        {
            _uiDispatcher.TryEnqueue(() => CurrentSiteListText(text));
            return;
        }
        SiteListProgressText.Text = text;
        SetSiteListProgressText("Подготовка...");
        SetCorrectCountText("Подготовка...");
        SetIncorrectCountText("Подготовка...");
    }
    private void SetSiteListProgressText(string text)
    {
        if (!_uiDispatcher.HasThreadAccess)
        {
            _uiDispatcher.TryEnqueue(() => SetSiteListProgressText(text));
            return;
        }
        SiteListProgress.Text = text;
    }
    private void SetCorrectCountText(string text)
    {
        if (!_uiDispatcher.HasThreadAccess)
        {
            _uiDispatcher.TryEnqueue(() => SetCorrectCountText(text));
            return;
        }
        GoodStrategyCount.Text = text;
    }
    private void SetIncorrectCountText(string text)
    {
        if (!_uiDispatcher.HasThreadAccess)
        {
            _uiDispatcher.TryEnqueue(() => SetIncorrectCountText(text));
            return;
        }
        BadStrategyCount.Text = text;
    }

    private void CalcSpeed(int current, int all)
    {
        if (!_uiDispatcher.HasThreadAccess)
        {
            _uiDispatcher.TryEnqueue(() => CalcSpeed(current, all));
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (_lastProgressTime == DateTime.MinValue)
        {
            _lastProgressTime = now;
            _lastProgressCount = current;
        }
        else
        {
            double dt = (now - _lastProgressTime).TotalSeconds;
            if (dt > 0.05)
            {
                int deltaItems = current - _lastProgressCount;
                double instantSpeed = deltaItems / dt; 

                if (instantSpeed < 0) instantSpeed = 0;

                if (_speedElementsPerSec <= 0.0)
                    _speedElementsPerSec = instantSpeed;
                else
                    _speedElementsPerSec = _emaAlpha * instantSpeed + (1.0 - _emaAlpha) * _speedElementsPerSec;

                _lastProgressTime = now;
                _lastProgressCount = current;
            }
        }

        int remainingItems = Math.Max(0, all - current);
        double etaSecondsCurrent = double.PositiveInfinity;
        if (_speedElementsPerSec > 1e-6)
            etaSecondsCurrent = remainingItems / _speedElementsPerSec;

        if (_initialEstimatedSecondsPerOperation <= 0.0 && !double.IsInfinity(etaSecondsCurrent))
        {
            _initialEstimatedSecondsPerOperation = etaSecondsCurrent * 1.05;
        }

        var idx = GoodCheckProcessHelper.Instance.CurrentSiteListIndex;
        int currentOpIndex = idx != null ? idx.Item1 : 1;
        int totalOps = idx != null ? Math.Max(1, idx.Item2) : 1;

        int remainingOpsCount = Math.Max(0, totalOps - currentOpIndex);

        double perOpEstimate = _initialEstimatedSecondsPerOperation > 1e-6
            ? _initialEstimatedSecondsPerOperation
            : (double.IsInfinity(etaSecondsCurrent) ? 0.0 : etaSecondsCurrent);

        double allSecondsRemaining;
        if (!double.IsInfinity(etaSecondsCurrent))
        {
            allSecondsRemaining = etaSecondsCurrent + remainingOpsCount * perOpEstimate;
        }
        else if (perOpEstimate > 1e-6)
        {
            allSecondsRemaining = perOpEstimate * (1 + remainingOpsCount);
        }
        else
        {
            allSecondsRemaining = double.PositiveInfinity;
        }

        if (!double.IsInfinity(etaSecondsCurrent) && allSecondsRemaining < etaSecondsCurrent)
        {
            allSecondsRemaining = etaSecondsCurrent + remainingOpsCount * perOpEstimate;
        }

        string speedText = _speedElementsPerSec > 0 ? $"{_speedElementsPerSec:F3} e/s" : "—";
        string etaCurrentText = double.IsInfinity(etaSecondsCurrent) ? "Расчет..." : Utils.ConvertMinutesToPrettyText((etaSecondsCurrent / 60.0));
        string allTimeText = double.IsInfinity(allSecondsRemaining) ? "Расчет..." : Utils.ConvertMinutesToPrettyText((allSecondsRemaining / 60.0));

        TimeText.Text = etaCurrentText;
        AllTimeText.Text = allTimeText;
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private void ViewMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (AdditionalInfo.Visibility == Visibility.Visible)
        {
            AdditionalInfo.Visibility = Visibility.Collapsed;
            ViewMoreText.Text = "Показать больше";
        }
        else
        {
            AdditionalInfo.Visibility = Visibility.Visible;
            ViewMoreText.Text = "Показать меньше";
        }
        ViewMoreText.TextDecorations = TextDecorations.Underline;
    }

    private void ViewMoreButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ViewMoreText.TextDecorations = TextDecorations.None;
    }

    private void ViewMoreButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ViewMoreText.TextDecorations = TextDecorations.Underline;
    }

    private void ViewLogHyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        string localAppData = StateHelper.GetDataDirectory();
        string dirName = Path.Combine(
            localAppData,
            StateHelper.StoreDirName,
            StateHelper.StoreItemsDirName,
            AddOnId,
            "Logs");

        Utils.OpenFileInDefaultApp($"{dirName}");
    }

    private void KillProc_Click(object sender, RoutedEventArgs e)
    {
        GoodCheckProcessHelper.Instance.RemoveFromQueueOrStopOperation(GoodCheckProcessHelper.Instance.CurrentOperationId);
    }

    private void EndAll_Click(object sender, RoutedEventArgs e)
    {
        GoodCheckProcessHelper.Instance.Stop();
    }

    private async void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(FilePath))
        {
            var window = await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>();
            window.OpenGoodCheckReportFromFile(FilePath);
            CreateConfigUtilWindow.Instance.Close();
        }
        else
        {
            Logger.Instance.CreateWarningLog(nameof(GoodCheckWorkPage), $"Exception happens: FilePath not set. ERR_GOODCHECK_REPORT_OPEN");
            CreateConfigUtilWindow.Instance.Close();
        }
    }
}
