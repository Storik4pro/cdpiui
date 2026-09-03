using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls.Primitives;

namespace CDPIUI.Controls.Universal;

public interface IStatusNotificationSource
{
    event EventHandler<StatusNotificationRequestedEventArgs> StatusNotificationRequested;
}

public sealed class StatusNotificationRequestedEventArgs : EventArgs
{
    public StatusNotificationRequestedEventArgs(
        InfoBarSeverity severity,
        string title,
        string message)
    {
        Severity = severity;
        Title = title ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public InfoBarSeverity Severity { get; }
    public string Title { get; }
    public string Message { get; }
}

public sealed class StatusNotificationItem
{
    public StatusNotificationItem(
        InfoBarSeverity severity,
        string title,
        string message,
        Brush severityBrush)
    {
        Severity = severity;
        Title = title;
        Message = message;
        SeverityBrush = severityBrush;
        CreatedAt = DateTimeOffset.Now;
    }

    public InfoBarSeverity Severity { get; }
    public string Title { get; }
    public string Message { get; }
    public DateTimeOffset CreatedAt { get; }
    public string TimeText => CreatedAt.ToString("t");
    public Brush SeverityBrush { get; }
    public string IconGlyph => Severity switch
    {
        InfoBarSeverity.Error => "\uE783",
        InfoBarSeverity.Warning => "\uE7BA",
        InfoBarSeverity.Success => "\uE73E",
        _ => "\uE946",
    };
}

public enum PlacementModes
{
    Top,
    Bottom
}

public sealed partial class StatusNotificationControl : UserControl
{
    private const int MaximumNotifications = 50;
    private readonly DispatcherQueueTimer toastTimer;
    private Storyboard showStoryboard;
    private Storyboard hideStoryboard;
    private int unreadCount;

    public static readonly DependencyProperty PlacementModeProperty =
        DependencyProperty.Register(
            nameof(PlacementMode),
            typeof(PlacementModes),
            typeof(StatusNotificationControl),
            new PropertyMetadata(PlacementModes.Top));

    public static readonly DependencyProperty ButtonHeightProperty =
        DependencyProperty.Register(
            nameof(ButtonHeight),
            typeof(double),
            typeof(StatusNotificationControl),
            new PropertyMetadata((double)28));

    public StatusNotificationControl()
    {
        InitializeComponent();

        this.DataContext = this;

        toastTimer = DispatcherQueue.CreateTimer();
        toastTimer.Interval = TimeSpan.FromSeconds(5);
        toastTimer.IsRepeating = false;
        toastTimer.Tick += ToastTimer_Tick;
        UpdateNotificationState();
    }

    public PlacementModes PlacementMode
    {
        get => (PlacementModes)GetValue(PlacementModeProperty);
        set
        {
            SetValue(PlacementModeProperty, value);
            RecalcPlacement();
        }
    }

    public double ButtonHeight
    {
        get => (double)GetValue(ButtonHeightProperty);
        set
        {
            SetValue(ButtonHeightProperty, value);
            RecalcPlacement();
        }
    }

    private void RecalcPlacement()
    {
        if (PlacementMode == PlacementModes.Top)
        {
            NotificationFlyout.Placement = FlyoutPlacementMode.TopEdgeAlignedRight;
            ToastCard.Margin = new(0, 0, 10, ButtonHeight + 8);
            NotificationButton.VerticalAlignment = VerticalAlignment.Bottom;
        }
        else if (PlacementMode == PlacementModes.Bottom)
        {
            NotificationFlyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            ToastCard.Margin = new(0, ButtonHeight + 8, 10, 0);
            NotificationButton.VerticalAlignment = VerticalAlignment.Top;
        }
    }

    public ObservableCollection<StatusNotificationItem> Notifications { get; } = [];

