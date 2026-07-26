using CDPIUI.Default;
using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.Static;
using CDPIUI.ViewModels;
using CDPIUI.Views;
using CDPIUI.Views.Main.Components;
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
using System.Collections.Specialized;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI
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
            IconUri = @"Assets/favicon.ico";
            this.CustomTitleBarUserControl = TitleBarUserControl;

            MainFrame = ContentFrame;

            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(GetMainPage(), new NameValueCollection());

            if (!SettingsManager.Instance.GetValue<bool>("AD", "welcomeToPreview"))
            {
                ShowDialog(localizer.GetLocalizedString("PreviewVersionDescription"), localizer.GetLocalizedString("PreviewVersion"));
                SettingsManager.Instance.SetValue("AD", "welcomeToPreview", true);
            }
        }

        private static Type GetMainPage()
        {
            if (SettingsManager.Instance.GetValue<string>("APPEARANCE", "mainPageMarkup") == MarkupTypes.Classic.ToString())
            {
                return typeof(MainPage);
            }
            else
            {
                return typeof(ModernMainPage);
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
                NavView_Navigate(GetMainPage(), null, new EntranceNavigationTransitionInfo());
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
                if (args.InvokedItemContainer.Tag.ToString().StartsWith("CDPIUI.Views.Components."))
                {
                    // string componentName = args.InvokedItemContainer.Tag.ToString().Replace("CDPIUI.Views.Components.", "");

                    // NavView_Navigate(typeof(ViewComponentSettingsPage), StateHelper.Instance.FindKeyByValue(componentName), args.RecommendedNavigationTransitionInfo);

                    return;
                }
                if (args.InvokedItemContainer.Tag.ToString().StartsWith("MAINPAGE"))
                {
                    NavView_Navigate(GetMainPage(), null, args.RecommendedNavigationTransitionInfo);
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
                if (Type.Equals(navPageType, GetMainPage()) && Type.Equals(preNavPageType, typeof(ViewComponentSettingsPage)))
                {
                    
                    if (!RemoveAndGoBackTo(GetMainPage(), ContentFrame))
                    {
                        ContentFrame.Navigate(GetMainPage(), parameter ?? new NameValueCollection(), new DrillInNavigationTransitionInfo());
                    }
                }
                else if (!Type.Equals(preNavPageType, navPageType) || Type.Equals(navPageType, typeof(ViewComponentSettingsPage)))
                {
                    ContentFrame.Navigate(navPageType, parameter ?? new NameValueCollection(), transitionInfo);
                }
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
                    /*
                    NavView.SelectedItem = NavView.MenuItems
                                .OfType<NavigationViewItem>()
                                .First(i => i.version.Equals(ContentFrame.SourcePageType.FullName.ToString()));
                    */
                }
                catch (Exception ex) { Debug.WriteLine(ex); }
            }
        }

        public void NavigateSubPage(Type page, SlideNavigationTransitionEffect effect)
        {
            try
            {
                ContentFrame.Navigate(page, new NameValueCollection(), new SlideNavigationTransitionInfo() { Effect = effect });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

        }
    }
}
