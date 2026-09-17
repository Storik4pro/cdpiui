using CDPIUI.ConditionalLaunch;
using CDPIUI.Shared.ConditionalLaunch;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormsKeys = System.Windows.Forms.Keys;
using WinUI3Localizer;

namespace CDPIUI.Controls.Dialogs.ConditionalLaunch
{
    public sealed partial class ConditionalTriggerContentDialog : ContentDialog
    {
        public ConditionalTrigger? ResultTrigger { get; private set; }

        private readonly ILocalizer _localizer = Localizer.Get();
        private readonly List<ChoiceItem<ConditionalTriggerType>> _triggerTypes;
        private readonly List<ChoiceItem<FormsKeys>> _hotKeys;
        private bool _loading;

        public ConditionalTriggerContentDialog(ConditionalTrigger? trigger)
        {
            InitializeComponent();
            _triggerTypes = ConditionalLaunchUiCatalog.CreateTriggerTypes(_localizer);
            _hotKeys = ConditionalLaunchUiCatalog.CreateHotKeyChoices();
            TriggerTypeComboBox.ItemsSource = _triggerTypes;
            HotKeyComboBox.ItemsSource = _hotKeys;

            Title = Text(trigger == null ? "CL_AddTriggerDialogTitle" : "CL_EditTriggerDialogTitle");
            PrimaryButtonText = Text("CL_OkButtonText");
            CloseButtonText = Text("CL_CancelButtonText");
            DefaultButton = ContentDialogButton.Primary;
            LoadTrigger(trigger ?? CreateDefaultTrigger());
        }

        private static ConditionalTrigger CreateDefaultTrigger() => new()
        {
            Type = ConditionalTriggerType.HotKey,
            DelaySeconds = 5,
            Parameters =
            [
                new() { Name = "modifiers", Value = ConditionalHotKeyModifiers.Control.ToString() },
                new() { Name = "key", Value = FormsKeys.F1.ToString() }
            ]
        };

        private void LoadTrigger(ConditionalTrigger trigger)
        {
            _loading = true;
            TriggerTypeComboBox.SelectedItem = _triggerTypes.First(item => item.Value == trigger.Type);
            if (Enum.TryParse<ConditionalHotKeyModifiers>(
                trigger.GetParameter("modifiers"), true, out var modifiers))
            {
                ControlModifierCheckBox.IsChecked = modifiers.HasFlag(ConditionalHotKeyModifiers.Control);
                AltModifierCheckBox.IsChecked = modifiers.HasFlag(ConditionalHotKeyModifiers.Alt);
                ShiftModifierCheckBox.IsChecked = modifiers.HasFlag(ConditionalHotKeyModifiers.Shift);
                WindowsModifierCheckBox.IsChecked = modifiers.HasFlag(ConditionalHotKeyModifiers.Windows);
            }

            if (Enum.TryParse<FormsKeys>(trigger.GetParameter("key"), true, out var key))
                HotKeyComboBox.SelectedItem = _hotKeys.FirstOrDefault(item => item.Value == key);
            HotKeyComboBox.SelectedItem ??= _hotKeys.First(item => item.Value == FormsKeys.F1);
            ProcessNameTextBox.Text = trigger.GetParameter("processName") ?? string.Empty;
            ProcessDelayNumberBox.Value = trigger.DelaySeconds;
            UpdateEditorVisibility();
            _loading = false;
        }

        private void TriggerTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loading)
                UpdateEditorVisibility();
        }

        private void UpdateEditorVisibility()
        {
            var isHotKey = (TriggerTypeComboBox.SelectedItem as ChoiceItem<ConditionalTriggerType>)?.Value
                == ConditionalTriggerType.HotKey;
            HotKeyEditor.Visibility = isHotKey ? Visibility.Visible : Visibility.Collapsed;
            ProcessEditor.Visibility = isHotKey ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ContentDialog_PrimaryButtonClick(
            ContentDialog sender,
            ContentDialogButtonClickEventArgs args)
        {
            try
            {
                ResultTrigger = CreateTrigger();
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                StatusInfoBar.Message = ex.Message;
                StatusInfoBar.IsOpen = true;
            }
        }

        private ConditionalTrigger CreateTrigger()
        {
            if (TriggerTypeComboBox.SelectedItem is not ChoiceItem<ConditionalTriggerType> triggerType)
                throw new InvalidDataException(Text("CL_ErrorTriggerRequired"));

            ConditionalTrigger trigger = new()
            {
                Type = triggerType.Value,
                DelaySeconds = triggerType.Value == ConditionalTriggerType.HotKey
                    ? 0
                    : double.IsNaN(ProcessDelayNumberBox.Value)
                        ? 5
                        : (int)ProcessDelayNumberBox.Value
            };

            if (triggerType.Value == ConditionalTriggerType.HotKey)
            {
                if (HotKeyComboBox.SelectedItem is not ChoiceItem<FormsKeys> hotKey)
                    throw new InvalidDataException(Text("CL_ErrorHotKeyRequired"));

                var modifiers = ConditionalHotKeyModifiers.None;
                if (ControlModifierCheckBox.IsChecked == true) modifiers |= ConditionalHotKeyModifiers.Control;
                if (AltModifierCheckBox.IsChecked == true) modifiers |= ConditionalHotKeyModifiers.Alt;
                if (ShiftModifierCheckBox.IsChecked == true) modifiers |= ConditionalHotKeyModifiers.Shift;
                if (WindowsModifierCheckBox.IsChecked == true) modifiers |= ConditionalHotKeyModifiers.Windows;
                trigger.SetParameter("modifiers", modifiers.ToString());
                trigger.SetParameter("key", hotKey.Value.ToString());
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ProcessNameTextBox.Text))
                    throw new InvalidDataException(Text("CL_ErrorProcessNameRequired"));
                trigger.SetParameter("processName", ProcessNameTextBox.Text.Trim());
            }

            return trigger;
        }

        private string Text(string resourceKey) => _localizer.GetLocalizedString(resourceKey);
    }
}
