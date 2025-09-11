using CDPI_UI.Controls.Dialogs;
using CDPI_UI.ViewModels;
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
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    public sealed partial class CreateConfigDesignerUserControl : UserControl
    {
        public ICommand ViewFullCommand { get; }
        public CreateConfigDesignerUserControl()
        {
            InitializeComponent();

            ViewFullCommand = new RelayCommand(p => ViewFullArgs());
        }

        public string Args
        {
            get { return (string)GetValue(ArgsProperty); }
            set { SetValue(ArgsProperty, value); }
        }

        public static readonly DependencyProperty ArgsProperty =
            DependencyProperty.Register(
                nameof(Args), typeof(string), typeof(CreateConfigDesignerUserControl), new PropertyMetadata(string.Empty)
            );
        public string SiteListName
        {
            get { return (string)GetValue(SiteListNameProperty); }
            set { SetValue(SiteListNameProperty, value); }
        }

        public static readonly DependencyProperty SiteListNameProperty =
            DependencyProperty.Register(
                nameof(SiteListName), typeof(string), typeof(CreateConfigDesignerUserControl), new PropertyMetadata(string.Empty)
            );

        private void ViewFullArgs()
        {
            ViewApplyArgsContentDialog dialog = new()
            {
                XamlRoot = this.XamlRoot,
                Title = "View arguments",
                DialogTitle = string.Empty,
                SeparationTextVisible = Visibility.Collapsed,
                Args = [this.Args ?? string.Empty]

            };
            _ = dialog.ShowAsync();
        }
    }
}
