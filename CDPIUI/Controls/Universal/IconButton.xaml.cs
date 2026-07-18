using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
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

public sealed partial class IconButton : UserControl
{
    public static readonly DependencyProperty ClickCommandProperty =
            DependencyProperty.Register(
                nameof(ClickCommand),
                typeof(ICommand),
                typeof(IconButton),
                new PropertyMetadata(null)
            );

    public ICommand ClickCommand
    {
        get => (ICommand)GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    public static readonly DependencyProperty ClickCommandParameterProperty =
        DependencyProperty.Register(
            nameof(ClickCommandParameter),
            typeof(object),
            typeof(IconButton),
            new PropertyMetadata(null)
        );

    public object ClickCommandParameter
    {
        get => GetValue(ClickCommandParameterProperty);
        set => SetValue(ClickCommandParameterProperty, value);
    }

    private bool IsPointerOnButton = false;

    public IconButton()
    {
        InitializeComponent();
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }

    public string DisplayText
    {
        get { return (string)GetValue(DisplayTextroperty); }
        set
        {
            SetValue(DisplayTextroperty, value);
            MainTextBlock.Text = value;
            MainTextBlock.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public static readonly DependencyProperty DisplayTextroperty =
        DependencyProperty.Register(
            nameof(DisplayText), typeof(string), typeof(IconButton), new PropertyMetadata(string.Empty)
        );

    public string IconGlyph
    {
        get { return (string)GetValue(IconGlyphProperty); }
        set { 
            SetValue(IconGlyphProperty, value);
            if (!Checked)
            {
                Icon.Glyph = value;
                Icon.Style = (Style)Resources["FontIconDefaultColor"];
                MainTextBlock.Style = (Style)Resources["TextDefaultColor"];
            }
        }
    }

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(
            nameof(IconGlyph), typeof(string), typeof(IconButton), new PropertyMetadata(string.Empty)
        );
    public string CheckedIconGlyph
    {
        get { return (string)GetValue(CheckedIconGlyphProperty); }
        set { 
            SetValue(CheckedIconGlyphProperty, value);
            if (Checked)
            {
                Icon.Glyph = value;
                Icon.Style = (Style)Resources["FontIconAccentColor"];
            }
        }
    }

    public static readonly DependencyProperty CheckedIconGlyphProperty =
        DependencyProperty.Register(
            nameof(CheckedIconGlyph), typeof(string), typeof(IconButton), new PropertyMetadata(string.Empty)
        );
    public bool IsButtonToggle
    {
        get { return (bool)GetValue(IsButtonToggleProperty); }
        set { SetValue(IsButtonToggleProperty, value); }
    }

    public static readonly DependencyProperty IsButtonToggleProperty =
        DependencyProperty.Register(
            nameof(IsButtonToggle), typeof(bool), typeof(IconButton), new PropertyMetadata(false)
        );
    public bool Checked
    {
        get { return (bool)GetValue(CheckedProperty); }
        set { 
            SetValue(CheckedProperty, value);
            if (value)
            {
                Icon.Style = IsPointerOnButton ? (Style)Resources["FontIconAccentColor"] : (Style)Resources["FontIconAccentTertiaryColor"];
                MainTextBlock.Style = IsPointerOnButton ? (Style)Resources["TextAccentColor"] : (Style)Resources["TextAccentTertiaryColor"];
                Icon.Glyph = !string.IsNullOrEmpty(CheckedIconGlyph) ? CheckedIconGlyph : IconGlyph;
            }
            else
            {
                Icon.Style = IsPointerOnButton ? (Style)Resources["FontIconAccentColor"] : (Style)Resources["FontIconDefaultColor"];
                MainTextBlock.Style = IsPointerOnButton ? (Style)Resources["TextAccentColor"] : (Style)Resources["TextDefaultColor"];
                Icon.Glyph = IconGlyph;
            }
        }
    }

    public static readonly DependencyProperty CheckedProperty =
        DependencyProperty.Register(
            nameof(Checked), typeof(bool), typeof(IconButton), new PropertyMetadata(false)
        );

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (IsButtonToggle)
        {
            if (Checked)
            {
                Checked = false;
            }
            else
            {
                Checked = true;
            }
        }
        if (ClickCommand != null && ClickCommand.CanExecute(ClickCommandParameter))
        {
            ClickCommand.Execute(ClickCommandParameter);
            return;
        }
    }

    private void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Icon.Style = (Style)Resources["FontIconAccentColor"];
        MainTextBlock.Style = (Style)Resources["TextAccentColor"];

        IsPointerOnButton = true;
    }

    private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        IsPointerOnButton = false;
        if (Checked)
        {
            Icon.Style = (Style)Resources["FontIconAccentTertiaryColor"];
            MainTextBlock.Style = (Style)Resources["TextAccentTertiaryColor"];
        }
        else
        {
            Icon.Style = (Style)Resources["FontIconDefaultColor"];
            MainTextBlock.Style = (Style)Resources["TextDefaultColor"];
            
        }
    }
}
