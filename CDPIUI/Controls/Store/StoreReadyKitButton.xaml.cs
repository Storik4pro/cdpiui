using CDPIUI.Commands;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace CDPIUI.Controls.Store
{
    public sealed partial class StoreReadyKitButton : UserControl
    {
        private readonly Vector3 _shadowOffset = new(0, 0, 20);

        public event Action<StoreReadyKitButton> Click;
        public UIElement ImageElement => PART_Image;

        public StoreReadyKitButton()
        {
            InitializeComponent();

            SharedShadow.Receivers.Add(ShadowReceiver);
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        }

        public string KitId
        {
            get => (string)GetValue(KitIdProperty);
            set => SetValue(KitIdProperty, value);
        }

        public static readonly DependencyProperty KitIdProperty = DependencyProperty.Register(
            nameof(KitId), typeof(string), typeof(StoreReadyKitButton), new PropertyMetadata(string.Empty));

        public ImageSource CardImageSource
        {
            get => (ImageSource)GetValue(CardImageSourceProperty);
            set => SetValue(CardImageSourceProperty, value);
        }

        public static readonly DependencyProperty CardImageSourceProperty = DependencyProperty.Register(
            nameof(CardImageSource), typeof(ImageSource), typeof(StoreReadyKitButton), new PropertyMetadata(null));

        public string CardTitle
        {
            get => (string)GetValue(CardTitleProperty);
            set => SetValue(CardTitleProperty, value);
        }

        public static readonly DependencyProperty CardTitleProperty = DependencyProperty.Register(
            nameof(CardTitle), typeof(string), typeof(StoreReadyKitButton), new PropertyMetadata(string.Empty));

        public string CardSubtitle
        {
            get => (string)GetValue(CardSubtitleProperty);
            set => SetValue(CardSubtitleProperty, value);
        }

        public static readonly DependencyProperty CardSubtitleProperty = DependencyProperty.Register(
            nameof(CardSubtitle), typeof(string), typeof(StoreReadyKitButton), new PropertyMetadata(string.Empty));

        public Brush CardBackgroundBrush
        {
            get => (Brush)GetValue(CardBackgroundBrushProperty);
            set => SetValue(CardBackgroundBrushProperty, value);
        }

        public static readonly DependencyProperty CardBackgroundBrushProperty = DependencyProperty.Register(
            nameof(CardBackgroundBrush), typeof(Brush), typeof(StoreReadyKitButton),
            new PropertyMetadata(new SolidColorBrush(Microsoft.UI.Colors.SlateGray)));

        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
            nameof(IconSize), typeof(double), typeof(StoreReadyKitButton), new PropertyMetadata(76d));

        public double TitleFontSize
        {
            get => (double)GetValue(TitleFontSizeProperty);
            set => SetValue(TitleFontSizeProperty, value);
        }

        public static readonly DependencyProperty TitleFontSizeProperty = DependencyProperty.Register(
            nameof(TitleFontSize), typeof(double), typeof(StoreReadyKitButton), new PropertyMetadata(18d));

        public List<ViewStoreItemModel> Items
        {
            get => (List<ViewStoreItemModel>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
            nameof(Items), typeof(List<ViewStoreItemModel>), typeof(StoreReadyKitButton), new PropertyMetadata(new List<ViewStoreItemModel>()));

        private void PART_Button_Click(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(this);
        }

        private void PART_Button_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            AnimateTranslateY(-3);
            PART_Button.Translation += _shadowOffset;
        }

        private void PART_Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimateTranslateY(0);
            PART_Button.Translation -= _shadowOffset;
        }

        private void AnimateTranslateY(double target)
        {
            DoubleAnimation animation = new()
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(200),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(animation, TranslateTransform);
            Storyboard.SetTargetProperty(animation, "Y");

            Storyboard storyboard = new();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private void ContainItemsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            CommandsHandler.HandleCommand($"cdpiui://Store/Catalog/{((ViewStoreItemModel)e.ClickedItem).StoreId}");
        }

        private void RootControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Debug.WriteLine($"SIZE CHANGED {this.ActualWidth}, {this.ActualHeight}");
            if (this.ActualHeight > 200)
            {
                VisualStateManager.GoToState(this, "NormalView", false);
            }
            else if (this.ActualHeight > 100)
            {
                Debug.WriteLine(VisualStateManager.GoToState(this, "SSmallView", false));
            }

            if (this.ActualWidth < 400 && this.ActualHeight > 200)
            {
                VisualStateManager.GoToState(this, "BasicView", false);
            }
        }
    }
}
