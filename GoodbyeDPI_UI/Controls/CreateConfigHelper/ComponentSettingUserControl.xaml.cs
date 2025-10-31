using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI;

public sealed partial class ComponentSettingUserControl : UserControl
{
    public static readonly DependencyProperty ToggledCommandProperty =
            DependencyProperty.Register(
                nameof(ToggledCommand),
                typeof(ICommand),
                typeof(ComponentSettingUserControl),
                new PropertyMetadata(null)
            );

    public ICommand ToggledCommand
    {
        get => (ICommand)GetValue(ToggledCommandProperty);
        set => SetValue(ToggledCommandProperty, value);
    }

    public static readonly DependencyProperty ToggledParameterProperty =
        DependencyProperty.Register(
            nameof(ToggledParameter),
            typeof(object),
            typeof(ComponentSettingUserControl),
            new PropertyMetadata(null)
        );

    public object ToggledParameter
    {
        get => GetValue(ToggledParameterProperty);
        set => SetValue(ToggledParameterProperty, value);
    }

    public static readonly DependencyProperty ValueChangedCommandProperty =
            DependencyProperty.Register(
                nameof(ValueChangedCommand),
                typeof(ICommand),
                typeof(ComponentSettingUserControl),
                new PropertyMetadata(null)
            );

    public ICommand ValueChangedCommand
    {
        get => (ICommand)GetValue(ValueChangedCommandProperty);
        set => SetValue(ValueChangedCommandProperty, value);
    }

    public static readonly DependencyProperty ValueChangedParameterProperty =
        DependencyProperty.Register(
            nameof(ValueChangedParameter),
            typeof(object),
            typeof(ComponentSettingUserControl),
            new PropertyMetadata(null)
        );

    public object ValueChangedParameter
    {
        get => GetValue(ValueChangedParameterProperty);
        set => SetValue(ValueChangedParameterProperty, value);
    }

    public ComponentSettingUserControl()
    {
        InitializeComponent();
    }
    public string Guid
    {
        get { return (string)GetValue(GuidProperty); }
        set { SetValue(GuidProperty, value); }
    }

    public static readonly DependencyProperty GuidProperty =
        DependencyProperty.Register(
            nameof(Guid), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );
    public string DisplayName
    {
        get { return (string)GetValue(DisplayNameProperty); }
        set { SetValue(DisplayNameProperty, value); }
    }

    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(
            nameof(DisplayName), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );
    public string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set { SetValue(DescriptionProperty, value); }
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );
    public string TextValue
    {
        get { return (string)GetValue(TextValueProperty); }
        set { 
            SetValue(TextValueProperty, value); 
            if (value != MainTextBox.Text) MainTextBox.Text = value;
        }
    }

    public static readonly DependencyProperty TextValueProperty =
        DependencyProperty.Register(
            nameof(TextValue), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );
    public bool EnableTextInput
    {
        get { return (bool)GetValue(EnableTextInputProperty); }
        set { 
            SetValue(EnableTextInputProperty, value);
            CheckSettingType();
        }
    }

    public static readonly DependencyProperty EnableTextInputProperty =
        DependencyProperty.Register(
            nameof(EnableTextInput), typeof(bool), typeof(ComponentSettingUserControl), new PropertyMetadata(false)
        );
    public bool IsSettingChecked
    {
        get { return (bool)GetValue(IsSettingCheckedProperty); }
        set { 
            SetValue(IsSettingCheckedProperty, value);
            if (MainCheckBox.IsChecked != value) MainCheckBox.IsChecked = value;
            if (MainToggleSwitch.IsOn != value) MainToggleSwitch.IsOn = value;
        }
    }

    public static readonly DependencyProperty IsSettingCheckedProperty =
        DependencyProperty.Register(
            nameof(IsSettingChecked), typeof(bool), typeof(ComponentSettingUserControl), new PropertyMetadata(false)
        );

    private void CheckSettingType()
    {
        if (!EnableTextInput)
        {
            MainCheckBox.Visibility = Visibility.Collapsed;
            MainToggleSwitch.Visibility = Visibility.Visible;
        }
        else
        {
            MainCheckBox.Visibility = Visibility.Visible;
            MainToggleSwitch.Visibility = Visibility.Collapsed;
        }
    }

    private void ToggleChecked()
    {
        bool isChecked;
        if (EnableTextInput)
        {
            isChecked = (bool)MainCheckBox.IsChecked;
        }
        else
        {
            isChecked = (bool)MainToggleSwitch.IsOn;
        }

        NotifyToggled(isChecked);
    }

    private void NotifyToggled(bool isChecked)
    {
        ToggledParameter ??= Tuple.Create(Guid, isChecked);
        if (ToggledCommand != null && ToggledCommand.CanExecute(ToggledParameter))
        {
            ToggledCommand.Execute(ToggledParameter);
        }
        ToggledParameter = null;
    }

    private void NotifyValueChanged()
    {
        ValueChangedParameter ??= Tuple.Create(Guid, TextValue);
        if (ValueChangedCommand != null && ValueChangedCommand.CanExecute(ValueChangedParameter))
        {
            ValueChangedCommand.Execute(ValueChangedParameter);
        }
        ValueChangedParameter = null;
    }

    private void MainCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        ToggleChecked();
    }

    private void MainCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        ToggleChecked();
    }

    private void MainToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        ToggleChecked();
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TextValue = MainTextBox.Text;
        NotifyValueChanged();
    }
}
