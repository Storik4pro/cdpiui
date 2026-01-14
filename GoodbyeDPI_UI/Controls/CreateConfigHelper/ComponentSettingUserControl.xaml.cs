using CDPI_UI.Controls.Dialogs.ComponentSettings;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Forms;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using UserControl = Microsoft.UI.Xaml.Controls.UserControl;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI;


public class EnumModel
{
    public string DisplayName { get; set; }
    public string ActualValue { get; set; }
}

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
            if (Type == "file_path" && !string.IsNullOrEmpty(value))
            {
                value = $"\"{value.Replace("\"", "")}\"";
                SetValue(TextValueProperty, value);
                NotifyValueChanged();
            }
            else
            {
                SetValue(TextValueProperty, value);
            }
            
            if (value != MainTextBox.Text) MainTextBox.Text = value;
            if (Type == "enum") SetValueToEnum();
            if (Type == "file_path") CheckFileOpenButtons();
        }
    }

    public static readonly DependencyProperty TextValueProperty =
        DependencyProperty.Register(
            nameof(TextValue), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );
    public string Type
    {
        get { return (string)GetValue(TypeProperty); }
        set { 
            SetValue(TypeProperty, value);
            CheckType();
        }
    }

    public static readonly DependencyProperty TypeProperty =
        DependencyProperty.Register(
            nameof(Type), typeof(string), typeof(ComponentSettingUserControl), new PropertyMetadata(string.Empty)
        );
    public List<EnumModel> AvailableEnumValues
    {
        get { return (List<EnumModel>)GetValue(AvailableEnumValuesProperty); }
        set { 
            SetValue(AvailableEnumValuesProperty, value);
            SetValueToEnum();
        }
    }

    public static readonly DependencyProperty AvailableEnumValuesProperty =
        DependencyProperty.Register(
            nameof(AvailableEnumValues), typeof(List<EnumModel>), typeof(ComponentSettingUserControl), new PropertyMetadata(new List<EnumModel>())
        );
    public bool EnableTextInput
    {
        get { return (bool)GetValue(EnableTextInputProperty); }
        set { 
            SetValue(EnableTextInputProperty, value);
            // CheckSettingType();
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

    
    private void CheckType()
    {
        MainCheckBox.Visibility = Visibility.Collapsed;
        MainTextBox.Visibility = Visibility.Collapsed;
        MainToggleSwitch.Visibility = Visibility.Collapsed;
        EnumPropertyComboBox.Visibility = Visibility.Collapsed;
        FilePathStackPanel.Visibility = Visibility.Collapsed;

        switch (Type)
        {
            case "flag":
                MainToggleSwitch.Visibility = Visibility.Visible;
                break;
            case "string":
                MainCheckBox.Visibility = Visibility.Visible;
                MainTextBox.Visibility = Visibility.Visible;
                break;
            case "integer":
                MainCheckBox.Visibility = Visibility.Visible;
                MainTextBox.Visibility = Visibility.Visible;
                break;
            case "file_path":
                MainCheckBox.Visibility = Visibility.Visible;
                FilePathStackPanel.Visibility = Visibility.Visible;
                
                CheckFileOpenButtons();
                if (!string.IsNullOrEmpty(TextValue)) TextValue = $"\"{TextValue.Replace("\"", "")}\"";
                break;
            case "enum":
                EnumPropertyComboBox.Visibility = Visibility.Visible;
                SetValueToEnum();
                break;
        }
    }

    private void SetValueToEnum()
    {
        EnumPropertyComboBox.SelectedItem = AvailableEnumValues.FirstOrDefault(x => ((EnumModel)x).ActualValue == TextValue);
    }


    private void ToggleChecked()
    {
        bool isChecked;
        if (EnableTextInput || Type == "file_path")
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

    private void EnumPropertyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        string val = ((EnumModel)EnumPropertyComboBox.SelectedItem)?.ActualValue?? TextValue;
        if (val != TextValue)
        {
            TextValue = val;
            NotifyValueChanged();
        }
    }

    private void CheckFileOpenButtons()
    {
        SelectFileButton.Visibility = Visibility.Collapsed;
        ViewSelectedFileButton.Visibility = Visibility.Collapsed;
        RemoveFileButton.Visibility = Visibility.Collapsed;

        Debug.WriteLine(TextValue);

        if (string.IsNullOrEmpty(TextValue))
        {
            SelectFileButton.Visibility = Visibility.Visible;
        }
        else
        {
            ViewSelectedFileButton.Visibility = Visibility.Visible;
            RemoveFileButton.Visibility = Visibility.Visible;
            SelectedFileText.Text = Path.GetFileName(TextValue.Replace("\"", ""));
        }

        
    }

    private void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        string filePath;
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Title = "Choose text file";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            openFileDialog.Filter = "TXT files (*.txt)|*.txt";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = openFileDialog.FileName;
            }
            else
            {
                return;
            }
        }
        TextValue = $"\"{filePath}\"";
        SelectedFileText.Text = Path.GetFileName(filePath);
        NotifyValueChanged();
    }

    private async void ViewSelectedFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (!SettingsManager.Instance.GetValue<bool>("FILEOPENACTIONS", "isDialogShown") || !SettingsManager.Instance.GetValueOrDefault<bool>("FILEOPENACTIONS", "doNotRemindAgain", defaultValue: true))
        {
            EditSitelistAskApplicationContentDialog dialog = new EditSitelistAskApplicationContentDialog()
            {
                XamlRoot = this.XamlRoot,
                FilePath = TextValue.Replace("\"", ""),
            };
            await dialog.ShowAsync();
            if (dialog.IsSuccess)
                SettingsManager.Instance.SetValue("FILEOPENACTIONS", "isDialogShown", true);
        }
        else
        {
            Utils.OpenFile(TextValue.Replace("\"", ""));
        }
        
    }

    private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
    {
        TextValue = string.Empty;
        NotifyValueChanged();
    }
}