    public void Show(InfoBarSeverity severity, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        StatusNotificationItem item = new(
            severity,
            title ?? string.Empty,
            message ?? string.Empty,
            ResolveSeverityBrush(severity));
        Notifications.Insert(0, item);
        while (Notifications.Count > MaximumNotifications)
        {
            Notifications.RemoveAt(Notifications.Count - 1);
        }
        unreadCount = Math.Min(999, unreadCount + 1);
        UpdateNotificationState();
        ShowToast(item);
    }

    public void Clear()
    {
        Notifications.Clear();
        unreadCount = 0;
        HideToast();
        UpdateNotificationState();
    }

    private void ShowToast(StatusNotificationItem item)
    {
        toastTimer.Stop();
        StopStoryboard(ref showStoryboard);
        StopStoryboard(ref hideStoryboard);

        ToastTitleTextBlock.Text = item.Title;
        ToastMessageTextBlock.Text = item.Message;
        ToastIcon.Glyph = item.IconGlyph;
        ToastIcon.Foreground = item.SeverityBrush;
        ToastCard.Visibility = Visibility.Visible;
        ToastCard.Opacity = 0;
        ToastTranslateTransform.X = 32;

        showStoryboard = new Storyboard();
        AddAnimation(showStoryboard, ToastCard, nameof(UIElement.Opacity), 0, 1, 160);
        AddAnimation(showStoryboard, ToastTranslateTransform, nameof(TranslateTransform.X), 32, 0, 190);
        showStoryboard.Begin();
        toastTimer.Start();
    }

    private void HideToast()
    {
        toastTimer.Stop();
        StopStoryboard(ref showStoryboard);
        StopStoryboard(ref hideStoryboard);
        if (ToastCard.Visibility != Visibility.Visible)
        {
            return;
        }

        hideStoryboard = new Storyboard();
        AddAnimation(hideStoryboard, ToastCard, nameof(UIElement.Opacity), ToastCard.Opacity, 0, 130);
        AddAnimation(hideStoryboard, ToastTranslateTransform, nameof(TranslateTransform.X), ToastTranslateTransform.X, 36, 160);
        hideStoryboard.Completed += HideStoryboard_Completed;
        hideStoryboard.Begin();
    }

    private static void AddAnimation(
        Storyboard storyboard,
        DependencyObject target,
        string property,
        double from,
        double to,
        int durationMilliseconds)
    {
        DoubleAnimation animation = new()
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }

    private void HideStoryboard_Completed(object sender, object e)
    {
        if (hideStoryboard != null)
        {
            hideStoryboard.Completed -= HideStoryboard_Completed;
        }
        StopStoryboard(ref hideStoryboard);
        ToastCard.Visibility = Visibility.Collapsed;
        ToastCard.Opacity = 0;
        ToastTranslateTransform.X = 32;
    }

    private static void StopStoryboard(ref Storyboard storyboard)
    {
        storyboard?.Stop();
        storyboard = null;
    }

    private void ToastTimer_Tick(DispatcherQueueTimer sender, object args) => HideToast();

    private void DismissToastButton_Click(object sender, RoutedEventArgs e) => HideToast();

    private void NotificationFlyout_Opened(object sender, object e)
    {
        unreadCount = 0;
        HideToast();
        UpdateNotificationState();
    }

    private void ClearNotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        Clear();
        NotificationFlyout.Hide();
    }

    private void UpdateNotificationState()
    {
        UnreadBadge.Visibility = unreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        UnreadCountTextBlock.Text = unreadCount > 99 ? "99+" : unreadCount.ToString();
        EmptyNotificationsTextBlock.Visibility = Notifications.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        NotificationsListView.Visibility = Notifications.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static Brush ResolveSeverityBrush(InfoBarSeverity severity)
    {
        string key = severity switch
        {
            InfoBarSeverity.Error => "SystemFillColorCriticalBrush",
            InfoBarSeverity.Warning => "SystemFillColorCautionBrush",
            InfoBarSeverity.Success => "SystemFillColorSuccessBrush",
            _ => "AccentFillColorDefaultBrush",
        };
        return Application.Current.Resources.TryGetValue(key, out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        NotificationFlyout.Hide();
    }
}
