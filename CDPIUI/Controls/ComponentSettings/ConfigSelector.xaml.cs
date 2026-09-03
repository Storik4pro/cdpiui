using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace CDPIUI.Controls.ComponentSettings;

public sealed class ConfigSelectorItem
{
    public string FileName { get; set; } = string.Empty;

    public string PackId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PackDisplayName { get; set; } = string.Empty;

    public bool IsLegacyConfig { get; set; }

    public Visibility LegacyVisibility =>
        IsLegacyConfig ? Visibility.Visible : Visibility.Collapsed;
}

public sealed partial class ConfigSelector : UserControl
{
    public ConfigSelector()
    {
        InitializeComponent();
    }

    public object ItemsSource
    {
        get => Selector.ItemsSource;
        set => Selector.ItemsSource = value;
    }

    public object SelectedItem
    {
        get => Selector.SelectedItem;
        set => Selector.SelectedItem = value;
    }

    public event SelectionChangedEventHandler SelectionChanged;

    private void Selector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShareButton.Visibility = Selector.SelectedItem is ConfigSelectorItem ? Visibility.Visible : Visibility.Collapsed;
        SelectionChanged?.Invoke(this, e);
    }

    private async void ShareButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is not ConfigSelectorItem selected) return;
        ShareButton.IsEnabled = false;
        try { await Helper.AddOns.ConfigShare.ConfigShareUI.ShowExportAsync(XamlRoot, selected); }
        catch (Exception exception)
        {
            Core.Basic.Logger.Instance.CreateWarningLog(nameof(ConfigSelector), exception.ToString());
            var error = new Dialogs.ConfigShare.ConfigShareMessageContentDialog
            {
                XamlRoot = XamlRoot, Title = Helper.AddOns.ConfigShare.ConfigShareUI.Text("ConfigShareError"),
                Message = Helper.AddOns.ConfigShare.ConfigShareUI.ErrorText(exception)
            };
            await error.ShowAsync();
        }
        finally { ShareButton.IsEnabled = true; }
    }
}
