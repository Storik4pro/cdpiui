using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views;
using CDPI_UI.Views.Components;
using Microsoft.UI;
using Microsoft.UI.Windowing;
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

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ModernMainWindow : WindowEx
    {
        public ModernMainWindow()
        {
            InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;

            this.Title = "CDPI UI";

            var appWindowPresenter = this.AppWindow.Presenter as OverlappedPresenter;
            appWindowPresenter.IsResizable = false;
            appWindowPresenter.IsMaximizable = false;

            NativeWindowHelper.ForceDisableMaximize(this);

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ((App)Application.Current).CurrentTheme;

                if (((App)Application.Current).CurrentTheme == ElementTheme.Dark)
                {
                    ApplyDarkThemeToSystemMenu();
                }
            }

            ExtendsContentIntoTitleBar = true;

            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(ModernMainPage));
            SetTitleBar(WindowMoveAera);

            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(@"Assets/favicon.ico");
        }

        private long LastTimestamp = 0;
        private int DoubleClickTimeMS = 250;


        private void ImageAera_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            if (milliseconds - LastTimestamp < DoubleClickTimeMS && milliseconds != 0)
            {
                this.Close();
            }
            else
            {
                LastTimestamp = milliseconds;
            }

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            RECT pos;
            GetWindowRect(hWnd, out pos);
            IntPtr hMenu = GetSystemMenu(hWnd, false);
            int cmd = TrackPopupMenu(hMenu, 0x100, pos.left + 10, pos.top + 40, 0, hWnd, IntPtr.Zero);
            if (cmd > 0) SendMessage(hWnd, 0x112, (IntPtr)cmd, IntPtr.Zero);
        }

        #region WINAPI

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
           int nReserved, IntPtr hWnd, IntPtr prcRect);
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        struct RECT { public int left, top, right, bottom; }

        #endregion

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigated += On_Navigated;

            NavView.SelectedItem = NavView.MenuItems[0];

            if (ContentFrame.CurrentSourcePageType == null)
                NavView_Navigate(typeof(Views.MainPage), null, new EntranceNavigationTransitionInfo());
        }

        private void NavView_ItemInvoked(NavigationView sender,
                                         NavigationViewItemInvokedEventArgs args)
        {
            FrameNavigationOptions navOptions = new FrameNavigationOptions();
            navOptions.TransitionInfoOverride = args.RecommendedNavigationTransitionInfo;

            if (args.InvokedItemContainer.Tag.ToString() == "AddNewComponent")
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    /*
                    if (AddNavigationViewFlyout.IsOpen)
                        AddNavigationViewFlyout.Hide();

                    AddNavigationViewFlyout.ShowAt(AddNaviagationViewItem);
                    */
                });
                Logger.Instance.CreateDebugLog(nameof(MainWindow), "FLY OPEN");
                return;
            }
            if (args.IsSettingsInvoked == true)
            {
                // pass
            }
            else if (args.InvokedItemContainer != null)
            {
                if (args.InvokedItemContainer.Tag.ToString().StartsWith("CDPI_UI.Views.Components."))
                {
                    string componentName = args.InvokedItemContainer.Tag.ToString().Replace("CDPI_UI.Views.Components.", "");

                    NavView_Navigate(typeof(ViewComponentSettingsPage), StateHelper.Instance.FindKeyByValue(componentName), args.RecommendedNavigationTransitionInfo);

                    return;
                }


                Type navPageType = Type.GetType(args.InvokedItemContainer.Tag.ToString());
                NavView_Navigate(navPageType, null, args.RecommendedNavigationTransitionInfo);
            }
        }

        private void NavView_SelectionChanged(NavigationView sender,
                                              NavigationViewSelectionChangedEventArgs args)
        {
            // pass
        }

        public void NavView_Navigate(
            Type navPageType,
            object parameter,
            NavigationTransitionInfo transitionInfo)
        {
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            if (navPageType is not null)
            {
                if (Type.Equals(navPageType, typeof(ModernMainPage)) && Type.Equals(preNavPageType, typeof(ViewComponentSettingsPage)))
                {
                    
                    if (!RemoveAndGoBackTo(typeof(ModernMainPage), ContentFrame))
                    {
                        ContentFrame.Navigate(typeof(ModernMainPage), parameter, new DrillInNavigationTransitionInfo());
                    }
                }
                else if (!Type.Equals(preNavPageType, navPageType) || Type.Equals(navPageType, typeof(ViewComponentSettingsPage)))
                {
                    ContentFrame.Navigate(navPageType, parameter, transitionInfo);
                }
            }
        }

        private bool RemoveAndGoBackTo(Type pageType, Frame rootFrame)
        {
            if (rootFrame == null) return false;

            var back = rootFrame.BackStack;
            int targetIndex = -1;
            for (int i = back.Count - 1; i >= 0; i--)
            {
                if (back[i].SourcePageType == pageType)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1) return false;

            for (int i = back.Count - 1; i > targetIndex; i--)
                back.RemoveAt(i);

            if (rootFrame.CanGoBack)
            {
                rootFrame.GoBack();
                return true;
            }
            return false;
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
                    /*
                    NavView.SelectedItem = NavView.MenuItems
                                .OfType<NavigationViewItem>()
                                .First(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName.ToString()));
                    */
                }
                catch (Exception ex) { Debug.WriteLine(ex); }
            }
        }

        public void NavigateSubPage(Type page, SlideNavigationTransitionEffect effect)
        {
            try
            {
                ContentFrame.Navigate(page, null, new SlideNavigationTransitionInfo() { Effect = effect });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

        }

        #region WINAPI

        private void ApplyDarkThemeToSystemMenu()
        {
            SetPreferredAppMode(2);
            FlushMenuThemes();
        }

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int SetPreferredAppMode(int preferredAppMode);

        [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern void FlushMenuThemes();

        #endregion
    }
}
