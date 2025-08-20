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

public sealed partial class SitelistButton : UserControl
{
    public Action Click;

    public string Directory;
    public string PackId;
    public SitelistButton()
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
            nameof(Title), typeof(string), typeof(SitelistButton), new PropertyMetadata(string.Empty)
        );

    public string PackName
    {
        get { return (string)GetValue(PackNameProperty); }
        set { SetValue(PackNameProperty, value); }
    }

    public static readonly DependencyProperty PackNameProperty =
        DependencyProperty.Register(
            nameof(PackName), typeof(string), typeof(SitelistButton), new PropertyMetadata(string.Empty)
        );

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        Click?.Invoke();
    }
}
