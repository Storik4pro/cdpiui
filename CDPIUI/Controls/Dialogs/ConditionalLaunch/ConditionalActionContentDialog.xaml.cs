using CDPIUI.ConditionalLaunch;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper;
using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using WinUI3Localizer;

namespace CDPIUI.Controls.Dialogs.ConditionalLaunch
{
    public sealed partial class ConditionalActionContentDialog : ContentDialog
    {
        public ConditionalAction? ResultAction { get; private set; }

        private readonly ILocalizer _localizer = Localizer.Get();
        private readonly List<ConditionalActionDefinition> _definitions;
        private readonly List<ConditionalParameterChoice> _componentChoices = [];
        private readonly List<ConditionalParameterChoice> _configKitChoices = [];
        private readonly Dictionary<string, FrameworkElement> _parameterControls =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ConditionalParameterDefinition> _parameterDefinitions =
            new(StringComparer.OrdinalIgnoreCase);
        private ConditionalAction? _existingAction;
        private bool _loading;

        public ConditionalActionContentDialog(ConditionalAction? action)
        {
            InitializeComponent();
            _definitions = ConditionalLaunchUiCatalog.CreateActionDefinitions(_localizer);
            LoadInstalledComponents(action?.GetParameter("componentId"));
            LoadInstalledConfigKits(action?.GetParameter("kitId"));
            ActionTypeComboBox.ItemsSource = _definitions;

            Title = Text(action == null ? "CL_AddActionDialogTitle" : "CL_EditActionDialogTitle");
            PrimaryButtonText = Text("CL_OkButtonText");
            CloseButtonText = Text("CL_CancelButtonText");
            DefaultButton = ContentDialogButton.Primary;

            _existingAction = action;
            if (action != null)
                ActionTypeComboBox.SelectedItem = _definitions.First(item => item.Type == action.Type);
        }

        private void LoadInstalledComponents(string? existingComponentId)
        {
            ObservableCollection<ViewComponentModel> components = [];
            UIHelper.LoadInstalledComponentsList(components);
            _componentChoices.AddRange(components.Select(component =>
                new ConditionalParameterChoice(component.DisplayName, component.StoreId)));

            if (!string.IsNullOrWhiteSpace(existingComponentId) &&
                !_componentChoices.Any(item => string.Equals(
                    item.Value, existingComponentId, StringComparison.OrdinalIgnoreCase)))
            {
                _componentChoices.Add(new ConditionalParameterChoice(existingComponentId, existingComponentId));
            }
        }

        private void LoadInstalledConfigKits(string? existingKitId)
        {
            try
            {
                _configKitChoices.AddRange(DatabaseHelper.Instance
                    .GetItemsByType("configlist")
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                    .Select(item => new ConditionalParameterChoice(
                        item.ShortName ?? item.Id!,
                        item.Id!)));
            }
            catch
            {
                // pass
            }

            if (!string.IsNullOrWhiteSpace(existingKitId) &&
                !_configKitChoices.Any(item => string.Equals(
                    item.Value, existingKitId, StringComparison.OrdinalIgnoreCase)))
            {
                _configKitChoices.Add(new ConditionalParameterChoice(
                    ResolveStoreItemName(existingKitId),
                    existingKitId));
            }
        }

        private void ActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || ActionTypeComboBox.SelectedItem is not ConditionalActionDefinition definition)
                return;

