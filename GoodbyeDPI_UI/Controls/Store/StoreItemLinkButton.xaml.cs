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

public sealed partial class StoreItemLinkButton : UserControl
{
    public StoreItemLinkButton()
    {
        InitializeComponent();
    }

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(StoreItemLinkButton), new PropertyMetadata(string.Empty)
        );

    public string Url
    {
        get { return (string)GetValue(UrlProperty); }
        set { SetValue(UrlProperty, value); }
    }

    public static readonly DependencyProperty UrlProperty =
        DependencyProperty.Register(
            nameof(Url), typeof(string), typeof(StoreItemLinkButton), new PropertyMetadata(string.Empty)
        );

    public string SiteIcon
    {
        get { return (string)GetValue(SiteIconProperty); }
        set { SetValue(SiteIconProperty, value); }
    }

    public static readonly DependencyProperty SiteIconProperty =
        DependencyProperty.Register(
            nameof(SiteIcon), typeof(string), typeof(StoreItemLinkButton), new PropertyMetadata("\uE774")
        );

    public string ActionIcon
    {
        get { return (string)GetValue(ActionIconProperty); }
        set { SetValue(ActionIconProperty, value); }
    }

    public static readonly DependencyProperty ActionIconProperty =
        DependencyProperty.Register(
            nameof(ActionIcon), typeof(string), typeof(StoreItemLinkButton), new PropertyMetadata("\uE8A7")
        );

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(Url));
    }
}
