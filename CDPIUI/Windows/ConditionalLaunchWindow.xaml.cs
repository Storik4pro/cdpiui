#nullable enable

using CDPIUI.ConditionalLaunch;
using CDPIUI.Core;
using CDPIUI.Core.Communication;
using CDPIUI.Default;
using CDPIUI.Helper.WindowHelper;
using CDPIUI.Shared.ConditionalLaunch;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Windows.System;
using WinUI3Localizer;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace CDPIUI
{
    public sealed partial class ConditionalLaunchWindow : TemplateWindow
    {
        internal ObservableCollection<ConditionalTaskListItem> Tasks { get; } = [];
        internal ObservableCollection<ConditionalTriggerListItem> PreviewTriggers { get; } = [];
        internal ObservableCollection<ConditionalActionListItem> PreviewActions { get; } = [];

        private readonly ILocalizer _localizer = Localizer.Get();
        private readonly string _tasksDirectory;
        private readonly DispatcherTimer _statusInfoBarTimer = new()
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        private ConditionalTask? _selectedTask;
        private bool _allowInfoBarClose;

        public ConditionalLaunchWindow()
        {
            InitializeComponent();

            WindowTitle = Text("CL_WindowTitle");
            IconUri = @"Assets/Icons/ConditionalUtil.ico";
            CustomTitleBarUserControl = TitleBarUserControl;
            WindowMinSize = new System.Windows.Size(900, 580);
            _statusInfoBarTimer.Tick += StatusInfoBarTimer_Tick;
            HideInfoBarAnimation.Completed += (_, _) =>
            {
                _allowInfoBarClose = true;
                StatusInfoBar.IsOpen = false;
                _allowInfoBarClose = false;
            };

            _tasksDirectory = ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile(
                SettingsManager.Instance.SettingsFilePath);

            LoadTasks();

            this.Closed += ConditionalLaunchWindow_Closed;
            this.SizeChanged += ConditionalLaunchWindow_SizeChanged;
        }

        private void ConditionalLaunchWindow_Closed(object sender, WindowEventArgs args)
        {
            _statusInfoBarTimer.Stop();
            this.SizeChanged -= ConditionalLaunchWindow_SizeChanged;
            this.Closed -= ConditionalLaunchWindow_Closed;
        }

        private void ConditionalLaunchWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            TasksBorder.MaxHeight = this.Height - 400;
        }

        public void ImportTaskFile(string filePath)
        {
            try
            {
                var imported = ConditionalTaskFileService.Load(filePath);
                if (Tasks.Any(item => string.Equals(
                    item.Task.Id, imported.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    imported.Id = Guid.NewGuid().ToString("D");
                }

                imported.FilePath = null;
                ConditionalTaskFileService.Save(imported, _tasksDirectory);
                LoadTasks(imported.Id);
                NotifyTaskEngine();
                ShowStatus(Text("CL_TaskImported"), InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus(string.Format(Text("CL_CannotImportTaskFormat"), ex.Message), InfoBarSeverity.Error);
            }
        }

        private void LoadTasks(string? selectedTaskId = null)
        {
            selectedTaskId ??= _selectedTask?.Id;
            Tasks.Clear();

            Directory.CreateDirectory(_tasksDirectory);
            var invalidFiles = 0;
            var loadedTasks = Directory.EnumerateFiles(
                    _tasksDirectory,
                    $"*{ConditionalTaskFileService.FileExtension}")
                .Select(filePath =>
                {
                    try
                    {
                        return ConditionalTaskFileService.Load(filePath);
                    }
                    catch
                    {
                        invalidFiles++;
                        return null;
                    }
                })
                .Where(task => task != null)
                .Cast<ConditionalTask>()
                .OrderByDescending(task => task.Priority)
                .ThenBy(task => task.Name, StringComparer.CurrentCultureIgnoreCase);

            foreach (var task in loadedTasks)
                Tasks.Add(ConditionalLaunchUiCatalog.CreateTaskListItem(task, _localizer));

            EmptyTaskListPanel.Visibility = Tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TaskListView.Visibility = Tasks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            var selected = Tasks.FirstOrDefault(item =>
                    string.Equals(item.Task.Id, selectedTaskId, StringComparison.OrdinalIgnoreCase))
                ?? Tasks.FirstOrDefault();
            TaskListView.SelectedItem = selected;
            UpdatePreview(selected?.Task);

            if (invalidFiles > 0)
            {
                ShowStatus(
                    string.Format(Text("CL_InvalidFilesSkippedFormat"), invalidFiles),
                    InfoBarSeverity.Warning);
            }

            UpdateSelectionButtons();
        }

        private void TaskListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview((TaskListView.SelectedItem as ConditionalTaskListItem)?.Task);
            UpdateSelectionButtons();
        }

        private async void TaskListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_selectedTask != null)
                await OpenEditorAsync(_selectedTask);
        }

        private async void TaskListView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && _selectedTask != null)
            {
                e.Handled = true;
                await OpenEditorAsync(_selectedTask);
            }
        }

        private void TaskListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
                return;

            DependencyObject? current = source;
            while (current != null && current is not ListViewItem)
                current = VisualTreeHelper.GetParent(current);
            if (current is not ListViewItem container)
            {
                return;
            }

            TaskListView.SelectedItem = container.Content;
            if (RootGrid.Resources["TaskContextMenu"] is MenuFlyout menu)
            {
                var toggleItem = menu.Items
                    .OfType<MenuFlyoutItem>()
                    .FirstOrDefault(item => Equals(item.Tag, "ToggleEnabled"));
                if (toggleItem != null && _selectedTask != null)
                {
                    toggleItem.Text = Text(_selectedTask.IsEnabled
                        ? "CL_DisableTaskMenuItem"
                        : "CL_EnableTaskMenuItem");
                }

                menu.ShowAt(RootGrid, new FlyoutShowOptions
                {
                    Position = e.GetPosition(RootGrid)
                });
            }
            e.Handled = true;
        }

        private async void RunTaskMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await RunSelectedTaskAsync();
        }

        private void ToggleTaskEnabledMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
                return;

            var taskId = _selectedTask.Id;
            var taskName = _selectedTask.Name;
            var isEnabled = !_selectedTask.IsEnabled;
            try
            {
                _selectedTask.IsEnabled = isEnabled;
                ConditionalTaskFileService.Save(
                    _selectedTask,
                    _tasksDirectory,
                    _selectedTask.FilePath);
                LoadTasks(taskId);
                NotifyTaskEngine();
                ShowStatus(
                    string.Format(Text(isEnabled
                        ? "CL_TaskEnabledFormat"
                        : "CL_TaskDisabledFormat"), taskName),
                    InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                _selectedTask.IsEnabled = !isEnabled;
                ShowStatus(
                    string.Format(Text("CL_CannotChangeTaskStateFormat"), ex.Message),
                    InfoBarSeverity.Error);
            }
        }

        private async void PropertiesTaskMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
                await OpenEditorAsync(_selectedTask);
        }

        private void DeleteTaskMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteButton_Click(sender, e);
        }

        private void HelpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string helpUrl })
            {
                Commands.CommandsHandler.HandleCommand(
                    $"cdpiui://Help/{helpUrl.Trim('/')}/");
            }
        }

        private async System.Threading.Tasks.Task RunSelectedTaskAsync()
        {
            if (_selectedTask == null)
                return;

            try
            {
                await PipeHelper.SendConditionalTaskExecutePacket(_selectedTask.Id);
                ShowStatus(
                    string.Format(Text("CL_TaskExecutionRequestedFormat"), _selectedTask.Name),
                    InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus(
                    string.Format(Text("CL_CannotRunTaskFormat"), ex.Message),
                    InfoBarSeverity.Error);
            }
        }

        private async void NewTaskButton_Click(object sender, RoutedEventArgs e)
        {
            await OpenEditorAsync(null);
        }

        private async void RunTaskButton_Click(object sender, RoutedEventArgs e)
        {
            await RunSelectedTaskAsync();
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask != null)
                await OpenEditorAsync(_selectedTask);
        }

        private async System.Threading.Tasks.Task OpenEditorAsync(ConditionalTask? task)
        {
            var editor = new ConditionalTaskEditorWindow(task, _tasksDirectory);
            WindowsPositionHelper.SetCustomWindowSizeAndPositionFromSettings(editor);
            var cornerOffset = (int)Math.Round(
                16 * WindowsPositionHelper.GetScaleFactor(this));
            WindowsPositionHelper.SetWindowPosition(
                editor,
                AppWindow.Position.X + cornerOffset,
                AppWindow.Position.Y + cornerOffset);

            var modalTask = ((App)Application.Current).ShowWindowModalAsync(editor, this);
            editor.Activate();
            await modalTask;

            if (!editor.IsSaved)
                return;

            LoadTasks(editor.SavedTaskId);
            ShowStatus(Text("CL_TaskSaved"), InfoBarSeverity.Success);
        }

        private void UpdatePreview(ConditionalTask? task)
        {
            _selectedTask = task;
            PreviewTriggers.Clear();
            PreviewActions.Clear();
            NoSelectionPanel.Visibility = task == null ? Visibility.Visible : Visibility.Collapsed;
            PreviewSelector.Visibility = task == null ? Visibility.Collapsed : Visibility.Visible;
            if (task == null)
                return;

            var item = ConditionalLaunchUiCatalog.CreateTaskListItem(task, _localizer);
            PreviewNameTextBlock.Text = item.Name;
            PreviewStatusTextBlock.Text = item.Status;
            PreviewPriorityTextBlock.Text = item.PriorityLabel;
            PreviewFileTextBlock.Text = task.FilePath ?? Text("CL_NotSaved");

            foreach (var trigger in task.Triggers)
            {
                var type = Text(trigger.Type switch
                {
                    ConditionalTriggerType.HotKey => "CL_TriggerHotKey",
                    ConditionalTriggerType.ProcessStarted => "CL_TriggerProcessStarted",
                    _ => "CL_TriggerProcessStopped"
                });
                PreviewTriggers.Add(new ConditionalTriggerListItem(
                    type,
                    ConditionalLaunchUiCatalog.FormatTrigger(trigger, _localizer),
                    Text("CL_StatusConfigured")));
            }

            var definitions = ConditionalLaunchUiCatalog.CreateActionDefinitions(_localizer);
            for (var index = 0; index < task.Actions.Count; index++)
            {
                PreviewActions.Add(ConditionalLaunchUiCatalog.CreateActionListItem(
                    task.Actions[index], index + 1, _localizer, definitions));
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            using OpenFileDialog dialog = new()
            {
                Title = Text("CL_ImportDialogTitle"),
                Filter = Text("CL_TaskFileFilter"),
                Multiselect = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                ImportTaskFile(dialog.FileName);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
                return;

            try
            {
                using SaveFileDialog dialog = new()
                {
                    Title = Text("CL_ExportDialogTitle"),
                    Filter = Text("CL_TaskFileFilter"),
                    FileName = SanitizeFileName(_selectedTask.Name) + ConditionalTaskFileService.FileExtension
                };

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                ConditionalTaskFileService.Export(_selectedTask, dialog.FileName);
                ShowStatus(Text("CL_TaskExported"), InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus(string.Format(Text("CL_CannotExportTaskFormat"), ex.Message), InfoBarSeverity.Error);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask?.FilePath == null)
                return;

            ContentDialog dialog = new()
            {
                Title = Text("CL_DeleteDialogTitle"),
                Content = string.Format(Text("CL_DeleteDialogContentFormat"), _selectedTask.Name),
                PrimaryButtonText = Text("CL_DeleteButtonText"),
                CloseButtonText = Text("CL_CancelButtonText"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            try
            {
                File.Delete(_selectedTask.FilePath);
                LoadTasks();
                NotifyTaskEngine();
                ShowStatus(Text("CL_TaskDeleted"), InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus(string.Format(Text("CL_CannotDeleteTaskFormat"), ex.Message), InfoBarSeverity.Error);
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTasks();
        }

        private void UpdateSelectionButtons()
        {
            var hasSelection = _selectedTask != null;
            EditButton.IsEnabled = hasSelection;
            RunButton.IsEnabled = hasSelection;
            ExportButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
        }

        private static void NotifyTaskEngine()
        {
            _ = PipeHelper.SendConditionalTasksReloadPacket();
        }

        private void ShowStatus(string message, InfoBarSeverity severity)
        {
            _statusInfoBarTimer.Stop();
            ShowInfoBarAnimation.Stop();
            HideInfoBarAnimation.Stop();
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
            ShowInfoBarAnimation.Begin();
            _statusInfoBarTimer.Start();
        }

        private void StatusInfoBarTimer_Tick(object? sender, object e)
        {
            HideStatusInfoBar();
        }

        private void StatusInfoBar_Closing(InfoBar sender, InfoBarClosingEventArgs args)
        {
            if (_allowInfoBarClose)
                return;

            HideStatusInfoBar();
            args.Cancel = true;
        }

        private void HideStatusInfoBar()
        {
            _statusInfoBarTimer.Stop();
            ShowInfoBarAnimation.Stop();
            HideInfoBarAnimation.Begin();
        }

        private string Text(string resourceKey) => _localizer.GetLocalizedString(resourceKey);

        private static string SanitizeFileName(string value)
        {
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidCharacter, '_');
            return string.IsNullOrWhiteSpace(value) ? "ConditionalTask" : value;
        }
    }
}
