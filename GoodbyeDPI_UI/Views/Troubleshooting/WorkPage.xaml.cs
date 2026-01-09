using CDPI_UI.Helper.Troubleshooting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static CDPI_UI.Helper.Troubleshooting.TroubleshootingHelper;
using static System.Windows.Forms.AxHost;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Troubleshooting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>

public enum NavigationParameters
{
    None,
    BeginBasicCheck,
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

public sealed partial class WorkPage : Page
{
    private ILocalizer localizer = Localizer.Get();

    private ObservableCollection<DiagnosticResultModel> DiagnosticResults = [];
    private ObservableCollection<DiagnosticResultModel> FixResults = [];
    private NavigationParameters navigationParameter = NavigationParameters.None;
    public WorkPage()
    {
        InitializeComponent();

        DiagnosticResultListView.ItemsSource = DiagnosticResults;
        FixResultListView.ItemsSource = FixResults;

        TroubleshootingHelper.Instance.BasicDialogStateChanged += TroubleshootingHelper_BasicDialogStateChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is NavigationParameters parameter)
        {
            navigationParameter = parameter;
            switch (parameter)
            {
                case NavigationParameters.BeginBasicCheck:
                    BeginBasicCheck();
                    break;
            }
        }
    }

    private void TroubleshootingHelper_BasicDialogStateChanged(TroubleshootingHelper.RunBasicDialogStates state)
    {
        if (state == TroubleshootingHelper.RunBasicDialogStates.Preparing || state == TroubleshootingHelper.RunBasicDialogStates.Completed)
        {
            CurrentStateTextBlock.Text = localizer.GetLocalizedString($"/Troubleshooting/{state}");
        }
        else
        {
            CurrentStateTextBlock.Text = string.Format(localizer.GetLocalizedString("/Troubleshooting/WorkingOn"), localizer.GetLocalizedString($"/Troubleshooting/{state}"));
        }
    }

    private Dictionary<RunBasicDialogStates, bool> BasicDiagnosticResults = [];

    private async void BeginBasicCheck()
    {
        LoadingStackPanel.Visibility = Visibility.Visible;
        ViewResultsStackPanel.Visibility = Visibility.Collapsed;
        DiagnosticResultStackPanel.Visibility = Visibility.Collapsed;
        FixStackPanel.Visibility = Visibility.Collapsed;

        DiagnosticResults.Clear();

        BasicDiagnosticResults = await TroubleshootingHelper.Instance.RunBasicDiagnostic();

        LoadingStackPanel.Visibility = Visibility.Collapsed;
        ViewResultsStackPanel.Visibility = Visibility.Visible;
        DiagnosticResultStackPanel.Visibility = Visibility.Visible;

        bool isErrorHappens = false;

        foreach (var crit in BasicDiagnosticResults)
        {
            bool isCorrect = TroubleshootingHelper.BasicDiagnosticStateModelPairs.GetValueOrDefault(crit.Key, null)?.CorrectState == crit.Value;
            string correctText = TroubleshootingHelper.BasicDiagnosticStateModelPairs.GetValueOrDefault(crit.Key, null)?.CorrectValueDisplayText ?? string.Empty;
            string inCorrectText = TroubleshootingHelper.BasicDiagnosticStateModelPairs.GetValueOrDefault(crit.Key, null)?.InCorrectValueDisplayText ?? string.Empty;
            DiagnosticResults.Add(new()
            {
                IsHighlighted = !isCorrect,
                CriterionName = localizer.GetLocalizedString($"/Troubleshooting/{crit.Key}"),
                CurrentValue = isCorrect ? localizer.GetLocalizedString($"/Troubleshooting/{correctText}") : localizer.GetLocalizedString($"/Troubleshooting/{inCorrectText}"),
                TargetValue = localizer.GetLocalizedString($"/Troubleshooting/{correctText}"),
                IconGlyph = isCorrect ? "\uE930" : "\uEA39",
                IsFixAvailable = false,
                IsHelpAvailable = true,
                HelpText = localizer.GetLocalizedString($"/Flashlight/{crit.Key}"),
                HelpUrl = GetUrl(crit.Key.ToString())
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

        ForwardButton.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
    }

    private async void BeginBasicFix()
    {
        LoadingStackPanel.Visibility = Visibility.Collapsed;
        ViewResultsStackPanel.Visibility = Visibility.Collapsed;
        DiagnosticResultStackPanel.Visibility = Visibility.Collapsed;
        FixStackPanel.Visibility = Visibility.Visible;
        FixResultStackPanel.Visibility = Visibility.Collapsed;

        FixResults.Clear();

        ForwardButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Visible;

        var result = await TroubleshootingHelper.Instance.FixAllBasicErrors(BasicDiagnosticResults);

        FixStackPanel.Visibility = Visibility.Collapsed;
        ViewResultsStackPanel.Visibility = Visibility.Visible;
        FixResultStackPanel.Visibility = Visibility.Visible;

        bool isErrorHappens = false;

        foreach (var crit in result)
        {
            bool isCorrect = crit.Value.IsFixed;
            if (isCorrect) continue;

            string correctText = TroubleshootingHelper.BasicDiagnosticStateModelPairs.GetValueOrDefault(crit.Key, null)?.CorrectValueDisplayText ?? string.Empty;
            string inCorrectText = TroubleshootingHelper.BasicDiagnosticStateModelPairs.GetValueOrDefault(crit.Key, null)?.InCorrectValueDisplayText ?? string.Empty;
            FixResults.Add(new()
            {
                IsHighlighted = !isCorrect,
                CriterionName = localizer.GetLocalizedString($"/Troubleshooting/{crit.Key}"),
                CurrentValue = localizer.GetLocalizedString($"/Troubleshooting/{inCorrectText}"),
                TargetValue = localizer.GetLocalizedString($"/Troubleshooting/{correctText}"),
                IconGlyph = isCorrect ? "\uE930" : "\uEA39",
                IsFixAvailable = false,
                IsHelpAvailable = true,
                HelpText = localizer.GetLocalizedString($"/Flashlight/{crit.Key}"),
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
        }

        
        ExitButton.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
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

    private async void GetHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var window = await((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
        window.NavigateToPage("/Utils/TroubleshootingUtility");
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (navigationParameter == NavigationParameters.BeginBasicCheck)
        {
            BeginBasicFix();
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
