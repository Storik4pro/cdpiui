using GoodbyeDPI_UI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Popups;

namespace GoodbyeDPI_UI.Views
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            ProcessManager.Instance.onProcessStateChanged += OnProcessStateChanged;
            ProcessManager.Instance.ErrorHappens += OnErrorHappens;
            ProcessToggleSwitch.IsOn = ProcessManager.Instance.processState;
            PageHeader.Text = "Home";

            ProcessToggleSwitch.Toggled += ToggleSwitch_Toggled;
        }

        private void OnErrorHappens(string message, string _object)
        {
            ProcessToggleSwitch.IsOn = false;
        }

        private async void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;

            if (toggleSwitch.IsOn)
            {
                await ProcessManager.Instance.StartProcess();
            }
            else
            {
                await ProcessManager.Instance.StopProcess();
            }
        }

        private void OnProcessStateChanged(string state)
        {
            ProcessToggleSwitch.Toggled -= ToggleSwitch_Toggled;
            if (state == "started")
            {
                ProcessToggleSwitch.IsOn = true;
            }
            else
            {
                ProcessToggleSwitch.IsOn = false;
            }
            ProcessToggleSwitch.Toggled += ToggleSwitch_Toggled;
        }

        private void OpenViewWindowButton_Click(object sender, RoutedEventArgs e)
        {
            if (!((App)Application.Current).CheckWindow<ViewWindow>())
            {
                ViewWindow viewWindow = new();
                viewWindow.Activate();
            }
            else
            {
                ((App)Application.Current).ShowWindow<ViewWindow>();
            }
        }

        private void OpenConfigCreateUtil_Click(object sender, RoutedEventArgs e)
        {
            if (!((App)Application.Current).CheckWindow<CreateConfigUtilWindow>())
            {
                CreateConfigUtilWindow viewWindow = new();
                viewWindow.Activate();
            }
            else
            {
                ((App)Application.Current).ShowWindow<CreateConfigUtilWindow>();
            }
        }

        private void OpenUpdatePageButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(UpdatePage));
        }

        private void OpenStoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (!((App)Application.Current).CheckWindow<StoreWindow>())
            {
                StoreWindow storeWindow = new();
                storeWindow.Activate();
            }
            else
            {
                ((App)Application.Current).ShowWindow<StoreWindow>();
            }
        }
        private void CommandInvokedHandler(IUICommand command)
        {
            
        }

        private async void OpenMessageBox_Click(object sender, RoutedEventArgs e)
        {
            CriticalErrorHandlerWindow window = new();
            window.Activate();
        }

        private void OpenViewGoodCheckWindow_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).SafeCreateNewWindow<ViewGoodCheckOutputWindow>();
        }
    }
}
