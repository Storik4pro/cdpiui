using CDPIUI.Commands;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
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
        private static readonly TimeSpan ButtonSequenceGap = TimeSpan.FromMilliseconds(10);

        private static ILocalizer localizer = Localizer.Get();
        private readonly bool animationsEnabled;
        private readonly HashSet<Button> configuredButtons = [];
        private readonly Dictionary<Button, long> visibilityCallbackTokens = [];
        private readonly Dictionary<Button, Visibility> requestedVisibilityStates = [];
        private CancellationTokenSource visibilitySequenceCancellation;
        private Frame navigationFrame;
        private Page navigationPage;

        public ObservableCollection<Button> Items { get; } = [];

        public UtilityButtonControls()
        {
            InitializeComponent();

            animationsEnabled = new UISettings().AnimationsEnabled;
            Items.CollectionChanged += Items_CollectionChanged;
            Loaded += UtilityButtonControls_Loaded;
            Unloaded += UtilityButtonControls_Unloaded;
        }

        private void UtilityButtonControls_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= UtilityButtonControls_Loaded;
            Unloaded -= UtilityButtonControls_Unloaded;

            if (!animationsEnabled)
                return;

            DetachFromNavigationFrame();
            SuspendVisibilityAnimations();
        }

        private void UtilityButtonControls_Loaded(object sender, RoutedEventArgs e)
        {
            if (!animationsEnabled)
                return;

            AttachToNavigationFrame();

            foreach (Button button in Items)
                PrepareVisibilityAnimations(button);
        }

        private void AttachToNavigationFrame()
        {
            DetachFromNavigationFrame();

            DependencyObject current = this;
            while (current is not null)
            {
                if (current is Page page && page.Frame is Frame frame)
                {
                    navigationPage = page;
                    navigationFrame = frame;
                    navigationFrame.Navigating += NavigationFrame_Navigating;
                    return;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }

        private void DetachFromNavigationFrame()
        {
            if (navigationFrame is null)
                return;

            navigationFrame.Navigating -= NavigationFrame_Navigating;
            navigationFrame = null;
            navigationPage = null;
        }

        private void NavigationFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            Frame currentFrame = navigationFrame;
            Page currentPage = navigationPage;
            DetachFromNavigationFrame();
            SuspendVisibilityAnimations();

            DispatcherQueue.TryEnqueue(() =>
            {
                if (!IsLoaded || !ReferenceEquals(currentFrame?.Content, currentPage))
                    return;

                AttachToNavigationFrame();

                foreach (Button button in Items)
                    PrepareVisibilityAnimations(button);

                if (requestedVisibilityStates.Count == 0)
                    return;

                (Button Button, Visibility Visibility)[] states =
                    new (Button, Visibility)[requestedVisibilityStates.Count];
                int stateIndex = 0;
                foreach (KeyValuePair<Button, Visibility> state in requestedVisibilityStates)
                    states[stateIndex++] = (state.Key, state.Value);

                SetButtonVisibilities(states);
            });
        }

        private void SuspendVisibilityAnimations()
        {
            visibilitySequenceCancellation?.Cancel();

            foreach (Button button in configuredButtons)
            {
                if (visibilityCallbackTokens.TryGetValue(button, out long callbackToken))
                {
                    button.UnregisterPropertyChangedCallback(
                        UIElement.VisibilityProperty,
                        callbackToken);
                }

                ElementCompositionPreview.SetImplicitShowAnimation(button, null);
                ElementCompositionPreview.SetImplicitHideAnimation(button, null);

                Visual visual = ElementCompositionPreview.GetElementVisual(button);
                visual.StopAnimation("Translation");
                visual.StopAnimation("Opacity");
                visual.Opacity = 1f;
                visual.StopAnimation("Offset");
                visual.ImplicitAnimations?.Remove("Offset");
            }

            visibilityCallbackTokens.Clear();
            configuredButtons.Clear();
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

        private void ConfigureVisibilityAnimations(Button button)
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
                fromTranslation: Vector3.Zero,
                toTranslation: new Vector3(0, VisibilityAnimationOffset, 0),
                fromOpacity: 1f,
                toOpacity: 0f,
                duration: HideAnimationDuration,
                firstControlPoint: new Vector2(0.55f, 0f),
                secondControlPoint: new Vector2(1f, 0.45f));

            ConfigureRepositionAnimation(button, compositor);
            ElementCompositionPreview.SetImplicitHideAnimation(button, hideAnimation);

            bool showAnimationIsConfigured = button.Visibility == Visibility.Collapsed;
            if (showAnimationIsConfigured)
                ElementCompositionPreview.SetImplicitShowAnimation(button, showAnimation);

            long callbackToken = button.RegisterPropertyChangedCallback(
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
            visibilityCallbackTokens[button] = callbackToken;
        }

        public void SetButtonVisibilities(params (Button Button, Visibility Visibility)[] states)
        {
            Dictionary<Button, Visibility> requestedStates = [];
            foreach ((Button button, Visibility visibility) in states)
            {
                if (button is not null && Items.Contains(button))
                    requestedStates[button] = visibility;
            }

            if (visibilitySequenceCancellation is { IsCancellationRequested: false } &&
                AreSameStates(requestedStates, requestedVisibilityStates))
            {
                return;
            }

            visibilitySequenceCancellation?.Cancel();
            requestedVisibilityStates.Clear();
            foreach (KeyValuePair<Button, Visibility> state in requestedStates)
                requestedVisibilityStates[state.Key] = state.Value;

            if (!animationsEnabled || !IsLoaded)
            {
                foreach (Button button in Items)
                {
                    if (requestedStates.TryGetValue(button, out Visibility visibility))
                        button.Visibility = visibility;
                }

                return;
            }

            List<(Button Button, Visibility Visibility)> hiddenButtons = [];
            List<(Button Button, Visibility Visibility)> shownButtons = [];

            foreach (Button button in Items)
            {
                if (!requestedStates.TryGetValue(button, out Visibility visibility) ||
                    button.Visibility == visibility)
                {
                    continue;
                }

                if (visibility == Visibility.Collapsed)
                    hiddenButtons.Add((button, visibility));
                else
                    shownButtons.Add((button, visibility));
            }

            if (hiddenButtons.Count == 0 && shownButtons.Count == 0)
                return;

            CancellationTokenSource cancellation = new();
            visibilitySequenceCancellation = cancellation;
            _ = RunVisibilitySequenceAsync(hiddenButtons, shownButtons, cancellation);
        }

        private static bool AreSameStates(
            IReadOnlyDictionary<Button, Visibility> first,
            IReadOnlyDictionary<Button, Visibility> second)
        {
            if (first.Count != second.Count)
                return false;

            foreach (KeyValuePair<Button, Visibility> state in first)
            {
                if (!second.TryGetValue(state.Key, out Visibility visibility) ||
                    visibility != state.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task RunVisibilitySequenceAsync(
            IReadOnlyList<(Button Button, Visibility Visibility)> hiddenButtons,
            IReadOnlyList<(Button Button, Visibility Visibility)> shownButtons,
            CancellationTokenSource cancellation)
        {
            try
            {
                int transitionCount = hiddenButtons.Count + shownButtons.Count;
                int transitionIndex = 0;

                foreach ((Button button, Visibility visibility) in hiddenButtons)
                {
                    bool repositionsButtonsToTheLeft = HasVisibleButtonToTheLeft(button);
                    button.Visibility = visibility;
                    transitionIndex++;

                    if (transitionIndex < transitionCount)
                    {
                        TimeSpan duration = repositionsButtonsToTheLeft
                            ? RepositionAnimationDuration
                            : HideAnimationDuration;
                        await Task.Delay(duration + ButtonSequenceGap, cancellation.Token);
                    }
                }

                foreach ((Button button, Visibility visibility) in shownButtons)
                {
                    bool repositionsButtonsToTheLeft = HasVisibleButtonToTheLeft(button);
                    button.Visibility = visibility;
                    transitionIndex++;

                    if (transitionIndex < transitionCount)
                    {
                        TimeSpan duration = repositionsButtonsToTheLeft
                            ? Max(ShowAnimationDuration, RepositionAnimationDuration / 2)
                            : ShowAnimationDuration;
                        await Task.Delay(duration + ButtonSequenceGap, cancellation.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                
            }
            finally
            {
                if (ReferenceEquals(visibilitySequenceCancellation, cancellation))
                    visibilitySequenceCancellation = null;

                cancellation.Dispose();
            }
        }

        private bool HasVisibleButtonToTheLeft(Button targetButton)
        {
            foreach (Button button in Items)
            {
                if (ReferenceEquals(button, targetButton))
                    return false;

                if (button.Visibility == Visibility.Visible)
                    return true;
            }

            return false;
        }

        private static TimeSpan Max(TimeSpan first, TimeSpan second)
        {
            return first >= second ? first : second;
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

            ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.Target = "Opacity";
            opacityAnimation.Duration = duration;
            opacityAnimation.InsertKeyFrame(0f, fromOpacity);
            opacityAnimation.InsertKeyFrame(1f, toOpacity, easingFunction);

            CompositionAnimationGroup animationGroup = compositor.CreateAnimationGroup();
            animationGroup.Add(translationAnimation);
            animationGroup.Add(opacityAnimation);

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

        public bool IsIndeterminate 
        { 
            get => LoadingProgressBar.IsIndeterminate; 
            set => LoadingProgressBar.IsIndeterminate = value;
        }

        public double LoadingValue
        {
            get => LoadingProgressBar.Value;
            set => LoadingProgressBar.Value = value;
        }

        public double LoadingMaximumValue
        {
            get => LoadingProgressBar.Maximum;
            set => LoadingProgressBar.Maximum = value;
        }

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
