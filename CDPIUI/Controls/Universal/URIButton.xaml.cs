using CDPIUI.Commands;
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
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.Universal;

public sealed partial class URIButton : Button
{
    public string URI
    {
        get { return (string)GetValue(URIMessageProperty); }
        set { SetValue(URIMessageProperty, value); }
    }

    public static readonly DependencyProperty URIMessageProperty =
        DependencyProperty.Register("URI", typeof(string), typeof(URIButton), new PropertyMetadata(string.Empty));

    public string UriName
    {
        get { return (string)GetValue(UriNameMessageProperty); }
        set { 
            SetValue(UriNameMessageProperty, value);
            Debug.WriteLine(UriName);
        }
    }

    public static readonly DependencyProperty UriNameMessageProperty =
        DependencyProperty.Register(nameof(UriName), typeof(string), typeof(URIButton), new PropertyMetadata("Link"));



    public URIButton()
    {
        InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        CommandsHandler.HandleCommand(URI);
    }
}
