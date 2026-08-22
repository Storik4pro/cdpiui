using CDPIUI.Commands;
using CDPIUI.Controls.Universal;
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
            UtilityButtonControls.HelpUrl = string.Empty;
            NextButton.IsEnabled = true;
            var sel = AnimatedHorizontalContentViewer.SelectedItem;

            if (sel == LicenseItem)
            {
                TryLoadLicense();
                NextButton.IsEnabled = LicenseAgreeCheckBox.IsChecked ?? false;
            }
            else if (sel == AdItem)
            {
                UtilityButtonControls.HelpUrl = "/Other/Ad";
            }

            bool isComplete = sel == CompleteItem;
            UtilityButtonControls.SetButtonVisibilities(
                (BackButton, sel == WelcomeItem || isComplete
                    ? Visibility.Collapsed
                    : Visibility.Visible),
                (SkipButton, sel == StoreItem ? Visibility.Visible : Visibility.Collapsed),
                (NextButton, isComplete ? Visibility.Collapsed : Visibility.Visible),
                (CompleteButton, isComplete ? Visibility.Visible : Visibility.Collapsed));
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

        private async void UpdateStoreDatabase()
        {
            bool result = await StoreHelper.Instance.LoadAllStoreDatabase();


        }

        private void UtilityButtonControls_Loaded(object sender, RoutedEventArgs e)
        {
            CheckNavigation();
        }

        private void CompleteButton_Click(object sender, RoutedEventArgs e)
        {
            CommandsHandler.HandleCommand("cdpiui://");
            if (ShowAppFeaturesCheckBox.IsChecked == true) 
            {
                CommandsHandler.HandleCommand("cdpiui://AppFeatures");
            }
            this.Close();
        }
    }
}
