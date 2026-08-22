using CDPIUI.Controls.Default;
using CDPIUI.Controls.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.CompilerServices;

namespace CDPIUI.Controls.MenuManagement
{
    public static class AnimatedSelectorBarMenu
    {
        private static readonly ConditionalWeakTable<AnimatedSelectorBar, Subscription> Subscriptions = new();

        public static string GetScopeId(DependencyObject element) =>
            (string)element.GetValue(ScopeIdProperty);

        public static void SetScopeId(DependencyObject element, string value) =>
            element.SetValue(ScopeIdProperty, value);

        public static readonly DependencyProperty ScopeIdProperty = DependencyProperty.RegisterAttached(
            "ScopeId",
            typeof(string),
            typeof(AnimatedSelectorBarMenu),
            new PropertyMetadata(null, OnScopeIdChanged));

        private static void OnScopeIdChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is not AnimatedSelectorBar selectorBar)
                return;

            if (Subscriptions.TryGetValue(selectorBar, out var oldSubscription))
            {
                oldSubscription.Dispose();
                Subscriptions.Remove(selectorBar);
            }

            if (args.NewValue is not string scopeId || string.IsNullOrWhiteSpace(scopeId))
                return;

            Subscriptions.Add(selectorBar, new Subscription(selectorBar, scopeId.Trim()));
        }

        private sealed class Subscription : IDisposable
        {
            private readonly AnimatedSelectorBar _selectorBar;
            private readonly string _scopeId;
            private TemplatePage _page;
            private bool _isRetryScheduled;
            private bool _isDisposed;

            public Subscription(AnimatedSelectorBar selectorBar, string scopeId)
            {
                _selectorBar = selectorBar;
                _scopeId = scopeId;

                _selectorBar.Loaded += SelectorBar_Loaded;
                _selectorBar.Unloaded += SelectorBar_Unloaded;
                _selectorBar.SelectionChanged += SelectorBar_SelectionChanged;

                if (_selectorBar.IsLoaded)
                    ApplySelection(_selectorBar.SelectedItem);
            }

            public void Dispose()
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                ClearScope();
                _selectorBar.Loaded -= SelectorBar_Loaded;
                _selectorBar.Unloaded -= SelectorBar_Unloaded;
                _selectorBar.SelectionChanged -= SelectorBar_SelectionChanged;
                _page = null;
            }

            private void SelectorBar_Loaded(object sender, RoutedEventArgs args)
            {
                _page = FindPage(_selectorBar);
                ApplySelection(_selectorBar.SelectedItem);
            }

            private void SelectorBar_Unloaded(object sender, RoutedEventArgs args)
            {
                ClearScope();
                _page = null;
            }

            private void SelectorBar_SelectionChanged(
                object sender,
                AnimatedSelectorBarSelectionChangedEventArgs args)
            {
                ApplySelection(args.NewItem);
            }

            private void ApplySelection(AnimatedSelectorBarItem item)
            {
                if (_isDisposed)
                    return;

                _page ??= FindPage(_selectorBar);
                if (_page == null)
                    return;

                if (_page.MenuBarSession == null)
                {
                    ScheduleRetry();
                    return;
                }

                _page.MenuBarSession.ReplaceScope(
                    _scopeId,
                    item == null ? null : MenuBarState.GetTemplate(item));
            }

            private void ClearScope()
            {
                _page?.MenuBarSession?.ClearScope(_scopeId);
            }

            private void ScheduleRetry()
            {
                if (_isRetryScheduled || !_selectorBar.IsLoaded)
                    return;

                _isRetryScheduled = true;
                _selectorBar.DispatcherQueue.TryEnqueue(() =>
                {
                    _isRetryScheduled = false;
                    if (!_isDisposed && _selectorBar.IsLoaded)
                        ApplySelection(_selectorBar.SelectedItem);
                });
            }

            private static TemplatePage FindPage(DependencyObject source)
            {
                DependencyObject current = source;
                while (current != null)
                {
                    if (current is TemplatePage page)
                        return page;

                    current = VisualTreeHelper.GetParent(current);
                }

                return null;
            }
        }
    }
}
