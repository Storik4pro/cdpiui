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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.Store
{
    public class SubItemModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Developer {  get; set; }
        public string Category { get; set; }
        public string ImageSource { get; set; }
    }
    public sealed partial class SubItemInstallAskContentDialog : ContentDialog
    {
        private ObservableCollection<SubItemModel> items = [];
        public SubItemInstallAskContentDialog(List<SubItemModel> _items)
        {
            InitializeComponent();

            ItemsListView.ItemsSource = items;

            foreach (SubItemModel item in _items)
            {
                items.Add(item);
            }
        }
    }
}
