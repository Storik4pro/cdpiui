using CDPIUI.AddOns.Troubleshooting;
using CDPIUI.Controls.Default;
using CDPIUI.Helper.LScript;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using MS.WindowsAPICodePack.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using WinUI3Localizer;
using static CDPIUI.AddOns.Troubleshooting.TroubleshootingService;
using CDPIUI.Shared.Extentions;
using static System.Windows.Forms.AxHost;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Views.Troubleshooting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>

public enum NavigationParameters
{
    None,
    BeginBasicCheck,
    BeginStoreRepoCheck,
}

public class DiagnosticResultModel
{
    public bool IsHighlighted { get; set; }
    public string CriterionName { get; set; }
    public string CurrentValue { get; set; }
    public string TargetValue { get; set; }
    public string IconGlyph { get; set; }
    public bool IsHelpAvailable { get; set; }
    public bool IsFixAvailable { get; set; }
    public string HelpText { get; set; }
    public string HelpUrl { get; set; }
}

public sealed partial class WorkPage : TemplatePage
{
    private ILocalizer localizer = Localizer.Get();

    private ObservableCollection<DiagnosticResultModel> DisplayDiagnosticResultsCollection = [];
    private ObservableCollection<DiagnosticResultModel> FixResults = [];
    private NavigationParameters navigationParameter = NavigationParameters.None;
    public WorkPage()
    {
        InitializeComponent();

        IsForwardAnimationToPageAvailable = true;
        ElementToAnimateForwardConnectedAnimation = ActionButtonsGrid;

        DiagnosticResultListView.ItemsSource = DisplayDiagnosticResultsCollection;
        FixResultListView.ItemsSource = FixResults;

        TroubleshootingService.Instance.CurrentStateChanged += TroubleshootingHelper_BasicDialogStateChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        try
        {
            PrepareToConnectedBackwardAnimate(ActionButtonsGrid);
        }
        catch { }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (Parameter != null)
        {
            NavigationParameters navp = Parameter.Get("action").ToEnum<NavigationParameters>(NavigationParameters.None);

            navigationParameter = navp;
            switch (navigationParameter)
            {
                case NavigationParameters.BeginBasicCheck:
                    BeginBasicCheck();
                    break;
                case NavigationParameters.BeginStoreRepoCheck:
                    BeginStoreRepoCheck();
                    break;
            }
        }

    }

    private void TroubleshootingHelper_BasicDialogStateChanged(Enum state)
    {
        if (state.ToString() == "Preparing" || state.ToString() == "Completed")
        {
            CurrentStateTextBlock.Text = localizer.GetLocalizedString($"/Troubleshooting/{state}");
        }
        else
        {
            CurrentStateTextBlock.Text = string.Format(localizer.GetLocalizedString("/Troubleshooting/WorkingOn"), localizer.GetLocalizedString($"/Troubleshooting/{state}"));
        }
    }

    private string GetHelpFor(string key)
    {
        string result = localizer.GetLocalizedString($"/Flashlight/{key}");

        return LScriptLangHelper.ExecuteScript(result, scriptArgs: "/Flashlight/");
    }

    private void GetReady()
    {
        LoadingStackPanel.Visibility = Visibility.Collapsed;
        ViewResultsStackPanel.Visibility = Visibility.Collapsed;
        DiagnosticResultStackPanel.Visibility = Visibility.Collapsed;
        FixStackPanel.Visibility = Visibility.Collapsed;
        FixResultStackPanel.Visibility = Visibility.Collapsed;

        DisplayDiagnosticResultsCollection.Clear();
        FixResults.Clear();

        ForwardButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Visible;

        ((App)Application.Current).ShowWindowModalAsync(((App)Application.Current).GetCurrentWindowFromType<TroubleshootingWindow>());
    }

    private void GetReadyForCheck()
    {
        GetReady();
        LoadingStackPanel.Visibility = Visibility.Visible;
    }

    private void GetReadyForFix()
    {
        GetReady();
        FixStackPanel.Visibility = Visibility.Visible;
    }

    private void CheckViewResultActions()
    {
        LoadingStackPanel.Visibility = Visibility.Collapsed;
        ViewResultsStackPanel.Visibility = Visibility.Visible;
        DiagnosticResultStackPanel.Visibility = Visibility.Visible;
    }

    private void CheckCompleteActions()
    {
        ForwardButton.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        ((App)Application.Current).MakeWindowNormal(((App)Application.Current).GetCurrentWindowFromType<TroubleshootingWindow>());
    }

    private void FixViewResultActions()
    {
        FixStackPanel.Visibility = Visibility.Collapsed;
        ViewResultsStackPanel.Visibility = Visibility.Visible;
        FixResultStackPanel.Visibility = Visibility.Visible;
    }

    private void FixCompleteActions()
    {
        ExitButton.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        ((App)Application.Current).MakeWindowNormal(((App)Application.Current).GetCurrentWindowFromType<TroubleshootingWindow>());
    }

    private object DiagnosticResults = null;

    private async void BeginBasicCheck()
    {
        GetReadyForCheck();

        DiagnosticResults = await TroubleshootingService.Instance.RunBasicDiagnostic();

        CheckViewResultActions();

        DisplayDiagnosticResults<RunBasicDialogStates>((Dictionary<RunBasicDialogStates, bool>)DiagnosticResults, TroubleshootingService.BasicDiagnosticStateModelPairs);

        CheckCompleteActions();
    }

    private async void BeginBasicFix()
    {
        GetReadyForFix();

        var result = await TroubleshootingService.Instance.FixAllBasicErrors((Dictionary<RunBasicDialogStates, bool>)DiagnosticResults);

        FixViewResultActions();

        DisplayFixResult<RunBasicDialogStates>(result, BasicDiagnosticStateModelPairs);

        FixCompleteActions();
    }

