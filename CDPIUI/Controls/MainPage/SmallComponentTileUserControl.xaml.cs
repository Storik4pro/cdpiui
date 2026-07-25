using CDPIUI.Controls.Dialogs.ComponentSettings;
using CDPIUI.Core;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.Features;
using CDPIUI.Core.Items;
using CDPIUI.Core.Static;
using CDPIUI.Core.Store.Database;
using CDPIUI.ViewModels;
using CDPIUI.Views.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.MainPage;

public sealed partial class SmallComponentTileUserControl : UserControl
{
    private ILocalizer localizer = Localizer.Get();

    #region Commands

    public static readonly DependencyProperty ShowSettingsCommandProperty =
           DependencyProperty.Register(
               nameof(ShowSettingsCommand),
               typeof(ICommand),
               typeof(SmallComponentTileUserControl),
               new PropertyMetadata(null)
           );

    public ICommand ShowSettingsCommand
    {
        get => (ICommand)GetValue(ShowSettingsCommandProperty);
        set => SetValue(ShowSettingsCommandProperty, value);
    }

    public static readonly DependencyProperty ShowSettingsCommandParameterProperty =
        DependencyProperty.Register(
            nameof(ShowSettingsCommandParameter),
            typeof(object),
            typeof(SmallComponentTileUserControl),
            new PropertyMetadata(null)
        );

    public object ShowSettingsCommandParameter
    {
        get => GetValue(ShowSettingsCommandParameterProperty);
        set => SetValue(ShowSettingsCommandParameterProperty, value);
    }

    #endregion

    public SmallComponentTileUserControl()
    {
        InitializeComponent();

        HandleTheme();
        this.ActualThemeChanged += SmallComponentTileUserControl_ActualThemeChanged;
        // this.Unloaded += SmallComponentTileUserControl_Unloaded;
    }

    #region Properties

    public bool IsOpened
    {
        get { return (bool)GetValue(IsOpenedProperty); }
        set { 
            SetValue(IsOpenedProperty, value);
            SetToolTip();
        }
    }

    public static readonly DependencyProperty IsOpenedProperty =
        DependencyProperty.Register(
            nameof(IsOpened), typeof(bool), typeof(SmallComponentTileUserControl), new PropertyMetadata(false)
        );

    public string StoreId
    {
        get { return (string)GetValue(StoreIdProperty); }
        set
        {
            SetValue(StoreIdProperty, value);
            Init();
        }
    }

    public static readonly DependencyProperty StoreIdProperty =
        DependencyProperty.Register(
            nameof(StoreId), typeof(string), typeof(SmallComponentTileUserControl), new PropertyMetadata(string.Empty)
        );


    public ImageSource CardImageSource
    {
        get { return (ImageSource)GetValue(CardImageSourceProperty); }
        set { SetValue(CardImageSourceProperty, value); }
    }

    public static readonly DependencyProperty CardImageSourceProperty =
        DependencyProperty.Register(
            "UriImageSource", typeof(ImageSource), typeof(SmallComponentTileUserControl), new PropertyMetadata(null)
        );

    public string CardTitle
    {
        get { return (string)GetValue(CardTitleProperty); }
        set { SetValue(CardTitleProperty, value); }
    }

    public static readonly DependencyProperty CardTitleProperty =
        DependencyProperty.Register(
            "Title", typeof(string), typeof(SmallComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    public string CardDeveloper
    {
        get { return (string)GetValue(CardDeveloperProperty); }
        set { SetValue(CardDeveloperProperty, value); }
    }

    public static readonly DependencyProperty CardDeveloperProperty =
        DependencyProperty.Register(
            "CardDeveloper", typeof(string), typeof(SmallComponentTileUserControl), new PropertyMetadata(string.Empty)
        );

    public string CardBackgroundColor
    {
        get { return (string)GetValue(CardBackgroundProperty); }
        set { 
            SetValue(CardBackgroundProperty, value);
            MainBrush.Color = UIHelper.HexToColorConverter(value);
        }
    }

    public static readonly DependencyProperty CardBackgroundProperty =
        DependencyProperty.Register(
            "CardBackgroundColor", typeof(string), typeof(SmallComponentTileUserControl), new PropertyMetadata("#000000")
        );

    #endregion

    #region Handlers

    private void SmallComponentTileUserControl_ActualThemeChanged(FrameworkElement sender, object args)
    {
        HandleTheme();
    }

    private void SmallComponentTileUserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        
    }

    private void SmallComponentTileUserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        this.ActualThemeChanged -= SmallComponentTileUserControl_ActualThemeChanged;
        ComponentTasksManager.Instance.TaskStateUpdated -= TaskUpdated;
        this.Unloaded -= SmallComponentTileUserControl_Unloaded;
        this.SizeChanged -= SmallComponentTileUserControl_SizeChanged;
    }

    private void TaskUpdated(Tuple<string, bool> tuple)
    {
        if (tuple.Item1 != StoreId) return;


        PreferTaskStateActions();
    }

    #endregion


