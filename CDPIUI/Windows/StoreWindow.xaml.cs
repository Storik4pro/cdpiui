using CDPIUI.Default;
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
using CDPIUI.Core.Store;

using CDPIUI.Shared;
using CDPIUI.Shared.PrettyErrorConvertionService;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class StoreWindow : TemplateWindow
{
    public static StoreWindow Instance { get; private set; }
    private bool _isSynchronizingNavigationSelection;

    private ILocalizer localizer = Localizer.Get();
    public StoreWindow()
    {
        this.InitializeComponent();
        WindowTitle = localizer.GetLocalizedString("StoreWindowsTitle");
        IconUri = "Assets/Icons/Store.png";
        this.CustomTitleBarUserControl = TitleBarUserControl;

        Instance = this;

        MainFrame = ContentFrame;

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigated += On_Navigated;
        ContentFrame.Navigate(typeof(HomePage));

        NavView.SelectionChanged += NavView_SelectionChanged;

        StoreHelper.Instance.QueueUpdated += StoreHelper_QueueUpdated;
        StoreHelper.Instance.ItemInstallingErrorHappens += Instance_ItemInstallingErrorHappens;

        this.Closed += StoreWindow_Closed;

        SetDownloadsFontIcon();
    }

    private void SetDownloadsFontIcon()
    {
        DownloadsFontIcon.Glyph = SharedUtils.IsOsSupportedNewGlyph() ? "\uEBD3" : "\uE896";
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

    private void Instance_ItemInstallingErrorHappens(Tuple<string, ErrorModel> obj)
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
        // pass
    }
    private double NavViewCompactModeThresholdWidth { get { return NavView.CompactModeThresholdWidth; } }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
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
        if (_isSynchronizingNavigationSelection)
            return;

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

        var sourcePageType = ContentFrame.SourcePageType;
        if (sourcePageType == null) return;

        Debug.WriteLine(sourcePageType.FullName);

        var navigationPageType = GetNavigationPageType(sourcePageType);
        var item = NavView.MenuItems
            .Concat(NavView.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(candidate => string.Equals(
                candidate.Tag?.ToString(),
                navigationPageType.FullName,
                StringComparison.Ordinal));

        if (item != null && !ReferenceEquals(NavView.SelectedItem, item))
        {
            _isSynchronizingNavigationSelection = true;
            try
            {
                NavView.SelectedItem = item;
            }
            finally
            {
                _isSynchronizingNavigationSelection = false;
            }
        }

        TitleBarUserControl.ShowControlsContent =
            !Type.Equals(sourcePageType, navigationPageType);
    }

    private static Type GetNavigationPageType(Type pageType)
    {
        if (pageType == typeof(Views.Store.ItemViewPage) ||
            pageType == typeof(Views.Store.CategoryViewPage))
        {
            return typeof(HomePage);
        }

        if (Views.Store.SettingsPage.MemoryNavigationSupportedPages.Contains(pageType))
            return typeof(Views.Store.SettingsPage);

        return pageType;
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
            TitleBarUserControl.ShowControlsContent = true;
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
