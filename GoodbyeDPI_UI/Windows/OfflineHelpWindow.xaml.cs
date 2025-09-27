using CDPI_UI.Helper.Static;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OfflineHelpWindow : Window
    {
        private ILocalizer localizer = Localizer.Get();
        public OfflineHelpWindow()
        {
            InitializeComponent();
            SetTitleBar(WindowMoveAera);
            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("OfflineHelpWindowTitle"));
            this.ExtendsContentIntoTitleBar = true;
        }

        public void NavigateToPage(string uri)
        {
            // ContentFrame.Navigate(page.GetType());
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
            /*
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
            */
        }
    }
}
