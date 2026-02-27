using CDPI_UI.Default;
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
using Windows.UI.Popups;
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
    public sealed partial class ModernMainWindow : TemplateWindow
    {
        private ILocalizer localizer = Localizer.Get();
        public ModernMainWindow()
        {
            InitializeComponent();
            this.Title = "CDPI UI";
            IconUri = @"Assets/favicon.ico";
            TitleIcon = ImageAera;
            TitleBar = WindowMoveAera;

            DisableResizeFeature(isMinimizable:true);

            SetTitleBar(WindowMoveAera);

            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(ModernMainPage));

            if (!SettingsManager.Instance.GetValue<bool>("AD", "welcomeToPreview"))
            {
                ShowDialog(localizer.GetLocalizedString("PreviewVersionDescription"), localizer.GetLocalizedString("PreviewVersion"));
                SettingsManager.Instance.SetValue("AD", "welcomeToPreview", true);
            }
        }


        private async void ShowDialog(string message, string title)
        {
            var dlg = new MessageDialog(message, title);
            InitializeWithWindow.Initialize(dlg, WindowNative.GetWindowHandle(this));
            await dlg.ShowAsync();
        }


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
    }
}
