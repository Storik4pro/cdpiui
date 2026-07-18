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
using CDPI_UI.Helper;
using Microsoft.UI.Xaml.Media.Imaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.WindowControls;

public sealed partial class TitleBarUserControl : UserControl
{

    public Visibility PreviewHintVisibility { get; private set; } = StateHelper.IsPreview ? Visibility.Visible : Visibility.Collapsed;


    

    public TitleBarUserControl()
    {
        InitializeComponent();
    }

    public bool ShowControlsContent
    {
        get { return (bool)GetValue(ShowControlsContentProperty); }
        set { 
            SetValue(ShowControlsContentProperty, value);
            ControlsContentPresenter.Visibility = ShowControlsContent ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public static readonly DependencyProperty ShowControlsContentProperty =
        DependencyProperty.Register(
            nameof(ShowControlsContent), typeof(bool), typeof(TitleBarUserControl), new PropertyMetadata(false)
        );

    public int ControlsContentMinWidth
    {
        get { return (int)GetValue(ControlsContentMinWidthProperty); }
        set { SetValue(ControlsContentMinWidthProperty, value); }
    }

    public static readonly DependencyProperty ControlsContentMinWidthProperty =
        DependencyProperty.Register(
            nameof(ControlsContentMinWidth), typeof(int), typeof(TitleBarUserControl), new PropertyMetadata(0)
        );

    public string Title
    {
        get { return (string)GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(TitleBarUserControl), new PropertyMetadata(string.Empty)
        );

    public ImageSource IconSource
    {
        get { return (ImageSource)GetValue(IconSourceProperty); }
        set { SetValue(IconSourceProperty, value); }
    }

    public static readonly DependencyProperty IconSourceProperty =
        DependencyProperty.Register(
            nameof(IconSource), typeof(ImageSource), typeof(TitleBarUserControl), new PropertyMetadata(new BitmapImage(new Uri("ms-appx://Store/empty.png")))
        );

    public object AdditionalContent
    {
        get { return (object)GetValue(AdditionalContentProperty); }
        set { SetValue(AdditionalContentProperty, value); }
    }

    public static readonly DependencyProperty AdditionalContentProperty =
    DependencyProperty.Register(
        nameof(AdditionalContent), typeof(object), typeof(TitleBarUserControl), new PropertyMetadata(default(object)));


    public object ControlsContent
    {
        get { return (object)GetValue(ControlsContentProperty); }
        set { SetValue(ControlsContentProperty, value); }
    }

    public static readonly DependencyProperty ControlsContentProperty =
    DependencyProperty.Register(nameof(ControlsContentProperty), typeof(object), typeof(TitleBarUserControl), new PropertyMetadata(default(object)));

}
