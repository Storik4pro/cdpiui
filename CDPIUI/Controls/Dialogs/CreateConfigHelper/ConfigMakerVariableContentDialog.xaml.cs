#nullable enable

using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using WinUI3Localizer;

namespace CDPIUI.Controls.Dialogs.CreateConfigHelper;

public sealed partial class ConfigMakerVariableContentDialog : ContentDialog
{
    private readonly ConfigMakerPresetDocument document;
    private readonly ConfigMakerVariableDefinition? source;
    private readonly ILocalizer localizer = Localizer.Get();

    public ConfigMakerVariableContentDialog(
        ConfigMakerPresetDocument document,
        ConfigMakerVariableDefinition? variable = null)
    {
        this.document = document ?? throw new ArgumentNullException(nameof(document));
        source = variable;
        InitializeComponent();
        Title = localizer.GetLocalizedString(variable == null
            ? "ConfigMakerCreateVariableDialogTitle"
            : "ConfigMakerEditVariableDialogTitle");
        PrimaryButtonText = localizer.GetLocalizedString("Save");
        CloseButtonText = localizer.GetLocalizedString("Cancel");
        DefaultButton = ContentDialogButton.Primary;
        LoadSource();
        ValidateInput();
    }

    public ConfigMakerVariableDefinition? ResultVariable { get; private set; }
    public string OriginalName => source?.Name ?? string.Empty;

    private ConfigMakerVariableKind SelectedKind =>
        Enum.TryParse(
            (KindComboBox.SelectedItem as ComboBoxItem)?.Tag as string,
            ignoreCase: true,
            out ConfigMakerVariableKind kind)
            ? kind
            : ConfigMakerVariableKind.Text;

    private void LoadSource()
    {
        ConfigMakerVariableDefinition variable = source ?? new ConfigMakerVariableDefinition
        {
            Kind = ConfigMakerVariableKind.Text,
            StorageKind = ConfigMakerVariableStorageKind.Direct,
        };
        NameTextBox.Text = variable.Name;
        DescriptionTextBox.Text = variable.Description;
        ValueTextBox.Text = variable.Value;
        ValuesTextBox.Text = string.Join(Environment.NewLine, variable.Values);
        OnValueTextBox.Text = variable.OnValue;
        OffValueTextBox.Text = variable.OffValue;
        DefaultSwitch.IsOn = variable.IsSwitchEnabled;
        KindComboBox.SelectedIndex = variable.Kind switch
        {
            ConfigMakerVariableKind.Choice => 1,
            ConfigMakerVariableKind.Switch => 2,
            _ => 0,
        };
        UpdateKindPanels();
    }

    private void KindComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateKindPanels();
        ValidateInput();
    }

    private void UpdateKindPanels()
    {
        bool isSwitch = SelectedKind == ConfigMakerVariableKind.Switch;
        DirectValuePanel.Visibility = isSwitch ? Visibility.Collapsed : Visibility.Visible;
        SwitchValuePanel.Visibility = isSwitch ? Visibility.Visible : Visibility.Collapsed;
        ValuesTextBox.Visibility = SelectedKind == ConfigMakerVariableKind.Choice
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => ValidateInput();

    private bool ValidateInput()
    {
        string message = string.Empty;
        string name = NameTextBox.Text.Trim();
        if (!ConfigMakerPresetDocument.IsValidVariableName(name))
        {
            message = localizer.GetLocalizedString("ConfigMakerVariableInvalidNameMessage");
        }
        else if (document.ContainsVariable(name, source?.Id))
        {
            message = localizer.GetLocalizedString("ConfigMakerVariableDuplicateNameMessage");
        }
        else if (SelectedKind == ConfigMakerVariableKind.Switch &&
                 (string.IsNullOrWhiteSpace(OnValueTextBox.Text) ||
                  string.IsNullOrWhiteSpace(OffValueTextBox.Text)))
        {
            message = localizer.GetLocalizedString("ConfigMakerVariableSwitchValuesRequiredMessage");
        }
        else if (SelectedKind == ConfigMakerVariableKind.Choice && ParseValues().Count == 0)
        {
            message = localizer.GetLocalizedString("ConfigMakerVariableChoiceValuesRequiredMessage");
        }

        IsPrimaryButtonEnabled = message.Length == 0;
        ValidationTextBlock.Text = message;
        ValidationTextBlock.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        return message.Length == 0;
    }

    private List<string> ParseValues() => ValuesTextBox.Text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    private void ContentDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        if (!ValidateInput())
        {
            args.Cancel = true;
            return;
        }

        ConfigMakerVariableDefinition variable = new()
        {
            Id = source?.Id ?? Guid.NewGuid().ToString("N"),
            Name = NameTextBox.Text.Trim(),
            Kind = SelectedKind,
            StorageKind = SelectedKind == ConfigMakerVariableKind.Switch
                ? ConfigMakerVariableStorageKind.Conditional
                : source?.StorageKind == ConfigMakerVariableStorageKind.Expression
                    ? ConfigMakerVariableStorageKind.Expression
                    : ConfigMakerVariableStorageKind.Direct,
            Value = ValueTextBox.Text,
            Description = DescriptionTextBox.Text.Trim(),
            OnValue = OnValueTextBox.Text,
            OffValue = OffValueTextBox.Text,
            IsSwitchEnabled = DefaultSwitch.IsOn,
            InternalParameterName = SelectedKind == ConfigMakerVariableKind.Switch
                ? source?.InternalParameterName ?? CreateParameterName(NameTextBox.Text.Trim())
                : string.Empty,
        };
        foreach (string value in ParseValues())
        {
            variable.Values.Add(value);
        }
        if (variable.Kind == ConfigMakerVariableKind.Choice &&
            !variable.Values.Contains(variable.Value, StringComparer.Ordinal))
        {
            variable.Value = variable.Values[0];
        }
        ResultVariable = variable;
    }

    private static string CreateParameterName(string name) =>
        $"{name}_var_{Guid.NewGuid().ToString("N")[..8]}";
}
