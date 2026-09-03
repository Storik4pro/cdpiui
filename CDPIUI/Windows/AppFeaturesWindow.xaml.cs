using CDPIUI.Controls.Navigation;
using CDPIUI.Default;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class AppFeaturesWindow : TemplateWindow
{
    private readonly ILocalizer localizer = Localizer.Get();
    private bool isReloading;

    public ObservableCollection<AppFeatureViewModel> Features { get; } = [];

    public AppFeaturesWindow()
    {
        InitializeComponent();

        WindowTitle = localizer.GetLocalizedString("AppFeaturesWindowTitle");
        IconUri = @"Assets/favicon.ico";
        CustomTitleBarUserControl = TitleBarUserControl;
        DisableResizeFeature();

        ReloadFeatures();

        localizer.LanguageChanged += Localizer_LanguageChanged;
        Closed += AppFeaturesWindow_Closed;
    }

    private void ReloadFeatures()
    {
        int selectedIndex = Math.Max(FeatureListView.SelectedIndex, 0);
        var localizedFeatures = AppFeaturesCatalog.CreateLocalized(localizer);
        DataTemplate contentTemplate = (DataTemplate)RootGrid.Resources["FeatureContentTemplate"];

        isReloading = true;
        Features.Clear();
        FeatureContentViewer.Items.Clear();

        foreach (var feature in localizedFeatures)
        {
            Features.Add(feature);
            FeatureContentViewer.Items.Add(new AnimatedHorizontalContentItem
            {
                Content = new ContentControl
                {
                    Content = feature,
                    ContentTemplate = contentTemplate,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch
                }
            });
        }

        selectedIndex = Math.Min(selectedIndex, Features.Count - 1);
        FeatureListView.SelectedIndex = selectedIndex;
        isReloading = false;

        if (FeatureContentViewer.IsLoaded && selectedIndex >= 0)
            FeatureContentViewer.GoTo(selectedIndex);
    }

    private void FeatureListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int selectedIndex = FeatureListView.SelectedIndex;
        if (isReloading || selectedIndex < 0 || selectedIndex >= FeatureContentViewer.Items.Count)
            return;

        FeatureContentViewer.GoTo(selectedIndex);
    }

    private void Localizer_LanguageChanged(object sender, LanguageChangedEventArgs e)
    {
        WindowTitle = localizer.GetLocalizedString("AppFeaturesWindowTitle");
        ReloadFeatures();
    }

    private void AppFeaturesWindow_Closed(object sender, WindowEventArgs args)
    {
        localizer.LanguageChanged -= Localizer_LanguageChanged;
        Closed -= AppFeaturesWindow_Closed;
    }
}
