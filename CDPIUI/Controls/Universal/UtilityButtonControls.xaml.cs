using CDPIUI.Commands;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Markup;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using Windows.UI.ViewManagement;
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

        private static readonly TimeSpan ShowAnimationDuration = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan HideAnimationDuration = TimeSpan.FromMilliseconds(150);
        private static readonly TimeSpan RepositionAnimationDuration = HideAnimationDuration * 2;

        private static ILocalizer localizer = Localizer.Get();
        private readonly bool animationsEnabled;
        private readonly HashSet<Button> configuredButtons = [];

        public ObservableCollection<Button> Items { get; } = [];

        public UtilityButtonControls()
        {
            InitializeComponent();

            animationsEnabled = new UISettings().AnimationsEnabled;
            Items.CollectionChanged += Items_CollectionChanged;
            Loaded += UtilityButtonControls_Loaded;
        }

        private void UtilityButtonControls_Loaded(object sender, RoutedEventArgs e)
        {
            if (!animationsEnabled)
                return;

            foreach (Button button in Items)
                PrepareVisibilityAnimations(button);
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is null)
                return;

            foreach (Button button in e.NewItems)
            {
                if (button.MinWidth < ActionButtonMinWidth)
                    button.MinWidth = ActionButtonMinWidth;

                if (animationsEnabled && IsLoaded)
                    PrepareVisibilityAnimations(button);
            }
        }

        private void PrepareVisibilityAnimations(Button button)
        {
            if (!configuredButtons.Add(button))
                return;

            if (button.IsLoaded)
            {
                ConfigureVisibilityAnimations(button);
                return;
            }

            button.Loaded -= Button_Loaded;
            button.Loaded += Button_Loaded;
        }

        private void Button_Loaded(object sender, RoutedEventArgs e)
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
                duration: ShowAnimationDuration,
                firstControlPoint: new Vector2(0.22f, 1f),
                secondControlPoint: new Vector2(0.36f, 1f));

            CompositionAnimationGroup hideAnimation = CreateVisibilityAnimation(
                compositor,
                fromTranslation: Vector3.Zero,
                toTranslation: new Vector3(0, VisibilityAnimationOffset, 0),
                duration: HideAnimationDuration,
                firstControlPoint: new Vector2(0.55f, 0f),
                secondControlPoint: new Vector2(1f, 0.45f));

            ConfigureRepositionAnimation(button, compositor);
            ElementCompositionPreview.SetImplicitHideAnimation(button, hideAnimation);

            bool showAnimationIsConfigured = button.Visibility == Visibility.Collapsed;
            if (showAnimationIsConfigured)
                ElementCompositionPreview.SetImplicitShowAnimation(button, showAnimation);

            button.RegisterPropertyChangedCallback(
                UIElement.VisibilityProperty,
                (dependencyObject, _) =>
                {
                    if (dependencyObject is not Button targetButton)
                        return;

                    if (targetButton.Visibility == Visibility.Collapsed)
                    {
                        if (!showAnimationIsConfigured)
                        {
                            ElementCompositionPreview.SetImplicitShowAnimation(targetButton, showAnimation);
                            showAnimationIsConfigured = true;
                        }

                        return;
                    }

                    SuppressRepositionForCurrentShow(targetButton, compositor);
                });
        }

        private static void SuppressRepositionForCurrentShow(Button button, Compositor compositor)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(button);
            visual.StopAnimation("Offset");
            visual.ImplicitAnimations?.Remove("Offset");

            EventHandler<object> layoutUpdatedHandler = null;
            layoutUpdatedHandler = (_, _) =>
            {
                button.LayoutUpdated -= layoutUpdatedHandler;
                ConfigureRepositionAnimation(button, compositor);
            };

            button.LayoutUpdated += layoutUpdatedHandler;
        }

        private static void ConfigureRepositionAnimation(Button button, Compositor compositor)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(button);
            CubicBezierEasingFunction easingFunction = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.22f, 1f),
                new Vector2(0.36f, 1f));

            Vector3KeyFrameAnimation repositionAnimation = compositor.CreateVector3KeyFrameAnimation();
            repositionAnimation.Target = "Offset";
            repositionAnimation.Duration = RepositionAnimationDuration;
            repositionAnimation.InsertExpressionKeyFrame(0f, "this.StartingValue");

            repositionAnimation.InsertExpressionKeyFrame(
                0.5f,
                "this.FinalValue.X > this.StartingValue.X ? this.StartingValue : this.FinalValue",
                easingFunction);
            repositionAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue", easingFunction);

            ImplicitAnimationCollection implicitAnimations =
                visual.ImplicitAnimations ?? compositor.CreateImplicitAnimationCollection();
            implicitAnimations["Offset"] = repositionAnimation;
            visual.ImplicitAnimations = implicitAnimations;
        }

        private static CompositionAnimationGroup CreateVisibilityAnimation(
            Compositor compositor,
            Vector3 fromTranslation,
            Vector3 toTranslation,
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
