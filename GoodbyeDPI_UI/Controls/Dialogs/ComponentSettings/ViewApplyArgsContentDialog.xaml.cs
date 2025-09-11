using GoodbyeDPI_UI.Helper;
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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Controls.Dialogs
{
    public sealed partial class ViewApplyArgsContentDialog : ContentDialog
    {
        public string DialogTitle { get; set; }
        public string MessageToShow { get; set; }
        public List<string> Args { get; set; }
        public FontFamily ArgsFontFamily { get; set; }
        public double ArgsFontSize { get; set; }
        public Visibility SeparationTextVisible {  get; set; } = Visibility.Visible;
        public string SeparationText { get; set; } = "--new";

        public ViewApplyArgsContentDialog()
        {
            InitializeComponent();

            this.DataContext = this;
            this.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;

            ArgsFontFamily = new FontFamily(SettingsManager.Instance.GetValue<string>("PSEUDOCONSOLE", "fontFamily"));
            ArgsFontSize = SettingsManager.Instance.GetValue<double>("PSEUDOCONSOLE", "fontSize");

            DialogTitleTextBlock.Visibility = !string.IsNullOrEmpty(SeparationText) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }
    }
}
