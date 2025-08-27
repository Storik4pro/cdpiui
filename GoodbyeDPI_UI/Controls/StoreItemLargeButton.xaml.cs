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

namespace GoodbyeDPI_UI;

public sealed partial class StoreItemLargeButton : UserControl
{
    private TranslateTransform _translate;
    private Button _button;
    private Rectangle _bottomRect;
    private Brush _origBrush;
    private Vector3 shadowVector = new Vector3(0, 0, 20);

    public Action<StoreItemLargeButton> Click;
    public UIElement imageElement;
    public StoreItemLargeButton()
    {
        InitializeComponent();

        SetValue(PreferredWidthProperty, (double)175);

        _translate = (TranslateTransform)this.FindName("PART_Translate");
        _button = (Button)FindName("PART_Button");
        _bottomRect = (Rectangle)FindName("PART_BottomRect");

        _origBrush = _bottomRect.Fill;

        SharedShadow.Receivers.Add(BackgroundGrid);
        this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

        this.SizeChanged += (s, e) =>
        {
            UserControl sender = s as UserControl;
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
            nameof(StoreId), typeof(string), typeof(StoreItemLargeButton), new PropertyMetadata(string.Empty)
        );

    public ImageSource CardImageSource
    {
        get { return (ImageSource)GetValue(CardImageSourceProperty); }
        set { SetValue(CardImageSourceProperty, value); }
    }

    public static readonly DependencyProperty CardImageSourceProperty =
        DependencyProperty.Register(
            "ImageSource", typeof(ImageSource), typeof(StoreItemLargeButton), new PropertyMetadata(null)
        );

    public string CardTitle
    {
        get { return (string)GetValue(CardTitleProperty); }
        set { SetValue(CardTitleProperty, value); }
    }

    public static readonly DependencyProperty CardTitleProperty =
        DependencyProperty.Register(
            "Title", typeof(string), typeof(StoreItemLargeButton), new PropertyMetadata(string.Empty)
        );

    public string CardPrice
    {
        get { return (string)GetValue(CardPriceProperty); }
        set { SetValue(CardPriceProperty, value); }
    }

    public static readonly DependencyProperty CardPriceProperty =
        DependencyProperty.Register(
            "Price", typeof(string), typeof(StoreItemLargeButton), new PropertyMetadata(string.Empty)
        );

    public Color CardBackgroundColor
    {
        get { return (Color)GetValue(CardBackgroundProperty); }
        set { SetValue(CardBackgroundProperty, value); }
    }

    public static readonly DependencyProperty CardBackgroundProperty =
        DependencyProperty.Register(
            "BackgroundColor", typeof(Color), typeof(StoreItemLargeButton), new PropertyMetadata(null)
        );

    public double PreferredWidth
    {
        get { return (double)GetValue(PreferredWidthProperty); }
    }

    public static readonly DependencyProperty PreferredWidthProperty =
        DependencyProperty.Register(
            nameof(PreferredWidth), typeof(double), typeof(StoreItemLargeButton), new PropertyMetadata(null)
        );


    private void Image_Loaded(object sender, RoutedEventArgs e)
    {

    }

    private void PART_Button_Click(object sender, RoutedEventArgs e)
    {
        Click?.Invoke(this);
    }
}
