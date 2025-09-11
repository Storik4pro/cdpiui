using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI;

public sealed partial class StoreItemSmallButton : UserControl
{
    private TranslateTransform _translate;
    private Button _button;
    private Rectangle _bottomRect;
    private Brush _origBrush;
    private Vector3 shadowVector = new Vector3(0, 0, 20);

    public Action<StoreItemSmallButton> Click;
    public UIElement imageElement;
    public StoreItemSmallButton()
    {
        InitializeComponent();

        SetValue(PreferredWidthProperty, (double)365);
        PART_Button.Width = 365;

        _translate = (TranslateTransform)this.FindName("PART_Translate");
        _button = (Button)FindName("PART_Button");
        _bottomRect = (Rectangle)FindName("PART_BottomRect");

        _origBrush = _bottomRect.Fill;

        SharedShadow.Receivers.Add(BackgroundGrid);
        this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

        this.SizeChanged += (s, e) =>
        {
            PART_Button.Width = e.NewSize.Width-15;
            PART_Button.Height = e.NewSize.Height;
        };

        imageElement = PART_Image;
    }

    private void OnButtonPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(_button, "Normal", true);

        AnimateTranslateY(-3, durationMs: 200);

        _bottomRect.Fill = (Brush)Application.Current.Resources["ControlFillColorTertiaryBrush"];
        PART_Button.Translation += shadowVector;
    }

    private void OnButtonPointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(_button, "Normal", true);
        AnimateTranslateY(0, durationMs: 200);

        _bottomRect.Fill = _origBrush;
        PART_Button.Translation -= shadowVector;
    }

    private void AnimateTranslateY(double to, double durationMs)
    {
        var dbl = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(dbl, _translate);
        Storyboard.SetTargetProperty(dbl, "Y");

        var sb = new Storyboard();
        sb.Children.Add(dbl);
        sb.Begin();
    }

    public string StoreId
    {
        get { return (string)GetValue(StoreIdProperty); }
        set { SetValue(StoreIdProperty, value); }
    }

    public static readonly DependencyProperty StoreIdProperty =
        DependencyProperty.Register(
            nameof(StoreId), typeof(string), typeof(StoreItemSmallButton), new PropertyMetadata(string.Empty)
        );

    public ImageSource CardImageSource
    {
        get { return (ImageSource)GetValue(CardImageSourceProperty); }
        set { SetValue(CardImageSourceProperty, value); }
    }

    public static readonly DependencyProperty CardImageSourceProperty =
        DependencyProperty.Register(
            "ImageSource", typeof(ImageSource), typeof(StoreItemSmallButton), new PropertyMetadata(null)
        );

    public string CardTitle
    {
        get { return (string)GetValue(CardTitleProperty); }
        set { SetValue(CardTitleProperty, value); }
    }

    public static readonly DependencyProperty CardTitleProperty =
        DependencyProperty.Register(
            "Title", typeof(string), typeof(StoreItemSmallButton), new PropertyMetadata(string.Empty)
        );

    public string CardDeveloper
    {
        get { return (string)GetValue(CardDeveloperProperty); }
        set { SetValue(CardDeveloperProperty, value); }
    }

    public static readonly DependencyProperty CardDeveloperProperty =
        DependencyProperty.Register(
            "Developer", typeof(string), typeof(StoreItemSmallButton), new PropertyMetadata(string.Empty)
        );
    public string CardPrice
    {
        get { return (string)GetValue(CardPriceProperty); }
        set { SetValue(CardPriceProperty, value); }
    }

    public static readonly DependencyProperty CardPriceProperty =
        DependencyProperty.Register(
            "Price", typeof(string), typeof(StoreItemSmallButton), new PropertyMetadata(string.Empty)
        );

    public Brush CardBackgroundBrush
    {
        get { return (Brush)GetValue(CardBackgroundProperty); }
        set { SetValue(CardBackgroundProperty, value); }
    }

    public static readonly DependencyProperty CardBackgroundProperty =
        DependencyProperty.Register(
            nameof(CardBackgroundBrush), 
            typeof(Brush), 
            typeof(StoreItemSmallButton), 
            new PropertyMetadata(new SolidColorBrush(Colors.Transparent))
        );
    public double PreferredWidth
    {
        get { return (double)GetValue(PreferredWidthProperty); }
    }

    public static readonly DependencyProperty PreferredWidthProperty =
        DependencyProperty.Register(
            nameof(PreferredWidth), typeof(double), typeof(StoreItemSmallButton), new PropertyMetadata(null)
        );

    private void PART_Button_Click(object sender, RoutedEventArgs e)
    {
        Click?.Invoke(this);
    }
}
