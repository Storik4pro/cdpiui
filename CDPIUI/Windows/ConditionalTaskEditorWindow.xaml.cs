#nullable enable

using CDPIUI.ConditionalLaunch;
using CDPIUI.Controls.Dialogs.ConditionalLaunch;
using CDPIUI.Core.Communication;
using CDPIUI.Default;
using CDPIUI.Shared.ConditionalLaunch;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using WinUI3Localizer;

namespace CDPIUI
{
    public sealed partial class ConditionalTaskEditorWindow : TemplateWindow
    {
        internal const string WindowIdPrefix = "ConditionalTaskEditor:";
        internal const string NewTaskWindowId = "ConditionalTaskEditor:New";

        internal ObservableCollection<ConditionalTriggerListItem> Triggers { get; } = [];
        internal ObservableCollection<ConditionalActionListItem> Actions { get; } = [];
        internal static event Action<string, bool>? TaskSaved;

        private readonly ILocalizer _localizer = Localizer.Get();
        private string _tasksDirectory = string.Empty;
        private ConditionalTask _task = null!;
        private bool _isImport;

        public ConditionalTaskEditorWindow()
        {
            InitializeComponent();

            IconUri = @"Assets/Icons/ConditionalUtil.ico";
            CustomTitleBarUserControl = TitleBarUserControl;
            WindowMinSize = new System.Windows.Size(720, 580);
            DisableResizeFeature(false);
        }

        public void SetTask(
            ConditionalTask? sourceTask,
            string tasksDirectory,
            bool isImport = false)
        {
            if (_task != null)
                return;

            var isNewTask = sourceTask == null;
            _tasksDirectory = tasksDirectory;
            _isImport = isImport;
            _task = isNewTask
                ? ConditionalLaunchUiCatalog.CreateNewTask(_localizer)
                : ConditionalLaunchUiCatalog.CloneTask(sourceTask!);
            if (isNewTask)
            {
                var defaultName = _task.Name;
                var existingNames = ConditionalTaskFileService
                    .LoadDirectory(_tasksDirectory)
                    .Select(task => task.Name)
                    .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
                for (var suffix = 1; existingNames.Contains(_task.Name); suffix++)
                    _task.Name = $"{defaultName} ({suffix})";
            }

            WindowTitle = isNewTask
                ? Text("CL_EditorCreateTitle")
                : string.Format(Text("CL_EditorEditTitle"), _task.Name);
            if (isImport)
                SaveTaskButton.Content = Text("CL_AddTaskButtonText");

            PriorityComboBox.ItemsSource = ConditionalLaunchUiCatalog.CreatePriorities(_localizer);
            LoadTaskIntoEditor();
        }

        private void LoadTaskIntoEditor()
        {
            TaskNameTextBox.Text = _task.Name;
            TaskEnabledCheckBox.IsChecked = _task.IsEnabled;
            StopAfterErrorCheckBox.IsChecked = _task.StopAfterError;
            PriorityComboBox.SelectedItem = PriorityComboBox.Items
                .OfType<ChoiceItem<ConditionalTaskPriority>>()
                .First(item => item.Value == _task.Priority);

            RefreshTriggerList();
            RefreshActionList();
            TaskNameTextBox.Focus(FocusState.Programmatic);
            TaskNameTextBox.SelectAll();
        }

        private void SaveTaskButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyEditorToTask();
                ConditionalTaskFileService.Save(_task, _tasksDirectory);
                _ = PipeHelper.SendConditionalTasksReloadPacket();
                TaskSaved?.Invoke(_task.Id, _isImport);
                Close();
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message, InfoBarSeverity.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void EditorHelpButton_Click(object sender, RoutedEventArgs e)
        {
            Commands.CommandsHandler.HandleCommand(
                "cdpiui://Help/ConditionalLaunch/CreatingTasks/");
        }

        private void ApplyEditorToTask()
        {
            if (string.IsNullOrWhiteSpace(TaskNameTextBox.Text))
                throw new InvalidDataException(Text("CL_ErrorTaskNameRequired"));
            if (PriorityComboBox.SelectedItem is not ChoiceItem<ConditionalTaskPriority> priority)
                throw new InvalidDataException(Text("CL_ErrorPriorityRequired"));
            if (_task.Triggers.Count == 0)
                throw new InvalidDataException(Text("CL_ErrorTriggerRequired"));
            if (Actions.Count == 0)
                throw new InvalidDataException(Text("CL_ErrorActionRequired"));

            _task.Name = TaskNameTextBox.Text.Trim();
            _task.IsEnabled = TaskEnabledCheckBox.IsChecked == true;
            _task.StopAfterError = StopAfterErrorCheckBox.IsChecked == true;
            _task.Priority = priority.Value;
            _task.Actions = Actions.Select(item => item.Action).ToList();
            ConditionalTaskFileService.Validate(_task);
        }

        private async void CreateTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            var trigger = await ShowTriggerDialogAsync(null);
            if (trigger == null)
                return;

            _task.Triggers.Add(trigger);
            RefreshTriggerList(_task.Triggers.Count - 1);
        }

