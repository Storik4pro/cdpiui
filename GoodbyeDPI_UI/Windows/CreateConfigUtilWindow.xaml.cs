using CDPI_UI.Default;
using CDPI_UI.Helper;
using CDPI_UI.Helper.CreateConfigUtil.GoodCheck;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.CreateConfigUtil;
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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CreateConfigUtilWindow : TemplateWindow
    {
        public static CreateConfigUtilWindow Instance { get; private set; }

        private ILocalizer localizer = Localizer.Get();

        public CreateConfigUtilWindow()
        {
            InitializeComponent();

            this.Title = localizer.GetLocalizedString("CreateConfigUtilWindowTitle");
            IconUri = @"Assets/Icons/GoodCheck.ico";
            TitleIcon = TitleImageRectagle;
            TitleBar = WindowMoveAera;
            DisableResizeFeature();

            Instance = this;

            ExtendsContentIntoTitleBar = true;

            ContentFrame.Navigate(typeof(Views.CreateConfigUtil.MainPage));
            SetTitleBar(WindowMoveAera);

            this.Closed += CreateConfigUtilWindow_Closed;
        }



        public void ToggleLoadingState(TaskbarProgressBarState loadingState, int currentLoadingValue = 0, int maxLoadingValue = 100)
        {
            TaskbarManager.Instance.SetProgressState(loadingState);
            if (loadingState != TaskbarProgressBarState.Indeterminate)
                TaskbarManager.Instance.SetProgressValue(currentLoadingValue, maxLoadingValue);
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
                    Content = localizer.GetLocalizedString("GoodCheckAskStopSelection"),
                    PrimaryButtonText = localizer.GetLocalizedString("Yes"),
                    CloseButtonText = localizer.GetLocalizedString("No"),
                    XamlRoot = this.Content.XamlRoot
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    GoodCheckProcessHelper.Instance.Stop();
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
                GoodCheckProcessHelper.Instance.Stop();
            }
            else if (GoodCheckProcessHelper.Instance.IsRunned())
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
            if (!GoodCheckProcessHelper.Instance.IsRunned())
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
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }
    }
}