            var existing = _existingAction?.Type == definition.Type ? _existingAction : null;
            BuildParameterEditor(definition, existing);
            _existingAction = null;
        }

        private void BuildParameterEditor(
            ConditionalActionDefinition definition,
            ConditionalAction? existingAction)
        {
            _loading = true;
            ActionParametersPanel.Children.Clear();
            _parameterControls.Clear();
            _parameterDefinitions.Clear();

            if (definition.Type == ConditionalActionType.ApplyPreset)
            {
                BuildPresetEditor(existingAction);
                _loading = false;
                return;
            }

            foreach (var parameter in definition.Parameters)
            {
                var control = CreateParameterControl(parameter, existingAction?.GetParameter(parameter.Name));
                _parameterControls[parameter.Name] = control;
                _parameterDefinitions[parameter.Name] = parameter;
                ActionParametersPanel.Children.Add(control);

                if (parameter.Name == "target" && control is ComboBox targetComboBox)
                    targetComboBox.SelectionChanged += TargetComboBox_SelectionChanged;
            }

            UpdateParameterVisibility();
            _loading = false;
        }

        private FrameworkElement CreateParameterControl(
            ConditionalParameterDefinition parameter,
            string? existingValue)
        {
            IReadOnlyList<ConditionalParameterChoice> choices = parameter.UseInstalledComponentSelector
                ? _componentChoices
                : parameter.UseInstalledConfigKitSelector
                    ? _configKitChoices
                    : parameter.Choices;

            if (choices.Count > 0 || parameter.UseInstalledComponentSelector ||
                parameter.UseInstalledConfigKitSelector)
            {
                ComboBox comboBox = new()
                {
                    Header = parameter.Label,
                    ItemsSource = choices,
                    DisplayMemberPath = nameof(ConditionalParameterChoice.Name),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = parameter.Name
                };
                comboBox.SelectedItem = choices.FirstOrDefault(choice =>
                    string.Equals(choice.Value, existingValue, StringComparison.OrdinalIgnoreCase))
                    ?? choices.FirstOrDefault();
                return comboBox;
            }

            if (parameter.IsNumber)
            {
                NumberBox numberBox = new()
                {
                    Header = parameter.Label,
                    Minimum = parameter.Minimum,
                    Maximum = parameter.Maximum,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                    Tag = parameter.Name
                };
                numberBox.Value = double.TryParse(existingValue ?? parameter.DefaultValue, out var number)
                    ? number
                    : parameter.Minimum;
                return numberBox;
            }

            return new TextBox
            {
                Header = parameter.Label,
                Text = existingValue ?? parameter.DefaultValue ?? string.Empty,
                Tag = parameter.Name
            };
        }

        private void BuildPresetEditor(ConditionalAction? existingAction)
        {
            ComboBox componentComboBox = new()
            {
                Header = Text("CL_ParameterComponent"),
                ItemsSource = _componentChoices,
                DisplayMemberPath = nameof(ConditionalParameterChoice.Name),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = "componentId"
            };
            var componentId = existingAction?.GetParameter("componentId");
            componentComboBox.SelectedItem = _componentChoices.FirstOrDefault(choice =>
                string.Equals(choice.Value, componentId, StringComparison.OrdinalIgnoreCase))
                ?? _componentChoices.FirstOrDefault();

            ComboBox presetComboBox = new()
            {
                Header = Text("CL_ParameterPreset"),
                ItemTemplate = (DataTemplate)Resources["ConditionalPresetChoiceTemplate"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = "preset",
                Height = 80
            };

            _parameterControls["componentId"] = componentComboBox;
            _parameterControls["preset"] = presetComboBox;
            ActionParametersPanel.Children.Add(componentComboBox);
            ActionParametersPanel.Children.Add(presetComboBox);

            componentComboBox.SelectionChanged += (_, _) => LoadPresetChoices(
                componentComboBox,
                presetComboBox,
                null,
                null);
            LoadPresetChoices(
                componentComboBox,
                presetComboBox,
                existingAction?.GetParameter("packId"),
                existingAction?.GetParameter("fileName"));
        }

        private static List<PresetChoice> GetPresetChoices(string componentId)
        {
            try
            {
                ComponentItemsLoaderHelper.Instance.Init();
                var componentHelper = ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(componentId);
                var configuration = componentHelper?.GetConfigHelper();
                if (configuration == null)
                    return [];

                return configuration.GetConfigItems()
                    .Where(item => item != null && !item.MarkAsRemoved)
                    .Select(item => new PresetChoice(
                        item.name ?? item.not_converted_name ?? item.file_name ?? item.packId ?? string.Empty,
                        ResolveStoreItemName(item.packId),
                        item.packId ?? string.Empty,
                        item.file_name ?? string.Empty))
                    .Where(item => !string.IsNullOrWhiteSpace(item.FileName))
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        private static void LoadPresetChoices(
            ComboBox componentComboBox,
            ComboBox presetComboBox,
            string? selectedPackId,
            string? selectedFileName)
        {
            var componentId = (componentComboBox.SelectedItem as ConditionalParameterChoice)?.Value;
            var presets = string.IsNullOrWhiteSpace(componentId)
                ? []
                : GetPresetChoices(componentId);

            if (!string.IsNullOrWhiteSpace(selectedFileName) &&
                !presets.Any(item =>
                    string.Equals(item.PackId, selectedPackId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.FileName, selectedFileName, StringComparison.OrdinalIgnoreCase)))
            {
                presets.Add(new PresetChoice(
                    selectedFileName,
                    ResolveStoreItemName(selectedPackId),
                    selectedPackId ?? string.Empty,
                    selectedFileName));
            }

            presetComboBox.ItemsSource = presets;
            presetComboBox.SelectedItem = presets.FirstOrDefault(item =>
                string.Equals(item.PackId, selectedPackId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.FileName, selectedFileName, StringComparison.OrdinalIgnoreCase))
                ?? presets.FirstOrDefault();
        }

        private void TargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loading)
                UpdateParameterVisibility();
        }

        private void UpdateParameterVisibility()
        {
            var values = ReadParameterValues();
            foreach (var pair in _parameterControls)
            {
                if (_parameterDefinitions.TryGetValue(pair.Key, out var definition))
                {
                    pair.Value.Visibility = definition.IsVisible(values)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

        private Dictionary<string, string> ReadParameterValues()
        {
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _parameterControls)
            {
                values[pair.Key] = pair.Value switch
                {
                    TextBox textBox => textBox.Text.Trim(),
                    NumberBox numberBox when !double.IsNaN(numberBox.Value) =>
                        ((long)numberBox.Value).ToString(),
                    ComboBox comboBox when comboBox.SelectedItem is ConditionalParameterChoice choice =>
                        choice.Value,
                    _ => string.Empty
                };
            }
            return values;
        }

        private void ContentDialog_PrimaryButtonClick(
            ContentDialog sender,
            ContentDialogButtonClickEventArgs args)
        {
            try
            {
                ResultAction = CreateAction();
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                StatusInfoBar.Message = ex.Message;
                StatusInfoBar.IsOpen = true;
            }
        }

        private ConditionalAction CreateAction()
        {
            if (ActionTypeComboBox.SelectedItem is not ConditionalActionDefinition definition)
                throw new InvalidDataException(Text("CL_ChooseAction"));

            ConditionalAction action = new() { Type = definition.Type };
            if (definition.Type == ConditionalActionType.ApplyPreset)
            {
                var componentId = (_parameterControls["componentId"] as ComboBox)?.SelectedItem
                    as ConditionalParameterChoice;
                var preset = (_parameterControls["preset"] as ComboBox)?.SelectedItem as PresetChoice;
                if (componentId == null)
                    throw new InvalidDataException(Text("CL_ErrorComponentRequired"));
                if (preset == null)
                    throw new InvalidDataException(Text("CL_ErrorPresetRequired"));

                action.SetParameter("componentId", componentId.Value);
                action.SetParameter("packId", preset.PackId);
                action.SetParameter("fileName", preset.FileName);
                return action;
            }

            var values = ReadParameterValues();
            foreach (var parameter in definition.Parameters)
            {
                if (!parameter.IsVisible(values))
                    continue;
                if (parameter.IsRequired(values) &&
                    (!values.TryGetValue(parameter.Name, out var value) || string.IsNullOrWhiteSpace(value)))
                {
                    throw new InvalidDataException(string.Format(
                        Text("CL_ErrorParameterRequiredFormat"), parameter.Label));
                }
            }

            foreach (var parameter in definition.Parameters.Where(parameter => parameter.IsVisible(values)))
            {
                if (values.TryGetValue(parameter.Name, out var value) && !string.IsNullOrWhiteSpace(value))
                    action.SetParameter(parameter.Name, value);
            }
            return action;
        }

        private string Text(string resourceKey) => _localizer.GetLocalizedString(resourceKey);

        private static string ResolveStoreItemName(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;
            try
            {
                return DatabaseHelper.Instance.GetItemById(id)?.ShortName ?? id;
            }
            catch
            {
                return id;
            }
        }

        private sealed record PresetChoice(
            string Name,
            string PackName,
            string PackId,
            string FileName);
    }
}
