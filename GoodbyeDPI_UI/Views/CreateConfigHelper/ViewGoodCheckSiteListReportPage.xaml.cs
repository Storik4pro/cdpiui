using CDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.CreateConfigHelper;

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
public sealed partial class ViewGoodCheckSiteListReportPage : Page
{
    
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
        this.Loaded += ViewGoodCheckSiteListReportPage_Loaded;
    }

    private async void ViewGoodCheckSiteListReportPage_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (StrategyModel strategy in Strategies)
        {
            if (float.TryParse(strategy.All, out var all) && int.TryParse(strategy.Success, out var success))
            {
                StrategyUIModel strategyUIModel = new()
                {
                    Args = strategy.Strategy,
                    FailureCount = (all - success).ToString(),
                    SuccessCount = strategy.Success,
                    Flag = strategy.Flag,
                };
                if (all != 0 && (success / (all / 100)) >= 65)
                {
                    SuccessStrategiesList.Add(strategyUIModel);
                }
                else
                {
                    FailureStrategiesList.Add(strategyUIModel);
                }
            }
        }
        this.Loaded -= ViewGoodCheckSiteListReportPage_Loaded;

        await Task.CompletedTask;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is Tuple<GoodCheckReportHeaderButton, List<StrategyModel>, string> tuple)
        {
            var button = tuple.Item1;
            ComponentId = tuple.Item3;
            Strategies = tuple.Item2;

            HeaderButton.Header = button.Header;
            HeaderButton.SubHeader = button.SubHeader;
            HeaderButton.FlagsCount = button.FlagsCount;
            HeaderButton.SuccessCount = button.SuccessCount;
            HeaderButton.FailureCount = button.FailureCount;
            
        }

        var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
        if (anim != null)
        {
            anim.TryStart(HeaderButton);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        SuccessStrategiesList.Clear();
        FailureStrategiesList.Clear();
        

        var anim = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("BackwardConnectedAnimation", HeaderButton);
        anim.Configuration = new DirectConnectedAnimationConfiguration();
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

    private void HeaderClick(object parameter)
    {
        UIHelper.GoBackWithParameter(Strategies, Frame);
    }
}
