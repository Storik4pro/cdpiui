using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using CDPI_UI.Views.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.MainPage;

public enum ComponentState
{
    SetupRequired,
    Stopped,
    Runned,
    ExitedWithException,
}

public sealed partial class ComponentTileUserControl : UserControl
{
    private ILocalizer localizer = Localizer.Get();

    public ICommand ViewOutputClickCommand { get; }
    public ICommand ViewSettingsButtonClickCommand { get; }
    public ICommand ToggleProcessButtonClickCommand { get; }


    public ComponentTileUserControl()
    {
        InitializeComponent();

        ViewOutputIconButton.DisplayText = localizer.GetLocalizedString("ViewOutputIconButton");
        SettingsIconButton.DisplayText = localizer.GetLocalizedString("SettingsIconButton");

        ViewOutputClickCommand = new RelayCommand(p => ViewOutputButtonClick());
        ViewSettingsButtonClickCommand = new RelayCommand(p => ViewSettingsButtonClick());
        ToggleProcessButtonClickCommand = new RelayCommand(p => ToggleProcessButtonClick());

        TasksHelper.Instance.TaskStateUpdated += TaskUpdated;
    }

    public string StoreId
    {
        get { return (string)GetValue(StoreIdProperty); }
        set { 
            SetValue(StoreIdProperty, value); 
            Init();
        }
    }

    public static readonly DependencyProperty StoreIdProperty =
        DependencyProperty.Register(
            nameof(StoreId), typeof(string), typeof(ComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    public string ImageUrl
    {
        get { return (string)GetValue(ImageUrlProperty); }
        set { 
            SetValue(ImageUrlProperty, value);
            MainImage.Source = new BitmapImage(new Uri(value));
        }
    }

    public static readonly DependencyProperty ImageUrlProperty =
        DependencyProperty.Register(
            nameof(ImageUrl), typeof(string), typeof(ComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    private void TaskUpdated(Tuple<string, bool> tuple)
    {
        if (tuple.Item1 != StoreId) return;
        

        PreferTaskStateActions();
    }

    private void Init()
    {
        TitleTextBlock.Text = StateHelper.Instance.ComponentIdPairs.GetValueOrDefault(StoreId, StoreId);

        if (DatabaseHelper.Instance.IsItemInstalled(StoreId))
        {
            PreferTaskStateActions();

            TileBackgroungBorder.Background = UIHelper.HexToSolidColorBrushConverter(DatabaseHelper.Instance.GetItemById(StoreId).BackgroudColor);

            bool isAddToAutorun = SettingsManager.Instance.GetValue<bool>(["CONFIGS", StoreId], "usedForAutorun");
            AutorunBadgeGrid.Visibility = isAddToAutorun ? Visibility.Visible : Visibility.Collapsed;
            CheckComponentState();
        }
    }

    private async Task<ProcessManager> GetProcessManager()
    {
        return (await TasksHelper.Instance.GetTaskFromId(StoreId))?.ProcessManager;
    }

    private async void PreferTaskStateActions()
    {
        bool isRunned = await TasksHelper.Instance.IsTaskRunned(StoreId);

        if (isRunned)
        {
            StateToggleButton.DisplayText = localizer.GetLocalizedString("Stop");
            ShowComponentState(ComponentState.Runned);
        }
        else
        {
            StateToggleButton.DisplayText = localizer.GetLocalizedString("Start");
            CheckComponentState();
        }

        StateToggleButton.Checked = isRunned;
    }

    private async void ToggleProcessButtonClick()
    {
        bool isRunned = await TasksHelper.Instance.IsTaskRunned(StoreId);

        if (!isRunned)
        {
            TasksHelper.Instance.CreateAndRunNewTask(StoreId);
        }
        else
        {
            await TasksHelper.Instance.StopTask(StoreId);
        }
    }

    private async void CheckComponentState()
    {
        ShowComponentState(await GetComponentState());
    }

    private async Task<ComponentState> GetComponentState()
    {
        ProcessManager processManager = await GetProcessManager();
        if (processManager == null) return ComponentState.SetupRequired;

        if (processManager.isErrorHappens)
        {
            return ComponentState.ExitedWithException;
        }
        else if (processManager.processState)
        {
            return ComponentState.Runned;
        }
        else
        {
            return ComponentState.Stopped;
        }
    }

    private void ShowComponentState(ComponentState componentState)
    {
        switch (componentState)
        {
            case ComponentState.SetupRequired:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorSolidNeutralBrush"];
                ForegroundStatusFontIcon.Glyph = "\uE713";
                ForegroundStatusFontIcon.FontSize = 22;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusSetupRequired"));
                break;
            case ComponentState.Stopped:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                ForegroundStatusFontIcon.Glyph = "\uE7E8";
                ForegroundStatusFontIcon.FontSize = 20;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusStopped"));
                break;
            case ComponentState.Runned:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                ForegroundStatusFontIcon.Glyph = "\uF13E";
                ForegroundStatusFontIcon.FontSize = 28;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusStarted"));
                break;
            case ComponentState.ExitedWithException:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorCautionBrush"];
                ForegroundStatusFontIcon.Glyph = "\uE7BA";
                ForegroundStatusFontIcon.FontSize = 18;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusExceptionHappens"));
                break;
        }
    }

    private void SetStatusToolTip(string text)
    {
        ToolTip toolTip = new();
        toolTip.Content = text;
        ToolTipService.SetToolTip(StatusGrid, toolTip);
    }

    private async void ViewOutputButtonClick()
    {
        var window = await ((App)Application.Current).UnsafeCreateNewWindow<ViewWindow>(id: StoreId);
        window?.SetId(StoreId);
    }

    private async void ViewSettingsButtonClick()
    {
        var window = await ((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>();
        window.NavView_Navigate(typeof(ViewComponentSettingsPage), StoreId, new DrillInNavigationTransitionInfo());
    }


}
