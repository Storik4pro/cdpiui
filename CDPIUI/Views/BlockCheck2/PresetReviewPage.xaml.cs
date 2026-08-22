using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;
using CDPIUI.Controls.Universal;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper.AddOns.BlockCheck2;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.Views.BlockCheck2;

public sealed partial class PresetReviewPage : CDPIUI.Controls.Default.TemplatePage, IStatusNotificationSource, INotifyPropertyChanged
{
    private readonly ILocalizer localizer = Localizer.Get();
    private readonly BlockCheck2PresetStorageService presetStorageService = new();
    private bool updatingEditingToggle;
    private bool savingPreset;

    public PresetReviewPage()
    {
        InitializeComponent();
        DataContext = this;
        Editor.UseInlineStatusMessages = false;
        Editor.StatusNotificationRequested += Editor_StatusNotificationRequested;
        Editor.TestStateChanged += Editor_MenuStateChanged;
        Editor.EditorReadOnlyChanged += Editor_MenuStateChanged;
        Editor.PanelStateChanged += Editor_MenuStateChanged;
    }

    public BlockCheck2ResultViewModel? ViewModel { get; private set; }
    public ConfigMakerUserControl Editor => PresetEditor.Editor;
    public event EventHandler<StatusNotificationRequestedEventArgs>? StatusNotificationRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool CanFormatEditor => !Editor.IsEditorReadOnly;
    public bool IsEditorCommandPanelVisible => Editor.IsCommandPanelVisible;
    public bool IsEditorBottomPanelVisible => Editor.IsBottomPanelVisible;
    public bool IsEditorFilesPanelVisible => Editor.IsPresetFilesPanelVisible;
    public bool HasEditorFiles => Editor.HasPresetFiles;
    public bool HasEditorGroups => Editor.HasPresetGroups;
    public bool CanStartEditorTest => !Editor.IsTesting;
    public bool CanStopEditorTest => Editor.IsTesting;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is BlockCheck2ResultViewModel viewModel)
        {
            Load(viewModel);
        }
    }

    public void Load(BlockCheck2ResultViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (ViewModel != null)
        {
            ViewModel.Draft.PropertyChanged -= Draft_PropertyChanged;
        }
        ViewModel = viewModel;
        ViewModel.Draft.PropertyChanged += Draft_PropertyChanged;
        PresetEditor.LoadDraft(ViewModel.Draft);
        UpdateState();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (ViewModel != null)
        {
            ViewModel.Draft.PropertyChanged -= Draft_PropertyChanged;
        }
    }

    private void Draft_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        UpdateState();

    private void UpdateState()
    {
        if (ViewModel == null)
        {
            return;
        }
        BlockCheck2PresetDraft draft = ViewModel.Draft;
        updatingEditingToggle = true;
        AllowEditingCheckBox.IsChecked = draft.IsExpertEditingEnabled;
        updatingEditingToggle = false;
        ExpertInfoBar.IsOpen = draft.IsExpertEditingEnabled;
        RaiseMenuStateChanged();
    }

    private async void AllowEditingCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (updatingEditingToggle || ViewModel == null)
        {
            return;
        }
        bool enable = AllowEditingCheckBox.IsChecked == true;
        if (enable)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = Text("BlockCheck2EnableExpertEditingTitle"),
                Content = Text("BlockCheck2EnableExpertEditingMessage"),
                PrimaryButtonText = Text("BlockCheck2EnableExpertEditingButton"),
                CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                updatingEditingToggle = true;
                AllowEditingCheckBox.IsChecked = false;
                updatingEditingToggle = false;
                return;
            }
        }
        PresetEditor.SetExpertEditing(enable);
        UpdateState();
    }

    public async Task NavigateBackToBuilderAsync()
    {
        if (ViewModel == null)
        {
            return;
        }
        if (ViewModel.Draft.HasExpertChanges)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = XamlRoot,
                Title = Text("BlockCheck2DiscardExpertChangesTitle"),
                Content = Text("BlockCheck2DiscardExpertChangesMessage"),
                PrimaryButtonText = Text("BlockCheck2DiscardExpertChangesButton"),
                CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }
        ViewModel.Draft.DiscardExpertChanges();
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
            DispatcherQueue.TryEnqueue(() =>
            {
                if (Frame.Content is ResultPage resultPage)
                {
                    resultPage.OpenPresetBuilder();
                }
            });
        }
    }

    public async Task SaveToApplicationAsync()
    {
        if (ViewModel == null || !ViewModel.Draft.CanUseConfig || savingPreset)
        {
            return;
        }

        TextBox nameTextBox = new()
        {
            Header = Text("BlockCheck2PresetNameFieldHeader"),
            PlaceholderText = Text("BlockCheck2PresetNameFieldPlaceholder"),
            MaxLength = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        TextBlock explanation = new()
        {
            Text = Text("BlockCheck2SaveToApplicationDialogMessage"),
            TextWrapping = TextWrapping.Wrap,
        };
        StackPanel content = new() { Spacing = 10 };
        content.Children.Add(explanation);
        content.Children.Add(nameTextBox);
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = Text("BlockCheck2SaveToApplicationDialogTitle"),
            Content = content,
            PrimaryButtonText = Text("BlockCheck2SaveToApplicationDialogButton"),
            CloseButtonText = Text("BlockCheck2CancelDialogButtonText"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };
        nameTextBox.TextChanged += (_, _) =>
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameTextBox.Text);
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        savingPreset = true;
        SaveToApplicationProgressRing.IsActive = true;
        UpdateState();
        try
        {
            BlockCheck2PresetStorageResult result = await presetStorageService.SaveAsync(
                nameTextBox.Text,
                ViewModel.Draft);
            if (!result.Success)
            {
                ShowStatus(
                    InfoBarSeverity.Error,
                    Text("BlockCheck2PresetSaveFailedTitle"),
                    PresetSaveErrorMessage(result));
                return;
            }

            ShowStatus(
                InfoBarSeverity.Success,
                Text("BlockCheck2PresetSavedTitle"),
                string.Format(
                    Text("BlockCheck2PresetSavedMessageFormat"),
                    result.PresetName,
                    result.CopiedFileCount));

            ShowDialog();

            try
            {
                ComponentItemsLoaderHelper.Instance
                    .GetComponentHelperFromId(HardcodedItemIds.ComponentIds[Components.Zapret2])
                    ?.ReInitConfigs();
            }
            catch (Exception exception)
            {
                ShowStatus(
                    InfoBarSeverity.Warning,
                    Text("BlockCheck2PresetSavedRefreshFailedTitle"),
                    exception.Message);
            }
        }
        catch (Exception exception)
        {
            ShowStatus(
                InfoBarSeverity.Error,
                Text("BlockCheck2PresetSaveFailedTitle"),
                exception.Message);
        }
        finally
        {
            savingPreset = false;
            SaveToApplicationProgressRing.IsActive = false;
            UpdateState();
        }

        
    }

    private async void ShowDialog()
    {
        CreateCompleteDialog dialog = new()
        {
            XamlRoot = this.XamlRoot
        };
        BlockCheck2ResultWindow window = ((App)Application.Current).GetCurrentWindowFromType<BlockCheck2ResultWindow>();
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            window?.Close();
        else
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

    }

    private string PresetSaveErrorMessage(BlockCheck2PresetStorageResult result) =>
        result.ErrorCode switch
        {
            "COMPONENT_UNAVAILABLE" => Text("BlockCheck2PresetSaveComponentUnavailableMessage"),
            "FILE_MISSING" => string.Format(
                Text("BlockCheck2PresetSaveFileMissingMessageFormat"),
                result.ErrorDetails),
            "SAVE_FAILED" => string.Format(
                Text("BlockCheck2PresetSaveStorageErrorMessageFormat"),
                result.ErrorDetails),
            "PRESET_EMPTY" => Text("BlockCheck2PresetSaveEmptyMessage"),
            _ => string.IsNullOrWhiteSpace(result.ErrorDetails)
                ? Text("BlockCheck2PresetSaveUnknownMessage")
                : result.ErrorDetails,
        };

    private void ShowStatus(InfoBarSeverity severity, string title, string message) =>
        StatusNotificationRequested?.Invoke(
            this,
            new StatusNotificationRequestedEventArgs(severity, title, message));

    private void Editor_StatusNotificationRequested(
        object? sender,
        StatusNotificationRequestedEventArgs e) =>
        StatusNotificationRequested?.Invoke(this, e);

    private async void BlockCheck2SaveToApplicationButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveToApplicationAsync();
    }

    private async void SaveEditorTextMenuItem_Click(object sender, RoutedEventArgs e) =>
        await Editor.SaveTextAsync();

    private void FormatEditorMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.FormatCommand();

    private void EditorFilesPanelMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.SetPresetFilesPanelVisible(!Editor.IsPresetFilesPanelVisible);

    private void EditorCommandPanelMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.SetCommandPanelVisible(!Editor.IsCommandPanelVisible);

    private void EditorBottomPanelMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.SetBottomPanelVisible(!Editor.IsBottomPanelVisible);

    private void ShowEditorOutputMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.ShowOutputTab();

    private void ShowEditorDiagnosticsMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.ShowDiagnosticsTab();

    private void ShowEditorGroupsMenuItem_Click(object sender, RoutedEventArgs e) =>
        Editor.ShowPresetGroupsTab();

    private async void StartEditorTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await Editor.StartTestAsync();

    private async void StopEditorTestMenuItem_Click(object sender, RoutedEventArgs e) =>
        await Editor.StopTestAsync(showCompletionMessage: true);

    private async void RefreshEditorHelpMenuItem_Click(object sender, RoutedEventArgs e) =>
        await Editor.RefreshHelpAsync();

    private void Editor_MenuStateChanged(object? sender, EventArgs e) =>
        RaiseMenuStateChanged();

    private void RaiseMenuStateChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    private string Text(string key) => localizer.GetLocalizedString(key);

    
}
