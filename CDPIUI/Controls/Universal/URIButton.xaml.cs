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

public sealed partial class URIButton : UserControl
{
    public string URI
    {
        get { return (string)GetValue(URIMessageProperty); }
        set { SetValue(URIMessageProperty, value); }
    }

    public static readonly DependencyProperty URIMessageProperty =
        DependencyProperty.Register("URI", typeof(string), typeof(URIButton), new PropertyMetadata(string.Empty));

    public string DisplayText
    {
        get { return (string)GetValue(DisplayTextProperty); }
        set  { SetValue(DisplayTextProperty, value); }
    }

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(
            nameof(DisplayText), typeof(string), typeof(URIButton), new PropertyMetadata(string.Empty)
        );

    public URIButton()
    {
        InitializeComponent();
    }
}
