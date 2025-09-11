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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI;

public sealed partial class StoreCategoryButton : UserControl
{
    public StoreCategoryButton()
    {
        InitializeComponent();

        this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

        PART_BackgroundRect.Visibility = Visibility.Collapsed;
        AnimToPrimary.Begin();
    }
    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(StoreItemLargeButton), new PropertyMetadata(string.Empty)
        );

    private void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        PART_BackgroundRect.Visibility = Visibility.Visible;
        AnimToPrimary.Stop();
        AnimToSecondary.Begin();

    }

    private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        PART_BackgroundRect.Visibility = Visibility.Collapsed;
        AnimToSecondary.Stop();
        AnimToPrimary.Begin();

    }
}
