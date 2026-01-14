using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using CDPI_UI.Helper.Static;
using CDPI_UI.Helper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Troubleshooting;

public sealed partial class DiagnosticResultUserControl : UserControl
{
    public DiagnosticResultUserControl()
    {
        InitializeComponent();
    }

    public string DisplayText
    {
        get { return (string)GetValue(DisplayTextProperty); }
        set { SetValue(DisplayTextProperty, value); }
    }

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(
            nameof(DisplayText), typeof(string), typeof(DiagnosticResultUserControl), new PropertyMetadata(string.Empty)
        );
    public string CurrentStateText
    {
        get { return (string)GetValue(CurrentStateTextProperty); }
        set { SetValue(CurrentStateTextProperty, value); }
    }

    public static readonly DependencyProperty CurrentStateTextProperty =
        DependencyProperty.Register(
            nameof(CurrentStateText), typeof(string), typeof(DiagnosticResultUserControl), new PropertyMetadata(string.Empty)
        );
    public string TargetStateText
    {
        get { return (string)GetValue(TargetStateTextProperty); }
        set { SetValue(TargetStateTextProperty, value); }
    }

    public static readonly DependencyProperty TargetStateTextProperty =
        DependencyProperty.Register(
            nameof(TargetStateText), typeof(string), typeof(DiagnosticResultUserControl), new PropertyMetadata(string.Empty)
        );
    public string ActionAeraText
    {
        get { return (string)GetValue(ActionAeraTextProperty); }
        set { SetValue(ActionAeraTextProperty, value); }
    }

    public static readonly DependencyProperty ActionAeraTextProperty =
        DependencyProperty.Register(
            nameof(ActionAeraText), typeof(string), typeof(DiagnosticResultUserControl), new PropertyMetadata(string.Empty)
        );
    public string HelpText
    {
        get { return (string)GetValue(HelpTextProperty); }
        set { SetValue(HelpTextProperty, value); }
    }

    public static readonly DependencyProperty HelpTextProperty =
        DependencyProperty.Register(
            nameof(HelpText), typeof(string), typeof(DiagnosticResultUserControl), new PropertyMetadata(string.Empty)
        );
    public string HelpUrl
    {
        get { return (string)GetValue(HelpUrlProperty); }
        set { 
            SetValue(HelpUrlProperty, value); 
            if (string.IsNullOrEmpty(value))
            {
                NetHelpAskButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                NetHelpAskButton.Visibility = Visibility.Visible;
            }
        }
    }

    public static readonly DependencyProperty HelpUrlProperty =
        DependencyProperty.Register(
            nameof(HelpUrl), typeof(string), typeof(DiagnosticResultUserControl), new PropertyMetadata(string.Empty)
        );

    public string IconGlyph
    {
        get { return (string)GetValue(IconGlyphProperty); }
        set { 
            SetValue(IconGlyphProperty, value); 

        }
    }

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(
            nameof(IconGlyph), typeof(string), typeof(DiagnosticResultUserControl), new PropertyMetadata(string.Empty)
        );

    public bool ShowTopRectangle
    {
        get { return (bool)GetValue(ShowTopRectangleProperty); }
        set { SetValue(ShowTopRectangleProperty, value); }
    }

    public static readonly DependencyProperty ShowTopRectangleProperty =
        DependencyProperty.Register(
            nameof(ShowTopRectangle), typeof(bool), typeof(DiagnosticResultUserControl), new PropertyMetadata(false)
        );
    public bool IsHighlighted
    {
        get { return (bool)GetValue(IsHighlightedProperty); }
        set { SetValue(IsHighlightedProperty, value); }
    }

    public static readonly DependencyProperty IsHighlightedProperty =
        DependencyProperty.Register(
            nameof(IsHighlighted), typeof(bool), typeof(DiagnosticResultUserControl), new PropertyMetadata(false)
        );
    public bool ShowActionButtons
    {
        get { return (bool)GetValue(ShowActionButtonsProperty); }
        set { 
            SetValue(ShowActionButtonsProperty, value);
            ActionAeraTextBlock.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public static readonly DependencyProperty ShowActionButtonsProperty =
        DependencyProperty.Register(
            nameof(ShowActionButtons), typeof(bool), typeof(DiagnosticResultUserControl), new PropertyMetadata(false)
        );
    public bool IsFixAvailable
    {
        get { return (bool)GetValue(IsFixAvailableProperty); }
        set { SetValue(IsFixAvailableProperty, value); }
    }

    public static readonly DependencyProperty IsFixAvailableProperty =
        DependencyProperty.Register(
            nameof(IsFixAvailable), typeof(bool), typeof(DiagnosticResultUserControl), new PropertyMetadata(false)
        );
    public bool IsHelpAvailable
    {
        get { return (bool)GetValue(IsHelpAvailableProperty); }
        set { SetValue(IsHelpAvailableProperty, value); }
    }

    public static readonly DependencyProperty IsHelpAvailableProperty =
        DependencyProperty.Register(
            nameof(IsHelpAvailable), typeof(bool), typeof(DiagnosticResultUserControl), new PropertyMetadata(false)
        );

    private void GetHelpHyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
    }

    private void NetHelpAskButton_Click(object sender, RoutedEventArgs e)
    {
        UrlOpenHelper.LaunchUrl(HelpUrl);
    }
}
