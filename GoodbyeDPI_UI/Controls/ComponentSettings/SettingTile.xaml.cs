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
using Microsoft.UI.Xaml.Markup;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI;

public sealed partial class SettingTile : UserControl
{
    public SettingTile()
    {
        InitializeComponent();
    }

    public string Title
    {
        get { return (string)GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(SettingTile), new PropertyMetadata(string.Empty)
        );

    public string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set { SetValue(DescriptionProperty, value); }
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(SettingTile), new PropertyMetadata(string.Empty)
        );

    public string IconGlyph
    {
        get { return (string)GetValue(IconGlyphProperty); }
        set { SetValue(IconGlyphProperty, value); }
    }

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(
            nameof(IconGlyph), typeof(string), typeof(SettingTile), new PropertyMetadata(string.Empty)
        );

    public object InnerContent
    {
        get { return (object)GetValue(InnerContentProperty); }
        set { SetValue(InnerContentProperty, value); }
    }

    public static readonly DependencyProperty InnerContentProperty =
        DependencyProperty.Register(nameof(InnerContent), typeof(object), typeof(SettingTile), new PropertyMetadata(null));

    
}
