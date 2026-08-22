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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.Universal
{
    public sealed partial class HorizontalSeparatedListView : ListView
    {
        public HorizontalSeparatedListView()
        {
            try
            {
                InitializeComponent();
            }
            catch { }

        }
    }

    public class ItemsDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate AllItems { get; set; }
        public DataTemplate LastItems { get; set; }

        protected override DataTemplate SelectTemplateCore(
            object item,
            DependencyObject container)
        {
            var itemsControl =
                ItemsControl.ItemsControlFromItemContainer(container);

            if (itemsControl is null)
                return AllItems;

            var index = itemsControl.IndexFromContainer(container);

            if (index < 0)
                return AllItems;

            return index == itemsControl.Items.Count - 1
                ? LastItems
                : AllItems;
        }
    }
}