        private async void EditTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            await EditSelectedTriggerAsync();
        }

        private async void TriggerListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            await EditSelectedTriggerAsync();
        }

        private async System.Threading.Tasks.Task EditSelectedTriggerAsync()
        {
            var index = TriggerListView.SelectedIndex;
            if (index < 0 || index >= _task.Triggers.Count)
                return;

            var trigger = await ShowTriggerDialogAsync(_task.Triggers[index]);
            if (trigger == null)
                return;

            _task.Triggers[index] = trigger;
            RefreshTriggerList(index);
        }

        private async System.Threading.Tasks.Task<ConditionalTrigger?> ShowTriggerDialogAsync(
            ConditionalTrigger? trigger)
        {
            ConditionalTriggerContentDialog dialog = new(trigger)
            {
                XamlRoot = Content.XamlRoot
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary
                ? dialog.ResultTrigger
                : null;
        }

        private void DeleteTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            var index = TriggerListView.SelectedIndex;
            if (index < 0 || index >= _task.Triggers.Count)
                return;

            _task.Triggers.RemoveAt(index);
            RefreshTriggerList(Math.Min(index, _task.Triggers.Count - 1));
        }

        private void TriggerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTriggerButtons();
        }

        private void RefreshTriggerList(int selectedIndex = 0)
        {
            Triggers.Clear();
            foreach (var trigger in _task.Triggers)
            {
                var type = Text(trigger.Type switch
                {
                    ConditionalTriggerType.HotKey => "CL_TriggerHotKey",
                    ConditionalTriggerType.ProcessStarted => "CL_TriggerProcessStarted",
                    _ => "CL_TriggerProcessStopped"
                });
                Triggers.Add(new ConditionalTriggerListItem(
                    type,
                    ConditionalLaunchUiCatalog.FormatTrigger(trigger, _localizer),
                    Text("CL_StatusConfigured")));
            }

            if (selectedIndex >= 0 && selectedIndex < Triggers.Count)
                TriggerListView.SelectedIndex = selectedIndex;
            UpdateTriggerButtons();
        }

        private void UpdateTriggerButtons()
        {
            var hasSelection = TriggerListView.SelectedIndex >= 0;
            EditTriggerButton.IsEnabled = hasSelection;
            DeleteTriggerButton.IsEnabled = hasSelection;
        }

        private async void CreateActionButton_Click(object sender, RoutedEventArgs e)
        {
            var action = await ShowActionDialogAsync(null);
            if (action == null)
                return;

            Actions.Add(CreateActionListItem(action, Actions.Count + 1));
            ActionListView.SelectedIndex = Actions.Count - 1;
        }

        private async void ActionListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            await EditSelectedActionAsync();
        }

        private async void EditActionButton_Click(object sender, RoutedEventArgs e)
        {
            await EditSelectedActionAsync();
        }

        private async System.Threading.Tasks.Task EditSelectedActionAsync()
        {
            var index = ActionListView.SelectedIndex;
            if (index < 0)
                return;

            var action = await ShowActionDialogAsync(Actions[index].Action);
            if (action == null)
                return;

            Actions[index] = CreateActionListItem(action, index + 1);
            ActionListView.SelectedIndex = index;
        }

        private async System.Threading.Tasks.Task<ConditionalAction?> ShowActionDialogAsync(
            ConditionalAction? action)
        {
            ConditionalActionContentDialog dialog = new(action)
            {
                XamlRoot = Content.XamlRoot
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary
                ? dialog.ResultAction
                : null;
        }

        private void RemoveActionButton_Click(object sender, RoutedEventArgs e)
        {
            var index = ActionListView.SelectedIndex;
            if (index < 0)
                return;
            Actions.RemoveAt(index);
            RefreshActionOrder(Math.Min(index, Actions.Count - 1));
        }

        private void MoveActionUpButton_Click(object sender, RoutedEventArgs e)
        {
            var index = ActionListView.SelectedIndex;
            if (index <= 0)
                return;
            Actions.Move(index, index - 1);
            RefreshActionOrder(index - 1);
        }

        private void MoveActionDownButton_Click(object sender, RoutedEventArgs e)
        {
            var index = ActionListView.SelectedIndex;
            if (index < 0 || index >= Actions.Count - 1)
                return;
            Actions.Move(index, index + 1);
            RefreshActionOrder(index + 1);
        }

        private void ActionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateActionButtons();
        }

        private void RefreshActionList()
        {
            Actions.Clear();
            foreach (var action in _task.Actions)
                Actions.Add(CreateActionListItem(action, Actions.Count + 1));
            UpdateActionButtons();
        }

        private void RefreshActionOrder(int selectedIndex)
        {
            var actions = Actions.Select(item => item.Action).ToList();
            Actions.Clear();
            for (var index = 0; index < actions.Count; index++)
                Actions.Add(CreateActionListItem(actions[index], index + 1));
            if (selectedIndex >= 0 && selectedIndex < Actions.Count)
                ActionListView.SelectedIndex = selectedIndex;
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            var hasSelection = ActionListView.SelectedIndex >= 0;
            EditActionButton.IsEnabled = hasSelection;
            RemoveActionButton.IsEnabled = hasSelection;
        }

        private ConditionalActionListItem CreateActionListItem(ConditionalAction action, int order) =>
            ConditionalLaunchUiCatalog.CreateActionListItem(action, order, _localizer);

        private void ShowStatus(string message, InfoBarSeverity severity)
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }

        private string Text(string resourceKey) => _localizer.GetLocalizedString(resourceKey);
    }
}
