using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using WinUI3Localizer;

namespace CDPIUI.Controls.Navigation;

[ContentProperty(Name = nameof(Items))]
public sealed partial class AnimatedHorizontalContentViewer : UserControl
{
    public ObservableCollection<AnimatedHorizontalContentItem> Items { get; } = [];

    private ContentPresenter _activePresenter;
    private ContentPresenter _inactivePresenter;
    private Storyboard _transitionStoryboard;
    private int _selectedIndex = -1;

    private ILocalizer localizer = Localizer.Get();

    public AnimatedHorizontalContentItem SelectedItem
    {
        get => _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null;
    }

    public AnimatedHorizontalContentViewer()
    {
        InitializeComponent();
        _activePresenter = PrimaryPresenter;
        _inactivePresenter = SecondaryPresenter;

        localizer.LanguageChanged += Localizer_LanguageChanged;

        this.Loaded += AnimatedHorizontalContentViewer_Loaded;
    }

    private void Localizer_LanguageChanged(object sender, LanguageChangedEventArgs e)
    {
        UpdateCurrentText(_selectedIndex);
    }

    public string CurrentHeader
    {
        get => (string)GetValue(CurrentHeaderProperty);
        private set => SetValue(CurrentHeaderProperty, value);
    }

    public static readonly DependencyProperty CurrentHeaderProperty = DependencyProperty.Register(
        nameof(CurrentHeader),
        typeof(string),
        typeof(AnimatedHorizontalContentViewer),
        new PropertyMetadata(string.Empty));

    public string CurrentDescription
    {
        get => (string)GetValue(CurrentDescriptionProperty);
        private set => SetValue(CurrentDescriptionProperty, value);
    }

    public static readonly DependencyProperty CurrentDescriptionProperty = DependencyProperty.Register(
        nameof(CurrentDescription),
        typeof(string),
        typeof(AnimatedHorizontalContentViewer),
        new PropertyMetadata(string.Empty));

    private void AnimatedHorizontalContentViewer_Loaded(object sender, RoutedEventArgs e)
    {
        GoTo(0);
        this.Loaded -= AnimatedHorizontalContentViewer_Loaded;
    }

    public void GoTo(AnimatedHorizontalContentItem item)
    {
        if (!Items.Contains(item))
            return;
        int newIndex = Items.IndexOf(item);
        GoTo(newIndex);
    }


    private void UpdateCurrentText(int index)
    {
        UpdateTextStoryboard.Begin();
        CurrentHeader = Items[index].Header;
        CurrentDescription = Items[index].Description;
    }

    public void GoTo(int index)
    {
        if (index < 0 || index >= Items.Count)
            return;

        UpdateCurrentText(index);

        if (index == _selectedIndex)
            return;

        ShowAnimated(index);
    }

    public void GoNext()
    {
        if (Items.Count == 0)
            return;
        var newIndex = (_selectedIndex + 1) % Items.Count;
        GoTo(newIndex);
    }
    public void GoPrevious()
    {
        if (Items.Count == 0)
            return;
        var newIndex = (_selectedIndex - 1 + Items.Count) % Items.Count;
        GoTo(newIndex);
    }

    private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsLoaded)
            RebuildSelector();
    }

    private void RebuildSelector()
    {
        if (Items.Count > 0)
        {
            var index = _selectedIndex >= 0 && _selectedIndex < Items.Count
                ? _selectedIndex
                : 0;
            ShowImmediately(index);
        }
        else
        {
            _selectedIndex = -1;
            _activePresenter.Content = null;
        }
    }

    private void ShowImmediately(int index)
    {
        CurrentHeader = Items[index].Header;
        CurrentDescription = Items[index].Description;

        FinishTransition();
        _selectedIndex = index;
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
}

[ContentProperty(Name = nameof(Content))]
public sealed class AnimatedHorizontalContentItem : DependencyObject
{
    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(string),
        typeof(AnimatedHorizontalContentItem),
        new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(AnimatedHorizontalContentItem),
        new PropertyMetadata(string.Empty));

    public object Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content),
        typeof(object),
        typeof(AnimatedHorizontalContentItem),
        new PropertyMetadata(null));
}
