using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CDPIUI.Controls.Navigation
{
    [ContentProperty(Name = nameof(Items))]
    public sealed partial class AnimatedSelectorBar : UserControl
    {
        public ObservableCollection<AnimatedSelectorBarItem> Items { get; } = [];

        public bool AreTransitionsEnabled { get; set; } = true;

        public int SelectedIndex => _selectedIndex;

        public AnimatedSelectorBarItem SelectedItem => _selectedItem;

        public event EventHandler<AnimatedSelectorBarSelectionChangedEventArgs> SelectionChanged;

        private ContentPresenter _activePresenter;
        private ContentPresenter _inactivePresenter;
        private Storyboard _transitionStoryboard;
        private int _selectedIndex = -1;
        private AnimatedSelectorBarItem _selectedItem;
        private bool _isBuildingSelector;

        public AnimatedSelectorBar()
        {
            InitializeComponent();
            _activePresenter = PrimaryPresenter;
            _inactivePresenter = SecondaryPresenter;
            Items.CollectionChanged += Items_CollectionChanged;
            Loaded += AnimatedSelectorBar_Loaded;
            Unloaded += AnimatedSelectorBar_Unloaded;

            AnimatedSelectorBarItem.EnabledChanged += AnimatedSelectorBarItem_EnabledChanged;
        }

        private void AnimatedSelectorBar_Unloaded(object sender, RoutedEventArgs e)
        {
            AnimatedSelectorBarItem.EnabledChanged -= AnimatedSelectorBarItem_EnabledChanged;
            Loaded -= AnimatedSelectorBar_Loaded;
            Unloaded -= AnimatedSelectorBar_Unloaded;
        }

        private void AnimatedSelectorBarItem_EnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            foreach (var item in Items)
            {
                if (d == item)
                {
                    RebuildSelector();
                    return;
                }
            }
        }

        private void AnimatedSelectorBar_Loaded(object sender, RoutedEventArgs e)
        {
            RebuildSelector();
        }

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsLoaded)
                RebuildSelector();
        }

        private void RebuildSelector()
        {
            var oldIndex = _selectedIndex;
            var oldItem = _selectedItem;

            _isBuildingSelector = true;
            NavigationSelector.Items.Clear();
            foreach (var item in Items)
            {
                NavigationSelector.Items.Add(new SelectorBarItem
                {
                    Text = item.Header,
                    Icon = item.Icon,
                    Tag = item,
                    IsEnabled = item.IsEnabled
                });
            }

            if (NavigationSelector.Items.Count > 0)
            {
                var index = _selectedIndex >= 0 && _selectedIndex < NavigationSelector.Items.Count
                    ? _selectedIndex
                    : 0;
                NavigationSelector.SelectedItem = NavigationSelector.Items[index];
                ShowImmediately(index);
            }
            else
            {
                _selectedIndex = -1;
                _selectedItem = null;
                _activePresenter.Content = null;
            }
            _isBuildingSelector = false;

            RaiseSelectionChanged(oldIndex, oldItem);
        }

        private void NavigationSelector_SelectionChanged(
            SelectorBar sender,
            SelectorBarSelectionChangedEventArgs args)
        {
            if (_isBuildingSelector || sender.SelectedItem?.Tag is not AnimatedSelectorBarItem item)
                return;

            var newIndex = Items.IndexOf(item);
            if (newIndex < 0 || newIndex == _selectedIndex)
                return;

            var oldIndex = _selectedIndex;
            var oldItem = _selectedItem;

            if (AreTransitionsEnabled)
                ShowAnimated(newIndex);
            else
                ShowImmediately(newIndex);

            RaiseSelectionChanged(oldIndex, oldItem);
        }

        public void SelectIndex(int index)
        {
            if (index < 0 || index >= Items.Count)
                return;

            if (!IsLoaded || NavigationSelector.Items.Count <= index)
            {
                var oldIndex = _selectedIndex;
                var oldItem = _selectedItem;
                _selectedIndex = index;
                _selectedItem = Items[index];
                RaiseSelectionChanged(oldIndex, oldItem);
                return;
            }

            var selectorItem = NavigationSelector.Items[index];
            if (!ReferenceEquals(NavigationSelector.SelectedItem, selectorItem))
                NavigationSelector.SelectedItem = selectorItem;
            else
                ShowImmediately(index);
        }

        private void ShowImmediately(int index)
        {
            FinishTransition();
            _selectedIndex = index;
            _selectedItem = Items[index];
            _activePresenter.Content = Items[index].Content;
            _activePresenter.Opacity = 1;
            _activePresenter.Visibility = Visibility.Visible;
            ((TranslateTransform)_activePresenter.RenderTransform).X = 0;
        }

        private void ShowAnimated(int newIndex)
        {
            FinishTransition();

            if (_selectedIndex < 0)
            {
                ShowImmediately(newIndex);
                return;
            }

            var direction = newIndex > _selectedIndex ? 1d : -1d;
            _selectedIndex = newIndex;
            _selectedItem = Items[newIndex];
            _inactivePresenter.Content = Items[newIndex].Content;
            _inactivePresenter.Visibility = Visibility.Visible;
            _inactivePresenter.Opacity = 0;
            ((TranslateTransform)_inactivePresenter.RenderTransform).X = 28 * direction;

            Canvas.SetZIndex(_inactivePresenter, 1);
            Canvas.SetZIndex(_activePresenter, 0);

            _transitionStoryboard = new Storyboard();
            AddAnimation(_activePresenter, nameof(UIElement.Opacity), 1, 0, 140);
            AddAnimation(
                (TranslateTransform)_activePresenter.RenderTransform,
                nameof(TranslateTransform.X),
                0,
                -16 * direction,
                140);
            AddAnimation(_inactivePresenter, nameof(UIElement.Opacity), 0, 1, 190);
            AddAnimation(
                (TranslateTransform)_inactivePresenter.RenderTransform,
                nameof(TranslateTransform.X),
                28 * direction,
                0,
                190);
            _transitionStoryboard.Completed += TransitionStoryboard_Completed;
            _transitionStoryboard.Begin();
        }

        private void AddAnimation(
            DependencyObject target,
            string propertyPath,
            double from,
            double to,
            int durationMilliseconds)
        {
            DoubleAnimation animation = new()
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, propertyPath);
            _transitionStoryboard.Children.Add(animation);
        }

        private void TransitionStoryboard_Completed(object sender, object e)
        {
            FinishTransition();
        }

        private void FinishTransition()
        {
            if (_transitionStoryboard == null)
                return;

            _transitionStoryboard.Completed -= TransitionStoryboard_Completed;
            _transitionStoryboard.Stop();
            _transitionStoryboard = null;

            _activePresenter.Content = null;
            _activePresenter.Visibility = Visibility.Collapsed;
            _activePresenter.Opacity = 0;
            ((TranslateTransform)_activePresenter.RenderTransform).X = 0;

            (_activePresenter, _inactivePresenter) = (_inactivePresenter, _activePresenter);
            _activePresenter.Visibility = Visibility.Visible;
            _activePresenter.Opacity = 1;
            ((TranslateTransform)_activePresenter.RenderTransform).X = 0;
        }

        private void RaiseSelectionChanged(
            int oldIndex,
            AnimatedSelectorBarItem oldItem)
        {
            if (oldIndex == _selectedIndex && ReferenceEquals(oldItem, _selectedItem))
                return;

            SelectionChanged?.Invoke(
                this,
                new AnimatedSelectorBarSelectionChangedEventArgs(
                    oldIndex,
                    _selectedIndex,
                    oldItem,
                    _selectedItem));
        }
    }

    public sealed class AnimatedSelectorBarSelectionChangedEventArgs : EventArgs
    {
        public AnimatedSelectorBarSelectionChangedEventArgs(
            int oldIndex,
            int newIndex,
            AnimatedSelectorBarItem oldItem,
            AnimatedSelectorBarItem newItem)
        {
            OldIndex = oldIndex;
            NewIndex = newIndex;
            OldItem = oldItem;
            NewItem = newItem;
        }

        public int OldIndex { get; }
        public int NewIndex { get; }
        public AnimatedSelectorBarItem OldItem { get; }
        public AnimatedSelectorBarItem NewItem { get; }
    }

    [ContentProperty(Name = nameof(Content))]
    public sealed class AnimatedSelectorBarItem : DependencyObject
    {
        public static event PropertyChangedCallback EnabledChanged;
        public bool IsEnabled
        {
            get => (bool)GetValue(IsEnabledProperty);
            set => SetValue(IsEnabledProperty, value);
        }
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.Register(
            nameof(IsEnabled),
            typeof(bool),
            typeof(AnimatedSelectorBarItem),
            new PropertyMetadata(true, EnabledChanged));

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            nameof(Header),
            typeof(string),
            typeof(AnimatedSelectorBarItem),
            new PropertyMetadata(string.Empty));

        public IconElement Icon
        {
            get => (IconElement)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon),
            typeof(IconElement),
            typeof(AnimatedSelectorBarItem),
            new PropertyMetadata(null));

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(AnimatedSelectorBarItem),
            new PropertyMetadata(null));
    }
}
