#nullable enable

using Microsoft.UI.Xaml.Controls;
using WinUI3Localizer;

namespace CDPIUI.Controls.Dialogs.CreateConfigHelper;

public sealed partial class ConfigMakerSavePresetContentDialog : ContentDialog
{
    public ConfigMakerSavePresetContentDialog(string suggestedName = "")
    {
        InitializeComponent();
        ILocalizer localizer = Localizer.Get();
        Title = localizer.GetLocalizedString("ConfigMakerSavePresetDialogTitle");
        PrimaryButtonText = localizer.GetLocalizedString("Save");
        CloseButtonText = localizer.GetLocalizedString("Cancel");
        DefaultButton = ContentDialogButton.Primary;
        PresetNameTextBox.Text = suggestedName ?? string.Empty;
        UpdatePrimaryButton();
    }

    public string PresetName { get; private set; } = string.Empty;

    private void PresetNameTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdatePrimaryButton();

    private void UpdatePrimaryButton() =>
        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(PresetNameTextBox.Text);

    private void ContentDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        PresetName = PresetNameTextBox.Text.Trim();
        args.Cancel = PresetName.Length == 0;
    }
}
