using CDPI_UI.Default;
using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using WinUI3Localizer;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class StoreWindow : TemplateWindow
{
    public static StoreWindow Instance { get; private set; }

    private ILocalizer localizer = Localizer.Get();
    public StoreWindow()
    {
        this.InitializeComponent();
        this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("StoreWindowsTitle"));
        TitleBar = WindowMoveAera;
        TitleIcon = TitleImageRectagle;

        Instance = this;

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage));

        SetTitleBar(WindowMoveAera);
        NavView.SelectionChanged += NavView_SelectionChanged;

        StoreHelper.Instance.QueueUpdated += StoreHelper_QueueUpdated;
        StoreHelper.Instance.ItemInstallingErrorHappens += Instance_ItemInstallingErrorHappens;

        this.Closed += StoreWindow_Closed;

        SetDownloadsFontIcon();
    }

    private void SetDownloadsFontIcon()
    {
        DownloadsFontIcon.Glyph = Utils.IsOsSupportedNewGlyph() ? "\uEBD3" : "\uE896";
    }

    private void StoreHelper_QueueUpdated()
    {
        if (StoreHelper.Instance.GetQueue().Count > 0 || !string.IsNullOrEmpty(StoreHelper.Instance.GetCurrentQueueOperationId())) 
        {
            NowDownloadingInfoBadge.Opacity = 1;
        }
        else
        {
            NowDownloadingInfoBadge.Opacity = 0;
        }
    }

    private void Instance_ItemInstallingErrorHappens(Tuple<string, string> obj)
    {
        /*
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = $"{obj.Item2}",
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot,
        };
        _ = dialog.ShowAsync();
        */
    }

    private void StoreWindow_Closed(object sender, WindowEventArgs args)
    {
        Instance = null;
        StoreHelper.Instance.ItemInstallingErrorHappens -= Instance_ItemInstallingErrorHappens;
        StoreHelper.Instance.QueueUpdated -= StoreHelper_QueueUpdated;
        ((App)Application.Current).OpenWindows.Remove(this);
    }

    ~StoreWindow()
    {
        StoreHelper.Instance.ItemInstallingErrorHappens -= Instance_ItemInstallingErrorHappens;
        StoreHelper.Instance.QueueUpdated -= StoreHelper_QueueUpdated;
        ((App)Application.Current).OpenWindows.Remove(this);
    }

    private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness()
        {
            Left = AppTitleBar.Margin.Left,
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }
    private double NavViewCompactModeThresholdWidth { get { return NavView.CompactModeThresholdWidth; } }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigated += On_Navigated;

        NavView.SelectedItem = NavView.MenuItems[0];
        if (ContentFrame.SourcePageType == null)
            NavView_Navigate(typeof(HomePage), new EntranceNavigationTransitionInfo());
    }

    private void NavView_ItemInvoked(NavigationView sender,
                                     NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked == true)
        {
            // pass
        }
        else if (args.InvokedItemContainer != null)
        {
            Type navPageType = Type.GetType(args.InvokedItemContainer.Tag.ToString());
            NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender,
                                          NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected == true)
        {
            
        }
        else if (args.SelectedItemContainer != null)
        {
            Type navPageType = Type.GetType(args.SelectedItemContainer.Tag.ToString());
            NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
        }
    }

    private void NavView_Navigate(
        Type navPageType,
        NavigationTransitionInfo transitionInfo)
    {
        Type preNavPageType = ContentFrame.CurrentSourcePageType;

        if (navPageType is not null && !Type.Equals(preNavPageType, navPageType))
        {
            ContentFrame.Navigate(navPageType, null, transitionInfo);
        }
    }

    private void NavView_BackRequested(NavigationView sender,
                                       NavigationViewBackRequestedEventArgs args)
    {
        TryGoBack();
    }

    private bool TryGoBack()
    {
        if (!ContentFrame.CanGoBack)
            return false;

        ContentFrame.GoBack();
        return true;
    }

    private void On_Navigated(object sender, NavigationEventArgs e)
    {
        NavView.IsBackEnabled = ContentFrame.CanGoBack;

        if (ContentFrame.SourcePageType != null)
        {
            Debug.WriteLine(ContentFrame.SourcePageType.FullName.ToString());
            try
            {
                var item = NavView.MenuItems
                            .OfType<NavigationViewItem>()
                            .FirstOrDefault(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));
                if (item == null)
                    item = NavView.FooterMenuItems
                            .OfType<NavigationViewItem>()
                            .FirstOrDefault(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));

                if (item == null) return;

                BackButton.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex) { Debug.WriteLine($"==>{ex}"); }
        }
    }

    public Frame GetCurrentFrame()
    {
        return ContentFrame;
    }

    public void NavigateSubPage(Type page, object parameter, NavigationTransitionInfo effect)
    {
        try
        {
            ContentFrame.Navigate(page, parameter, effect);
            BackButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

    }

    private void BackButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(this.SearchAnimatedIcon, "PointerOver");
    }

    private void BackButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimatedIcon.SetState(this.SearchAnimatedIcon, "Normal");
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }
}
