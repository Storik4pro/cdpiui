using CDPIUI.AddOns.GoodCheck;
using CDPIUI.Controls.Default;
using CDPIUI.Core;
using CDPIUI.Core.JSON;
using CDPIUI.Helper;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.CreateConfigHelper;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>

public class StrategyUIModel
{
    public string Args {  get; set; }
    public string SuccessCount {  get; set; }
    public string FailureCount {  get; set; }
    public bool Flag {  get; set; }

}
public sealed partial class ViewGoodCheckSiteListReportPage : TemplatePage
{
    private ILocalizer localizer = Localizer.Get();
    
    public ICommand HeaderClickCommand { get; }
    public ICommand FlagSetCommand { get; }

    private ObservableCollection<StrategyUIModel> SuccessStrategiesList = new();
    private ObservableCollection<StrategyUIModel> FailureStrategiesList = new();
    public List<StrategyModel> Strategies { get; private set; } = new();
    public string ComponentId { get; private set; }

    private object parameter = null;
    public ViewGoodCheckSiteListReportPage()
    {
        InitializeComponent();
        this.DataContext = this;

        HeaderClickCommand = new RelayCommand(p => HeaderClick((GoodCheckReportHeaderButton)p));
        FlagSetCommand = new RelayCommand(p => ToggleFlag(p));

        SuccessStrategiesListView.ItemsSource = SuccessStrategiesList;
        FailureStrategiesListView.ItemsSource = FailureStrategiesList;
        MainSelectorBar.SelectedItem = SelectorBarItemSuccess;

        var window = ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>();
        window?.SetStatus(true, localizer.GetLocalizedString("ReadingReportResults"));

        if (SettingsManager.Instance.GetValue<bool>("CONFIGDESIGNER", "showFailureStrategies"))
        {
            FailureStrategiesListView.Visibility = Visibility.Visible;
            ContentNotDisplayedStackPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            FailureStrategiesListView.Visibility = Visibility.Collapsed;
            ContentNotDisplayedStackPanel.Visibility = Visibility.Visible;
        }

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = HeaderButton;
        this.Loaded += ViewGoodCheckSiteListReportPage_Loaded;
    }

    private async void ViewGoodCheckSiteListReportPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadResultFor(true);

        this.Loaded -= ViewGoodCheckSiteListReportPage_Loaded;

        await Task.CompletedTask;
    }

    private async void LoadResultFor(bool isSuccess)
    {
        var window = ((App)Application.Current).GetCurrentWindowFromType<CreateConfigHelperWindow>();
        window?.SetStatus(true, localizer.GetLocalizedString("ReadingReportResults"));

        await Task.Run(() => LoadResultToCollection(isSuccess, isSuccess? SuccessStrategiesList : FailureStrategiesList));

        if (SuccessStrategiesList.Count == 0)
        {
            SuccessNotShow.Visibility = Visibility.Visible;
        }
        else
        {
            SuccessNotShow.Visibility = Visibility.Collapsed;
        }
        if (FailureStrategiesList.Count == 0)
        {
            FailureNotShow.Visibility = Visibility.Visible;
        }
        else
        {
            FailureNotShow.Visibility = Visibility.Collapsed;
        }

            window?.SetStatus(false);
    }

    private SemaphoreSlim Semaphore = new(1, 1);
    private async Task LoadResultToCollection(bool isSuccess, ObservableCollection<StrategyUIModel> collection)
    {
        await Semaphore.WaitAsync();
        try
        {
            DispatcherQueue.TryEnqueue(() => collection.Clear());
            await Task.Delay(500);
            foreach (StrategyModel strategy in Strategies)
            {
                if (float.TryParse(strategy.All, out var all) && int.TryParse(strategy.Success, out var success))
                {

                    bool isCorrect = all != 0 && (success / (all / 100)) >= 65;
                    if ((isCorrect && isSuccess) || (!isCorrect && !isSuccess))
                    {
                        StrategyUIModel strategyUIModel = new()
                        {
                            Args = strategy.Strategy,
                            FailureCount = (all - success).ToString(),
                            SuccessCount = strategy.Success,
                            Flag = strategy.Flag,
                        };
                        DispatcherQueue.TryEnqueue(() => collection.Add(strategyUIModel));
                    }
                }
            }
        }
        finally
        {
            Semaphore.Release();
        }

        await Task.CompletedTask;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (Parameter != null)
        {
            ComponentId = Parameter.Get("componentId");
            Strategies = JSONConvertor.DeserializeObject<List<StrategyModel>>(Parameter.Get("strategies"));

            HeaderButton.Header = Parameter.Get("buttonHeader");
            HeaderButton.SubHeader = Parameter.Get("buttonSubHeader");
            HeaderButton.FlagsCount = Parameter.Get("buttonFlagsCount");
            HeaderButton.SuccessCount = Parameter.Get("buttonSuccessCount");
            HeaderButton.FailureCount = Parameter.Get("buttonFailureCount");
            
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        SuccessStrategiesList.Clear();
        FailureStrategiesList.Clear();

        PrepareToConnectedBackwardAnimate(HeaderButton);

        UIHelper.CleanUp.FrameworkElement(this);
    }

    public void ToggleFlag(object parameter)
    {
        if (parameter is Tuple<string, bool> tuple)
        {
            if (int.TryParse(HeaderButton.FlagsCount, out var flagsCount))
            {
                flagsCount += tuple.Item2 ? 1 : -1;
                HeaderButton.FlagsCount = flagsCount.ToString();
            }
            foreach (var _strategyModel in Strategies)
            {
                if (_strategyModel.Strategy == tuple.Item1)
                {
                    _strategyModel.Flag = tuple.Item2;
                }
            }
        }
    }

    private async void MainSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem == SelectorBarItemSuccess)
        {
            SuccessStackPanel.Visibility = Visibility.Visible;
            FailureStackPanel.Visibility = Visibility.Collapsed;
            if (SuccessStrategiesList.Count == 0) LoadResultFor(true);
        }
        else
        {
            SuccessStackPanel.Visibility = Visibility.Collapsed;
            FailureStackPanel.Visibility = Visibility.Visible;
            if (FailureStrategiesList.Count == 0 && SettingsManager.Instance.GetValue<bool>("CONFIGDESIGNER", "showFailureStrategies")) LoadResultFor(false);
        }
    }

    private void ShowAnywayButton_Click(object sender, RoutedEventArgs e)
    {
        SuccessStackPanel.Visibility = Visibility.Collapsed;
        FailureStackPanel.Visibility = Visibility.Visible;
        FailureStrategiesListView.Visibility = Visibility.Visible;
        ContentNotDisplayedStackPanel.Visibility = Visibility.Collapsed;
        if (FailureStrategiesList.Count == 0) LoadResultFor(false);
    }

    private void HeaderClick(object parameter)
    {
        UIHelper.GoBackWithParameter(
            new NameValueCollection() 
            {
                { "type", NavigationState.GoBack.ToString() },
                { "strategies", JSONConvertor.SerializeObject(Strategies) } }
        , Frame);
    }
}
