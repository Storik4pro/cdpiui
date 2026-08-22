using CDPIUI.Default;
using CDPIUI.Core;

using CDPIUI.Messages;
using CDPIUI.Views.CreateConfigUtil;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using WinUI3Localizer;
using WinUIEx;
using CDPIUI.AddOns.GoodCheck;
using System.Collections.Specialized;
using BlockCheck2MainPage = CDPIUI.Views.BlockCheck2.MainPage;


namespace CDPIUI
{
    public sealed partial class CreateConfigUtilWindow : TemplateWindow
    {
        public static CreateConfigUtilWindow Instance { get; private set; }

        private string targetStoreId = string.Empty;
        public string TargetStoreId
        {
            get => targetStoreId;
            set
            {
                targetStoreId = value;
                if (ContentFrame.SourcePageType == typeof(Views.CreateConfigUtil.MainPage))
                    ContentFrame.Navigate(typeof(Views.CreateConfigUtil.MainPage), new NameValueCollection() { { "componentId", TargetStoreId } });
            }
        }

        private ILocalizer localizer = Localizer.Get();

        public CreateConfigUtilWindow()
        {
            InitializeComponent();

            WindowTitle = localizer.GetLocalizedString("CreateConfigUtilWindowTitle");
            IconUri = @"Assets/Icons/GoodCheck.ico";
            this.CustomTitleBarUserControl = TitleBarUserControl;
            DisableResizeFeature();

            Instance = this;

            MainFrame = ContentFrame;

            ContentFrame.Navigate(typeof(Views.CreateConfigUtil.MainPage), new NameValueCollection() { { "componentId", TargetStoreId } });

            this.Closed += CreateConfigUtilWindow_Closed;

            this.Activated += CreateConfigUtilWindow_Activated;
        }

        private void CreateConfigUtilWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= CreateConfigUtilWindow_Activated;
        }

        public void NavigateToPage<T>(object parameter = null)
        {
            this.Activate();
            if (ContentFrame.CurrentSourcePageType == typeof(MainPage))
            {

                ContentFrame.Navigate(typeof(T), parameter);
                ContentFrame.BackStack.Clear();
            }
            
        }

        private readonly SemaphoreSlim _dialogLock = new SemaphoreSlim(1, 1);
        private bool isDialogOpened = false;

        private async void AskForExit()
        {
            await _dialogLock.WaitAsync();
            isDialogOpened = true;
            try
            {
                ContentDialog dialog = new()
                {
                    Title = localizer.GetLocalizedString("ConfirmationRequired"),
                    Content = localizer.GetLocalizedString(IsBlockCheck2Running
                        ? "BlockCheck2AskStopSelection"
                        : "GoodCheckAskStopSelection"),
                    PrimaryButtonText = localizer.GetLocalizedString("Yes"),
                    CloseButtonText = localizer.GetLocalizedString("No"),
                    XamlRoot = this.Content.XamlRoot
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    StopActiveSelection();
                    this.Close();
                }
            }
            catch
            {

            }
            finally
            {
                isDialogOpened = false;
                _dialogLock.Release();

            }
        }

        private void CreateConfigUtilWindow_Closed(object sender, WindowEventArgs args)
        {
            if (isDialogOpened)
            {
                StopActiveSelection();
            }
            else if (IsSelectionRunning)
            {
                AskForExit();
                args.Handled = true;
                return;
            }
            
            Instance = null;
            this.Closed -= CreateConfigUtilWindow_Closed;
            
        }

        ~CreateConfigUtilWindow()
        {
            if (!GoodCheckProcessService.Instance.IsRunned())
            {
                Instance = null;
            }
            Debug.WriteLine("CreateConfigUtilWindow finalized");
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
            if (IsSelectionRunning)
            {
                return;
            }

            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private bool IsBlockCheck2Running =>
            ContentFrame.Content is BlockCheck2MainPage page && page.ViewModel.IsRunning;

        private bool IsSelectionRunning =>
            GoodCheckProcessService.Instance.IsRunned() || IsBlockCheck2Running;

        private void StopActiveSelection()
        {
            if (GoodCheckProcessService.Instance.IsRunned())
            {
                GoodCheckProcessService.Instance.Stop();
            }

            if (ContentFrame.Content is BlockCheck2MainPage page)
            {
                page.CancelRunningSession();
            }
        }
    }
}
