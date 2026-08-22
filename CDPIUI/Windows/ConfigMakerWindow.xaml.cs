using CDPIUI.Default;
using CDPIUI.Helper.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using WinUI3Localizer;

namespace CDPIUI;

public sealed partial class ConfigMakerWindow : TemplateWindow
{
    private bool componentSelectionRequested;

    public ConfigMakerWindow()
    {
        InitializeComponent();

        WindowTitle = Localizer.Get().GetLocalizedString("ConfigMakerWindowTitle");
        IconUri = @"Assets/Icons/Edit.png";
        CustomTitleBarUserControl = TitleBarUserControl;
        WindowMinSize = new System.Windows.Size(960, 620);
        Closed += ConfigMakerWindow_Closed;
    }

    public void OpenComponent(string componentId, string commandText = "")
    {
        ConfigMaker.ComponentId = componentId ?? string.Empty;
        ConfigMaker.CommandText = commandText ?? string.Empty;
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (componentSelectionRequested || !string.IsNullOrWhiteSpace(ConfigMaker.ComponentId))
        {
            return;
        }

        componentSelectionRequested = true;
        IReadOnlyList<ConfigMakerComponentInfo> components =
            ConfigMakerComponentCatalog.GetAvailableComponents();
        ILocalizer localizer = Localizer.Get();
        if (components.Count == 0)
        {
            ContentDialog unavailableDialog = new()
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = localizer.GetLocalizedString("ConfigMakerSelectComponentDialogTitle"),
                Content = localizer.GetLocalizedString("ConfigMakerNoComponentsDialogMessage"),
                CloseButtonText = localizer.GetLocalizedString("Cancel"),
            };
            await unavailableDialog.ShowAsync();
            Close();
            return;
        }

        ComboBox componentComboBox = new()
        {
            Header = localizer.GetLocalizedString("ConfigMakerComponentComboBox.Header"),
            ItemsSource = components,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 380,
        };
        TextBlock executableTextBlock = new()
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        StackPanel dialogContent = new()
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = localizer.GetLocalizedString("ConfigMakerSelectComponentDialogMessage"),
                    TextWrapping = TextWrapping.Wrap,
                },
                componentComboBox,
                executableTextBlock,
            },
        };
        ContentDialog selectionDialog = new()
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = localizer.GetLocalizedString("ConfigMakerSelectComponentDialogTitle"),
            Content = dialogContent,
            PrimaryButtonText = localizer.GetLocalizedString("Continue"),
            CloseButtonText = localizer.GetLocalizedString("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };
        componentComboBox.SelectionChanged += (_, _) =>
        {
            ConfigMakerComponentInfo selected = componentComboBox.SelectedItem as ConfigMakerComponentInfo;
            executableTextBlock.Text = selected?.ExecutableName ?? string.Empty;
            selectionDialog.IsPrimaryButtonEnabled = selected != null;
        };

        if (await selectionDialog.ShowAsync() != ContentDialogResult.Primary ||
            componentComboBox.SelectedItem is not ConfigMakerComponentInfo component)
        {
            Close();
            return;
        }

        await ConfigMaker.SetComponentAsync(component.Id);
    }

    private async void ConfigMakerWindow_Closed(object sender, WindowEventArgs args)
    {
        Closed -= ConfigMakerWindow_Closed;
        await ConfigMaker.StopTestAsync();
    }

    private void ActionMenu_CloseRequested(object sender, System.EventArgs e) => Close();
}
