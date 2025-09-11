using GoodbyeDPI_UI.Controls.Dialogs;
using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.Items;
using GoodbyeDPI_UI.ViewModels;
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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI;

public sealed partial class GoodCheckReportStrategyUserControl : UserControl
{
    private bool IsPointerOnControl = false;
    private bool isRunned = false;

    public string PlayIconTooltip { get; private set; } = "Test this";
    public string FlagIconTooltip { get; private set; } = "Set flag";

    public static readonly DependencyProperty FlagToggledCommandProperty =
            DependencyProperty.Register(
                nameof(FlagToggledCommand),
                typeof(ICommand),
                typeof(GoodCheckReportStrategyUserControl),
                new PropertyMetadata(null)
            );

    public ICommand FlagToggledCommand
    {
        get => (ICommand)GetValue(FlagToggledCommandProperty);
        set => SetValue(FlagToggledCommandProperty, value);
    }

    public static readonly DependencyProperty FlagToggledCommandParameterProperty =
        DependencyProperty.Register(
            nameof(FlagToggledCommandParameter),
            typeof(object),
            typeof(GoodCheckReportStrategyUserControl),
            new PropertyMetadata(null)
        );

    public object FlagToggledCommandParameter
    {
        get => GetValue(FlagToggledCommandParameterProperty);
        set => SetValue(FlagToggledCommandParameterProperty, value);
    }

    public ICommand ViewFullCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand SetFlagCommand { get; }

    public GoodCheckReportStrategyUserControl()
    {
        InitializeComponent();

        ViewFullCommand = new RelayCommand(p => ViewFullArgs());
        PlayCommand = new RelayCommand(p => Play());
        SetFlagCommand = new RelayCommand(p => SetFlag());
        SetVisible(false);
    }

    public string ComponentId
    {
        get { return (string)GetValue(ComponentIdProperty); }
        set { SetValue(ComponentIdProperty, value); }
    }

    public static readonly DependencyProperty ComponentIdProperty =
        DependencyProperty.Register(
            nameof(ComponentId), typeof(string), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(string.Empty)
        );
    public string Args
    {
        get { return (string)GetValue(ArgsProperty); }
        set { SetValue(ArgsProperty, value); }
    }

    public static readonly DependencyProperty ArgsProperty =
        DependencyProperty.Register(
            nameof(Args), typeof(string), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(string.Empty)
        );
    public string FailureCount
    {
        get { return (string)GetValue(FailureCountProperty); }
        set { SetValue(FailureCountProperty, value); }
    }

    public static readonly DependencyProperty FailureCountProperty =
        DependencyProperty.Register(
            nameof(FailureCount), typeof(string), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(string.Empty)
        );
    public string SuccessCount
    {
        get { return (string)GetValue(SuccessCountProperty); }
        set { SetValue(SuccessCountProperty, value); }
    }

    public static readonly DependencyProperty SuccessCountProperty =
        DependencyProperty.Register(
            nameof(SuccessCount), typeof(string), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(string.Empty)
        );

    public bool Flag
    {
        get { return (bool)GetValue(FlagProperty); }
        set { SetValue(FlagProperty, value); }
    }
    public static readonly DependencyProperty FlagProperty =
        DependencyProperty.Register(
            nameof(Flag), typeof(bool), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(false)
        );


    private void SetFlag()
    {
        Flag = FlagIconButton.Checked;
        if (FlagToggledCommandParameter == null) FlagToggledCommandParameter = Tuple.Create(Args, FlagIconButton.Checked);

        FlagIconTooltip = FlagIconButton.Checked ? "Remove flag" : "Set flag";

        if (FlagToggledCommand != null && FlagToggledCommand.CanExecute(FlagToggledCommandParameter))
        {
            FlagToggledCommand.Execute(FlagToggledCommandParameter);
            return;
        }
    }
    private void Play()
    {
        if (ComponentId != null)
        {
            PlayAsync();
        }
    }

    private async void PlayAsync()
    {
        if (!isRunned)
        {
            isRunned = true;
            ProcessManager.Instance.onProcessStateChanged += ProcessManager_onProcessStateChanged;

            await ProcessManager.Instance.StopProcess();
            
            await ProcessManager.Instance.StartProcess(ComponentId, Args);

        }
        else
        {
            await ProcessManager.Instance.StopProcess();
            PlayIconButton.Checked = false;
            PlayIconTooltip = "Test this";
            if (!IsPointerOnControl)
                PlayIconButton.Visibility = Visibility.Collapsed;
            isRunned = false;
            ProcessManager.Instance.onProcessStateChanged -= ProcessManager_onProcessStateChanged;
        }
    }

    private void ProcessManager_onProcessStateChanged(string obj)
    {
        if (obj == "started")
        {
            PlayIconButton.Checked = true;
            PlayIconTooltip = "Stop testing";
            PlayIconButton.Visibility = Visibility.Visible;
            isRunned = true;
        }
        else
        {
            PlayIconButton.Checked = false;
            PlayIconTooltip = "Test this";
            if (!IsPointerOnControl)
                PlayIconButton.Visibility = Visibility.Collapsed;
            isRunned = false;
        }
    }

    private void ViewFullArgs()
    {
        ViewApplyArgsContentDialog dialog = new()
        {
            XamlRoot = this.XamlRoot,
            Title = "View arguments",
            DialogTitle = string.Empty,
            SeparationTextVisible = Visibility.Collapsed,
            Args = [this.Args ?? string.Empty]

        };
        _ = dialog.ShowAsync();
    }

    private void SetVisible(bool isVisible)
    {
        if (!isVisible)
        {
            if (!PlayIconButton.Checked)
            {
                PlayIconButton.Visibility = Visibility.Collapsed;
            }
            if (!FlagIconButton.Checked)
            {
                FlagIconButton.Visibility = Visibility.Collapsed;
            }
            ViewIconButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            PlayIconButton.Visibility = Visibility.Visible;
            FlagIconButton.Visibility = Visibility.Visible;
            ViewIconButton.Visibility = Visibility.Visible;
        }

    }

    private void UserControl_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        IsPointerOnControl = true;
        SetVisible(true);
    }

    private void UserControl_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        IsPointerOnControl = false;
        SetVisible(false);
    }
}
