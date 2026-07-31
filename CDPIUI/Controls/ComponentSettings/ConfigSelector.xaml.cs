using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        SelectionChanged?.Invoke(this, e);
    }
}
