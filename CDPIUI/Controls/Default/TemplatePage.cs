using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CDPIUI.Controls.MenuManagement;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Specialized;
using Windows.Foundation.Metadata;

namespace CDPIUI.Controls.Default
{
    public partial class TemplatePage : Page
    {
        protected const string ForwardConnectedAnimation = "ForwardConnectedAnimation";
        protected const string BackwardConnectedAnimation = "BackwardConnectedAnimation";

        protected bool IsBackwardAnimationToPageAvailable = false;
        protected bool IsForwardAnimationToPageAvailable = false;

        protected FrameworkElement ElementToAnimateBackwardConnectedAnimation = null;
        protected FrameworkElement ElementToAnimateForwardConnectedAnimation = null;

        public NameValueCollection Parameter { get; set; } = null;

        protected bool IsAnimated = false;

        public DataTemplate MenuBarTemplate
        {
            get => (DataTemplate)GetValue(MenuBarTemplateProperty);
            set => SetValue(MenuBarTemplateProperty, value);
        }

        public static readonly DependencyProperty MenuBarTemplateProperty = DependencyProperty.Register(
            nameof(MenuBarTemplate),
            typeof(DataTemplate),
            typeof(TemplatePage),
            new PropertyMetadata(null, OnMenuBarTemplateChanged));

        public MenuBarSession MenuBarSession { get; internal set; }

        public TemplatePage()
        {
            this.Loaded += TemplatePage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is NameValueCollection collection)
            {
                Parameter = collection;
            }

            if (IsForwardAnimationToPageAvailable && ElementToAnimateForwardConnectedAnimation != null)
            {
                var fdAnim = ConnectedAnimationService.GetForCurrentView().GetAnimation(ForwardConnectedAnimation);
                IsAnimated = fdAnim?.TryStart(ElementToAnimateForwardConnectedAnimation) ?? false;
            }
            if (IsBackwardAnimationToPageAvailable && ElementToAnimateBackwardConnectedAnimation != null)
            {
                var bkAnim = ConnectedAnimationService.GetForCurrentView().GetAnimation(BackwardConnectedAnimation);
                IsAnimated = bkAnim?.TryStart(ElementToAnimateBackwardConnectedAnimation) ?? false;
            }

        }

        protected bool FocusElement(string elementName)
        {
            if (string.IsNullOrEmpty(elementName)) return false;
            if (FindName(elementName) is not FrameworkElement element)
                return false;

            if (element.Visibility != Visibility.Visible)
                return false;

            return element.Focus(FocusState.Programmatic);
        }

        protected void PrepareToConnectedForwardAnimate(
            UIElement elementToAnimate, 
            ConnectedAnimationConfiguration configuration = null)
        {
            PrepareToAnimate(elementToAnimate, ForwardConnectedAnimation, configuration);
        }

        protected void PrepareToConnectedBackwardAnimate(
            UIElement elementToAnimate, 
            ConnectedAnimationConfiguration configuration = null)
        {
            PrepareToAnimate(elementToAnimate, BackwardConnectedAnimation, configuration);
        }

        private static void PrepareToAnimate(
            UIElement elementToAnimate, 
            string animateName, 
            ConnectedAnimationConfiguration configuration = null)
        {
            configuration ??= new BasicConnectedAnimationConfiguration();
            var animq = ConnectedAnimationService.GetForCurrentView()
                   .PrepareToAnimate(animateName, elementToAnimate);

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
            {
                animq.Configuration = configuration;
            }
        }

        private void TemplatePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (Parameter != null)
            {
                FocusElement(Parameter.Get("setFocus"));
            }
        }

        private static void OnMenuBarTemplateChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is TemplatePage page)
                page.MenuBarSession?.Refresh();
        }
    }
}
