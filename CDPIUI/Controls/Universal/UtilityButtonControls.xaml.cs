using CDPIUI.Commands;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Markup;
using System;
using System.Collections.ObjectModel;
using System.Numerics;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.Universal
{
    [ContentProperty(Name = nameof(Items))]
    public sealed partial class UtilityButtonControls : UserControl
    {
        private const double ActionButtonMinWidth = 150d;
        private const float VisibilityAnimationOffset = 16f;

        private static readonly TimeSpan ShowAnimationDuration = TimeSpan.FromMilliseconds(180);
        private static readonly TimeSpan HideAnimationDuration = TimeSpan.FromMilliseconds(150);

        private static ILocalizer localizer = Localizer.Get();

        public ObservableCollection<Button> Items { get; } = [];

        public UtilityButtonControls()
        {
            InitializeComponent();

            Items.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is null)
                return;

            foreach (Button button in e.NewItems)
            {
                if (button.MinWidth < ActionButtonMinWidth)
                    button.MinWidth = ActionButtonMinWidth;

                PrepareVisibilityAnimations(button);
            }
        }

        private static void PrepareVisibilityAnimations(Button button)
        {
            // A show animation assigned before the element enters the visual tree also
            // runs for its initial appearance. Configure it after Loaded so that the
            // initial state stays untouched and only later Visibility changes animate.
            if (button.IsLoaded)
            {
                ConfigureVisibilityAnimations(button);
                return;
            }

            button.Loaded -= Button_Loaded;
            button.Loaded += Button_Loaded;
        }

        private static void Button_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            button.Loaded -= Button_Loaded;
            ConfigureVisibilityAnimations(button);
        }

        private static void ConfigureVisibilityAnimations(Button button)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(button, true);

            Compositor compositor = ElementCompositionPreview.GetElementVisual(button).Compositor;

            CompositionAnimationGroup showAnimation = CreateVisibilityAnimation(
                compositor,
                fromTranslation: new Vector3(0, VisibilityAnimationOffset, 0),
                toTranslation: new Vector3(0, 0, 0),
                fromOpacity: 0f,
                toOpacity: 1f,
                duration: ShowAnimationDuration,
                firstControlPoint: new Vector2(0.22f, 1f),
                secondControlPoint: new Vector2(0.36f, 1f));

            CompositionAnimationGroup hideAnimation = CreateVisibilityAnimation(
                compositor,
                fromTranslation: new Vector3(-(float)button.ActualWidth, 0, 0),
                toTranslation: new Vector3(-(float)button.ActualWidth, VisibilityAnimationOffset, 0),
                fromOpacity: 1f,
                toOpacity: 0f,
                duration: HideAnimationDuration,
                firstControlPoint: new Vector2(0.55f, 0f),
                secondControlPoint: new Vector2(1f, 0.45f));

            ElementCompositionPreview.SetImplicitHideAnimation(button, hideAnimation);

            if (button.Visibility == Visibility.Collapsed)
            {
                ElementCompositionPreview.SetImplicitShowAnimation(button, showAnimation);
                return;
            }

            long visibilityCallbackToken = 0;
            visibilityCallbackToken = button.RegisterPropertyChangedCallback(
                UIElement.VisibilityProperty,
                (dependencyObject, _) =>
                {
                    if (dependencyObject is not Button targetButton ||
                        targetButton.Visibility != Visibility.Collapsed)
                    {
                        return;
                    }

                    targetButton.UnregisterPropertyChangedCallback(
                        UIElement.VisibilityProperty,
                        visibilityCallbackToken);
                    ElementCompositionPreview.SetImplicitShowAnimation(targetButton, showAnimation);
                });
        }

        private static CompositionAnimationGroup CreateVisibilityAnimation(
            Compositor compositor,
            Vector3 fromTranslation,
            Vector3 toTranslation,
            float fromOpacity,
            float toOpacity,
            TimeSpan duration,
            Vector2 firstControlPoint,
            Vector2 secondControlPoint)
        {
            CubicBezierEasingFunction easingFunction = compositor.CreateCubicBezierEasingFunction(
                firstControlPoint,
                secondControlPoint);

            Vector3KeyFrameAnimation translationAnimation = compositor.CreateVector3KeyFrameAnimation();
            translationAnimation.Target = "Translation";
            translationAnimation.Duration = duration;
            translationAnimation.InsertKeyFrame(0f, fromTranslation);
            translationAnimation.InsertKeyFrame(1f, toTranslation, easingFunction);

            CompositionAnimationGroup animationGroup = compositor.CreateAnimationGroup();
            animationGroup.Add(translationAnimation);

            return animationGroup;
        }

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(
                nameof(IsLoading), typeof(bool), typeof(UtilityButtonControls), new PropertyMetadata(false)
            );

        public string LoadingStateText
        {
            get { return (string)GetValue(LoadingStateTextProperty); }
            set { SetValue(LoadingStateTextProperty, value); }
        }

        public static readonly DependencyProperty LoadingStateTextProperty =
            DependencyProperty.Register(
                nameof(LoadingStateText), typeof(string), typeof(UtilityButtonControls), new PropertyMetadata(localizer.GetLocalizedString("WorkingOnItTextBlock"))
            );


        public string HelpUrl
        {
            get { return (string)GetValue(HelpUrlProperty); }
            set { SetValue(HelpUrlProperty, value); }
        }

        public static readonly DependencyProperty HelpUrlProperty =
            DependencyProperty.Register(
                nameof(HelpUrl), typeof(string), typeof(UtilityButtonControls), new PropertyMetadata(string.Empty)
            );

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {
            CommandsHandler.HandleCommand($"cdpiui://Help/{HelpUrl}");
        }
    }
}
