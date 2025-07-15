using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GoodbyeDPI_UI.Helper;
using System.Linq;

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
    }
}
