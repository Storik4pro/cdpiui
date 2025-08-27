using GoodbyeDPI_UI.ViewModels;
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
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Controls.Dialogs.CreateConfigHelper
{
    public class ConfigToEditModel
    {
        public string DisplayName { get; set; }
        public string PackName { get; set; }
        public string Directory { get; set; }
        public string PackId { get; set; }

    }
    public sealed partial class SelectConfigToEditContentDialog : ContentDialog
    {
        public ICommand SelectConfigCommand { get; }

        private ObservableCollection<ConfigToEditModel> ConfigModels = new();
        public SelectConfigToEditContentDialog()
        {
            InitializeComponent();
            this.DataContext = this;

            SelectConfigCommand = new RelayCommand(p => ConfigSelected((Tuple<string, string>)p));
            ConfigsListView.ItemsSource = ConfigModels;
        }



        private void ConfigSelected(Tuple<string, string> tuple)
        {
            this.Hide();
        }
    }
}
