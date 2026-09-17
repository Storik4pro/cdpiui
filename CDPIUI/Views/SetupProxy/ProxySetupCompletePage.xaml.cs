using CDPIUI.Controls.Default;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;

namespace CDPIUI.Views.SetupProxy
{
    public sealed partial class ProxySetupCompletePage : TemplatePage
    {
        public ProxySetupCompletePage()
        {
            InitializeComponent();

            IsForwardAnimationToPageAvailable = true;
            ElementToAnimateForwardConnectedAnimation = ActionButtonsGrid;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).CloseWindow<ProxySetupUtilWindow>();
        }

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: open help
        }
    }
}
