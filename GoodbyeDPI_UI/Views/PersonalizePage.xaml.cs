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
using GoodbyeDPI_UI.Helper;
using Windows.Storage;
using Microsoft.UI;
using GoodbyeDPI_UI.DesktopWap.Helper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PersonalizePage : Page
    {
        public PersonalizePage()
        {
            this.InitializeComponent();

            PageHeader.Text = "Personalize";

            SetThemeComboBoxValue();
        }
        private void SetThemeComboBoxValue()
        {
            ElementTheme theme = ((App)Application.Current).GetCurrentTheme();
            int selectedIndex;
            if (theme == ElementTheme.Default)
            {
                selectedIndex = 0;
            }
            else if (theme == ElementTheme.Dark)
            {
                selectedIndex = 1;
            }
            else
            {
                selectedIndex = 2;
            }

            ThemeComboBox.SelectedIndex = selectedIndex;
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Content is FrameworkElement frameworkElement)
            {

                ElementTheme newTheme = ElementTheme.Default;

                switch (ThemeComboBox.SelectedIndex)
                {
                    case 0: 
                        frameworkElement.RequestedTheme = ElementTheme.Default;
                        SettingsManager.Instance.SetValue<string>("APPEARANCE", "Theme", "Default");
                        newTheme = ElementTheme.Default;
                        break;
                    case 1:
                        frameworkElement.RequestedTheme = ElementTheme.Dark;
                        SettingsManager.Instance.SetValue<string>("APPEARANCE", "Theme", "Dark");
                        newTheme = ElementTheme.Dark;
                        break;
                    case 2: 
                        frameworkElement.RequestedTheme = ElementTheme.Light;
                        SettingsManager.Instance.SetValue<string>("APPEARANCE", "Theme", "Light");
                        newTheme = ElementTheme.Light;
                        break;
                }
                ((App)Application.Current).UpdateThemeForAllWindows(newTheme);
            }
        }
    }
}
