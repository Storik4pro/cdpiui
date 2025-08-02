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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI;

public class GlyphToFontIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var glyph = value as string;
        if (string.IsNullOrWhiteSpace(glyph))
            return null;

        return new FontIcon { Glyph = glyph };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public sealed partial class SettingTileControlElement : UserControl
{
    public SettingTileControlElement()
    {
        InitializeComponent();
        ShowTopRectangle = true;
    }

    public string Title
    {
        get { return (string)GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(SettingTileControlElement), new PropertyMetadata(string.Empty)
        );

    public string HeaderIconGlyph
    {
        get { return (string)GetValue(HeaderIconGlyphProperty); }
        set { 
            SetValue(HeaderIconGlyphProperty, value);
        }
    }

    public static readonly DependencyProperty HeaderIconGlyphProperty =
        DependencyProperty.Register(
            nameof(HeaderIconGlyph), typeof(string), typeof(SettingTileControlElement), new PropertyMetadata(string.Empty)
        );

    public string ActionIconGlyph
    {
        get { return (string)GetValue(ActionIconGlyphProperty); }
        set { SetValue(ActionIconGlyphProperty, value); }
    }

    public static readonly DependencyProperty ActionIconGlyphProperty =
        DependencyProperty.Register(
            nameof(ActionIconGlyph), typeof(string), typeof(SettingTileControlElement), new PropertyMetadata(string.Empty)
        );

    public bool ShowTopRectangle
    {
        get { return (bool)GetValue(ShowTopRectangleProperty); }
        set { 
            SetValue(ShowTopRectangleProperty, value); 
            TopRectangle.Visibility = value? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public static readonly DependencyProperty ShowTopRectangleProperty =
        DependencyProperty.Register(
            nameof(ShowTopRectangle), typeof(bool), typeof(SettingTileControlElement), new PropertyMetadata(true)
        );

    public bool IsClickEnabled
    {
        get { return (bool)GetValue(IsClickEnabledProperty); }
        set { SetValue(IsClickEnabledProperty, value); }
    }

    public static readonly DependencyProperty IsClickEnabledProperty =
        DependencyProperty.Register(
            nameof(IsClickEnabled), typeof(bool), typeof(SettingTileControlElement), new PropertyMetadata(false)
        );

    public object InnerContent
    {
        get { return (object)GetValue(InnerContentProperty); }
        set { SetValue(InnerContentProperty, value); }
    }

    public static readonly DependencyProperty InnerContentProperty =
        DependencyProperty.Register(nameof(InnerContent), typeof(object), typeof(SettingTileControlElement), new PropertyMetadata(null));

    private void SettingsCard_Click(object sender, RoutedEventArgs e)
    {

    }
}
