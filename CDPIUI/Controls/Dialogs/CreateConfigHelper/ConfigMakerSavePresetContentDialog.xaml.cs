#nullable enable

using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.Controls.Dialogs.CreateConfigHelper;

public enum ConfigMakerSavePresetContentDialogResult
{
    SaveAsNew,
    Overwrite,
    Cancel
}

public sealed partial class ConfigMakerSavePresetContentDialog : ContentDialog
{
    private bool overwriteEnable = true;

    public ConfigMakerSavePresetContentDialogResult Result = ConfigMakerSavePresetContentDialogResult.Cancel;

    public bool OverwriteEnable
    {
        get => overwriteEnable;
        set
        {
            overwriteEnable = value;
            SaveOverwriteItem.IsEnabled = value;
            if (!value && SelectorBar.SelectedItem == SaveOverwriteItem)
            {
                SelectorBar.SelectIndex(0);
            }
            UpdatePrimaryButton();
        }
    }

    public ConfigMakerSavePresetContentDialog(string suggestedName = "")
    {
        InitializeComponent();
        ILocalizer localizer = Localizer.Get();
        Title = localizer.GetLocalizedString("ConfigMakerSavePresetDialogTitle");
        PrimaryButtonText = localizer.GetLocalizedString("Save");
        CloseButtonText = localizer.GetLocalizedString("Cancel");
        DefaultButton = ContentDialogButton.Primary;
        PresetNameTextBox.Text = suggestedName ?? string.Empty;
        SelectorBar.SelectIndex(0);
        UpdatePrimaryButton();
    }

    public string PresetName { get; private set; } = string.Empty;

    private void PresetNameTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdatePrimaryButton();

    private void SelectorBar_SelectionChanged(object sender, Navigation.AnimatedSelectorBarSelectionChangedEventArgs e) =>
        UpdatePrimaryButton();

    private void UpdatePrimaryButton()
    {
        IsPrimaryButtonEnabled = SelectorBar.SelectedItem == SaveAsNewItem ? !string.IsNullOrWhiteSpace(PresetNameTextBox.Text) : true;
    }

    private void ContentDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        Result = SelectorBar.SelectedItem == SaveAsNewItem
            ? ConfigMakerSavePresetContentDialogResult.SaveAsNew
            : ConfigMakerSavePresetContentDialogResult.Overwrite;
        PresetName = PresetNameTextBox.Text.Trim();
        args.Cancel = Result == ConfigMakerSavePresetContentDialogResult.SaveAsNew &&
                      PresetName.Length == 0;
    }
}