    private async void BeginStoreRepoCheck()
    {
        GetReadyForCheck();

        DiagnosticResults = await TroubleshootingService.Instance.RunStoreDiagnostic();

        CheckViewResultActions();

        DisplayDiagnosticResults<StoreCheckStates>((Dictionary<StoreCheckStates, bool>)DiagnosticResults, TroubleshootingService.StoreDiagnosticStateModelPairs);

        CheckCompleteActions();
    }

    private async void BeginStoreFix()
    {
        GetReadyForFix();

        var result = await TroubleshootingService.Instance.FixAllStoreErrors((Dictionary<StoreCheckStates, bool>)DiagnosticResults);

        FixViewResultActions();

        DisplayFixResult<StoreCheckStates>(result, StoreDiagnosticStateModelPairs);

        FixCompleteActions();
    }

    private void DisplayDiagnosticResults<T>(Dictionary<T, bool> diagnosticResult, Dictionary<T, DiagnosticStateModel> diagnosticStatesPair) where T : Enum
    {
        bool isErrorHappens = false;
        foreach (var criterion in diagnosticResult)
        {
            bool isCorrect = diagnosticStatesPair.GetValueOrDefault(criterion.Key, null)?.CorrectState == criterion.Value;
            string correctText = diagnosticStatesPair.GetValueOrDefault(criterion.Key, null)?.CorrectValueDisplayText ?? string.Empty;
            string inCorrectText = diagnosticStatesPair.GetValueOrDefault(criterion.Key, null)?.InCorrectValueDisplayText ?? string.Empty;
            DisplayDiagnosticResultsCollection.Add(new()
            {
                IsHighlighted = !isCorrect,
                CriterionName = localizer.GetLocalizedString($"/Troubleshooting/{criterion.Key}"),
                CurrentValue = isCorrect ? localizer.GetLocalizedString($"/Troubleshooting/{correctText}") : localizer.GetLocalizedString($"/Troubleshooting/{inCorrectText}"),
                TargetValue = localizer.GetLocalizedString($"/Troubleshooting/{correctText}"),
                IconGlyph = isCorrect ? "\uE930" : "\uEA39",
                IsFixAvailable = false,
                IsHelpAvailable = true,
                HelpText = GetHelpFor($"{criterion.Key}"),
                HelpUrl = GetUrl(criterion.Key.ToString())
            });

            if (!isErrorHappens) isErrorHappens = !isCorrect;
        }

        if (isErrorHappens)
        {
            ResultTextBlock.Text = localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultErrorTitle");
            ResultTipTextBlock.Text = localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultErrorTip");
        }
        else
        {
            ResultTextBlock.Text = localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultTitle");
            ResultTipTextBlock.Text = localizer.GetLocalizedString("/Troubleshooting/DiagnosticResultTip");

        }
    }

    private void DisplayFixResult<T>(Dictionary<T, FixResultModel> fixResult, Dictionary<T, DiagnosticStateModel> diagnosticStatesPair) where T : Enum
    {
        bool isErrorHappens = false;

        foreach (var crit in fixResult)
        {
            bool isCorrect = crit.Value.IsFixed;
            if (isCorrect) continue;

            string correctText = diagnosticStatesPair.GetValueOrDefault(crit.Key, null)?.CorrectValueDisplayText ?? string.Empty;
            string inCorrectText = diagnosticStatesPair.GetValueOrDefault(crit.Key, null)?.InCorrectValueDisplayText ?? string.Empty;
            FixResults.Add(new()
            {
                IsHighlighted = !isCorrect,
                CriterionName = localizer.GetLocalizedString($"/Troubleshooting/{crit.Key}"),
                CurrentValue = localizer.GetLocalizedString($"/Troubleshooting/{inCorrectText}"),
                TargetValue = localizer.GetLocalizedString($"/Troubleshooting/{correctText}"),
                IconGlyph = isCorrect ? "\uE930" : "\uEA39",
                IsFixAvailable = false,
                IsHelpAvailable = true,
                HelpText = GetHelpFor($"{crit.Key}"),
                HelpUrl = GetUrl(crit.Key.ToString())
            });

            if (!isErrorHappens) isErrorHappens = !isCorrect;
        }


        if (isErrorHappens)
        {
            ResultTextBlock.Text = localizer.GetLocalizedString("/Troubleshooting/FixResultErrorTitle");
            ResultTipTextBlock.Text = localizer.GetLocalizedString("/Troubleshooting/FixResultErrorTip");
        }
        else
        {
            ResultTextBlock.Text = localizer.GetLocalizedString("/Troubleshooting/FixResultTitle");
            ResultTipTextBlock.Text = localizer.GetLocalizedString("/Troubleshooting/FixResultTip");
            FixResultListView.Visibility = Visibility.Collapsed;
        }
    }

    private string GetUrl(string key)
    {
        
        string url = localizer.GetLocalizedString($"/Flashlight/URL_{key}");
        if (string.IsNullOrEmpty(url))
        {
            string name = localizer.GetLocalizedString($"/Troubleshooting/{key}");
            url = string.Format(localizer.GetLocalizedString("/Flashlight/NetSearchPlaceholder"), name);
        }
        if (url == "$EMPTY") return string.Empty;
        return url;
    }

    private void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        Commands.CommandsHandler.HandleCommand(
            "cdpiui://Help/Utils/TroubleshootingUtility/");
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (navigationParameter == NavigationParameters.BeginBasicCheck)
        {
            BeginBasicFix();
        }
        else if (navigationParameter == NavigationParameters.BeginStoreRepoCheck)
        {
            BeginStoreFix();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        (await ((App)Application.Current).SafeCreateNewWindow<TroubleshootingWindow>()).Close();
    }
}
