using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using CDPI_UI.Helper;
using Microsoft.UI.Xaml.Media.Animation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SystemPage : Page
    {
        public SystemPage()
        {
            this.InitializeComponent();

            PageHeader.Text = "System";
            AutorunToggleSwitch.IsOn = SettingsManager.Instance.GetValue<bool>("SYSTEM", "autorun");
        }

        private void AutorunSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggleSwitch = sender as ToggleSwitch;

            if (toggleSwitch.IsOn)
            {
                new AutoStartManager().AddToAutorun();
            }
            else
            {
                new AutoStartManager().RemoveFromAutorun();
            }
        }

        private void ManageComponents_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.NavigateSubPage(typeof(ComponentsPage), SlideNavigationTransitionEffect.FromRight);
        }
    }
}