    private void Init()
    {
        ComponentTasksManager.Instance.TaskStateUpdated += TaskUpdated;

        if (DatabaseHelper.Instance.IsItemInstalled(StoreId))
        {
            PreferTaskStateActions();

            bool isAddToAutorun = SettingsManager.Instance.GetValue<bool>(["CONFIGS", StoreId], "usedForAutorun");
            AutorunCheckBox.IsChecked = isAddToAutorun;
            CheckComponentState();
        }
        SetToolTip();
    }

    private void SetToolTip()
    {
        ShowMoreSettingsToolTip.Text = IsOpened ? localizer.GetLocalizedString("ShowLessComponentSettingsText") : localizer.GetLocalizedString("ShowMoreComponentSettingsText");
    }

    private async Task<ProcessService> GetProcessManager()
    {
        return (await ComponentTasksManager.Instance.GetTaskFromId(StoreId))?.ProcessManager;
    }

    private async void CheckComponentState()
    {
        ShowComponentState(await GetComponentState());
    }

    private async Task<ComponentState> GetComponentState()
    {
        ProcessService processManager = await GetProcessManager();
        if (processManager == null) return ComponentState.SetupRequired;

        if (processManager.IsErrorHappens)
        {
            return ComponentState.ExitedWithException;
        }
        else if (processManager.IsProcessRunning)
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
        ForegroundStatusFontIcon.Visibility = Visibility.Visible;
        ForegroundStopStatusFontIcon.Visibility = Visibility.Collapsed;
        switch (componentState)
        {
            case ComponentState.SetupRequired:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorSolidNeutralBrush"];
                ForegroundStopStatusFontIcon.Glyph = "\uE713";
                ForegroundStatusFontIcon.Visibility = Visibility.Collapsed;
                ForegroundStopStatusFontIcon.Visibility = Visibility.Visible;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusSetupRequired"));
                break;
            case ComponentState.Stopped:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                ForegroundStopStatusFontIcon.Glyph = "\uE7E8";
                ForegroundStatusFontIcon.Visibility = Visibility.Collapsed;
                ForegroundStopStatusFontIcon.Visibility = Visibility.Visible;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusStopped"));
                break;
            case ComponentState.Runned:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                ForegroundStatusFontIcon.Glyph = "\uF13E";
                ForegroundStatusFontIcon.FontSize = 18;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusStarted"));
                break;
            case ComponentState.ExitedWithException:
                BackgroundStatusFontIcon.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorCautionBrush"];
                ForegroundStopStatusFontIcon.Glyph = "\uEDAE";
                ForegroundStatusFontIcon.Visibility = Visibility.Collapsed;
                ForegroundStopStatusFontIcon.Visibility = Visibility.Visible;
                SetStatusToolTip(localizer.GetLocalizedString("ComponentStatusExceptionHappens"));
                break;
        }
    }

    private async void PreferTaskStateActions()
    {
        bool isRunned = await ComponentTasksManager.Instance.IsTaskRunned(StoreId);

        if (isRunned)
        {
            PlayToolTip.Text = localizer.GetLocalizedString("Stop");
            PlayGlyph.Glyph = "\uE62E";
            ShowComponentState(ComponentState.Runned);
        }
        else
        {
            PlayToolTip.Text = localizer.GetLocalizedString("Start");
            PlayGlyph.Glyph = "\uF5B0";
            CheckComponentState();
        }

        PlayButton.IsChecked = isRunned;
    }

    private void SetStatusToolTip(string text)
    {
        ToolTip toolTip = new();
        toolTip.Content = text;
        ToolTipService.SetToolTip(StatusGrid, toolTip);
    }

    private void HandleTheme()
    {
        if (this.ActualTheme == ElementTheme.Dark)
        {
            AcrylicBrush.TintColor = UIHelper.HexToColorConverter("#000000");
        }
        else if (this.ActualTheme == ElementTheme.Light)
        {
            AcrylicBrush.TintColor = UIHelper.HexToColorConverter("#FFFFFF");
        }
        else
        {
            AcrylicBrush.TintColor = Application.Current.RequestedTheme == ApplicationTheme.Light ? UIHelper.HexToColorConverter("#FFFFFF") : UIHelper.HexToColorConverter("#000000");
        }
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        bool isRunned = await ComponentTasksManager.Instance.IsTaskRunned(StoreId);

        if (!isRunned)
        {
            ComponentTasksManager.Instance.CreateAndRunNewTask(StoreId);
        }
        else
        {
            await ComponentTasksManager.Instance.StopTask(StoreId);
        }
    }

    private void ShowMoreSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (ShowSettingsCommand != null && ShowSettingsCommand.CanExecute(ShowSettingsCommandParameter))
        {
            ShowSettingsCommand.Execute(ShowSettingsCommandParameter);
            return;
        }
    }

    private async void ViewOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var window = await ((App)Application.Current).UnsafeCreateNewWindow<ViewWindow>(id: StoreId);
    }

    private void AutorunCheckBox_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.SetValue<bool>(["CONFIGS", StoreId], "usedForAutorun", (bool)AutorunCheckBox.IsChecked);
        if ((bool)AutorunCheckBox.IsChecked) ApplicationAutorunManager.AddToAutorun();
    }
}
