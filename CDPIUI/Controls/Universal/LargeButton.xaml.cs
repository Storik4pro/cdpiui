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

namespace CDPIUI.Controls.Universal;

public sealed partial class LargeButton : Button
{
    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(LargeButton), new PropertyMetadata(string.Empty)
        );

    public string ActionIcon
    {
        get { return (string)GetValue(ActionIconProperty); }
        set { SetValue(ActionIconProperty, value); }
    }

    public static readonly DependencyProperty ActionIconProperty =
        DependencyProperty.Register(
            nameof(ActionIcon), typeof(string), typeof(LargeButton), new PropertyMetadata(string.Empty)
        );

    public IconSource Icon
    {
        get { return (IconSource)GetValue(IconProperty); }
        set { SetValue(IconProperty, value); }
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon), typeof(IconSource), typeof(LargeButton), new PropertyMetadata(null)
        );

    public LargeButton()
    {
        InitializeComponent();
    }


}
