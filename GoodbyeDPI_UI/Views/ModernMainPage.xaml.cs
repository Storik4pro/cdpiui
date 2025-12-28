using CDPI_UI.Helper;
using CDPI_UI.Helper.LScript;
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.StartScreen;
using WinRT.Interop;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public class ComponentTileModel
{
    public string Id;
    public string ImageSource;
    public double Width;
    public double Height;
}

public sealed partial class ModernMainPage : Page
{
    public ObservableCollection<ComponentTileModel> TileModels = [];

    public double ElementWidth { get; set; } = 210;
    public double ElementHeight { get; set; } = 250;
    public double Spacing { get; set; } = 0;

    private ILocalizer localizer = Localizer.Get();
    public ModernMainPage()
    {
        InitializeComponent();
        this.DataContext = this;

        StoreHelper.Instance.ItemActionsStopped += Instance_ItemActionsStopped;
        StoreHelper.Instance.ItemRemoved += Instance_ItemRemoved;

        LearnMoreAboutUIHyperlink.Content = localizer.GetLocalizedString("/Help/LearnMoreAboutUI");
        FirstStepsHyperlink.Content = localizer.GetLocalizedString("/Help/FirstSteps");
        AddingCustomSiteListsToConfigHyperlink.Content = localizer.GetLocalizedString("/Help/AddingCustomSiteListsToConfig");

        this.Loaded += ModernMainPage_Loaded;
    }

    private void Instance_ItemRemoved(string obj)
    {
        CreateTiles();
    }

    private void Instance_ItemActionsStopped(string obj)
    {
        CreateTiles();
    }

    private void CreateTiles()
    {
        TileModels.Clear();
        List<DatabaseStoreItem> installedComponents = DatabaseHelper.Instance.GetItemsByType("component");
        CalcWidth(SettingsManager.Instance.GetValue<int>("APPEARANCE", "mainGridColumnsCount"), installedComponents.Count);
        foreach (DatabaseStoreItem installedComponent in installedComponents)
        {
            TileModels.Add(new ComponentTileModel()
            {
                Id = installedComponent.Id,
                ImageSource = LScriptLangHelper.ExecuteScript(installedComponent.IconPath),
                Width = ElementWidth,
                Height = ElementHeight,
            });
        }
        AuditMarkup();
    }

    private void ModernMainPage_Loaded(object sender, RoutedEventArgs e)
    {
        MainGridView.ItemsSource = TileModels;
        CreateTiles();
        
    }

    private void CalcWidth(int columns, int elCount)
    {
        double pageWidth = this.ActualWidth;
        
        if (columns < 0)
        {
            if (elCount == 1)
            {
                columns = 1;
            }
            else if (elCount == 2)
            {
                columns = 2;
            }
            else
            {
                columns = 4;
            }
        }
        ElementWidth = (pageWidth - 40 - (10 * (columns - 1))) / columns;
        Spacing = columns != 1 ? 10 : 0;
    }

    private void AuditMarkup()
    {
        if (TileModels.Count == 0)
        {
            ComponentTilePlaceholder.Visibility = Visibility.Visible;
            ComponentTilesScrollContainer.Visibility = Visibility.Collapsed;
        }
        else
        {
            ComponentTilePlaceholder.Visibility = Visibility.Collapsed;
            ComponentTilesScrollContainer.Visibility = Visibility.Visible;
        }
    }

    private async void ShowDialog(string message, string title)
    {
        var dlg = new MessageDialog(message, title);
        InitializeWithWindow.Initialize(dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>()));
        await dlg.ShowAsync();
    }

    private void ApplicationSetupHelperButton_Click(object sender, RoutedEventArgs e)
    {
        ShowDialog(localizer.GetLocalizedString("PreviewVersionDescription"), localizer.GetLocalizedString("PreviewVersion"));
    }

    private async void GetNewComponentsFromStoreButton_Click(object sender, RoutedEventArgs e)
    {
        await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
    }

    private async void OpenStoreButton_Click(object sender, RoutedEventArgs e)
    {
        await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
    }

    private async void OpenHelpButton_Click(object sender, RoutedEventArgs e)
    {
        await ((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
    }

    private void AskCommunityButton_Click(object sender, RoutedEventArgs e)
    {
        UrlOpenHelper.LaunchTelegramUrl();
    }

    private void ReportProblemButton_Click(object sender, RoutedEventArgs e)
    {
        UrlOpenHelper.LaunchReportUrl();
    }

    private async void NavigateToHelpUri(string uri)
    {
        OfflineHelpWindow window = await ((App)Application.Current).SafeCreateNewWindow<OfflineHelpWindow>();
        window.NavigateToPage(uri);
    }

    private void LearnMoreAboutUIHyperlink_Click(object sender, RoutedEventArgs e)
    {
        NavigateToHelpUri("/GettingStarted/LearnMoreAboutUI");
    }

    private void FirstStepsHyperlink_Click(object sender, RoutedEventArgs e)
    {
        NavigateToHelpUri("/GettingStarted/FirstSteps");
    }

    private void AddingCustomSiteListsToConfigHyperlink_Click(object sender, RoutedEventArgs e)
    {
        NavigateToHelpUri("/GettingStarted/AddingCustomSiteListsToConfig");
    }
}
