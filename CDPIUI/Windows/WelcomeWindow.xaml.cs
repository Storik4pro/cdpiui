using CDPIUI.Core.Data;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Repository.Localization;
using CDPIUI.Core.System;
using CDPIUI.Default;
using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;


namespace CDPIUI
{
    public sealed partial class WelcomeWindow : TemplateWindow
    {
        private ILocalizer localizer = Localizer.Get();

        private MarkdownConfig _config;

        public MarkdownConfig MarkdownConfig
        {
            get => _config;
            set => _config = value;
        }

        public WelcomeWindow()
        {
            InitializeComponent();

            WindowTitle = localizer.GetLocalizedString("WelcomeWindowTitle");
            IconUri = @"Assets/Icons/find_error.png";
            this.CustomTitleBarUserControl = TitleBarUserControl;

            DisableResizeFeature();

            _config = new MarkdownConfig();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            AnimatedHorizontalContentViewer.GoNext();
            CheckNavigation();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            AnimatedHorizontalContentViewer.GoPrevious();
            CheckNavigation();
        }

        private void CheckNavigation()
        {
            NextButton.IsEnabled = true;
            var sel = AnimatedHorizontalContentViewer.SelectedItem;

            if (sel == WelcomeItem)
            {
                BackButton.Visibility = Visibility.Collapsed;
            }
            else if (sel == LicenseItem)
            {
                TryLoadLicense();
                BackButton.Visibility = Visibility.Visible;
                NextButton.IsEnabled = false;
            }
        }

        private void TryLoadLicense()
        {
            string path = Path.Combine(Directories.ELUADirectory, StoreLocalizationHelper.GetStoreLikeLocale(), "ELUA.md");
            try
            {
                LicenseTextBlock.Text = ShellHelper.LoadAllTextFromFile(path);
                LicenseAgreeCheckBox.IsEnabled = true;
            }
            catch 
            {
                LicenseAgreeCheckBox.IsEnabled = false;
                LicenseTextBlock.Text = string.Format(localizer.GetLocalizedString("/WelcomeWizard/UnableLoadLicense"), path);
            }
        }

        private void LicenseAgreeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            NextButton.IsEnabled = LicenseAgreeCheckBox.IsChecked ?? false;
        }
    }
}
