using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.Items;
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

namespace GoodbyeDPI_UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ZapretSettingsPage : Page
    {
        private class ComboboxItem
        {
            public string file_name { get; set; }
            public string packId { get; set; }
            public string name { get; set; }
        }

        private string ComponentId = "CSZTBN012";
        private ObservableCollection<ComboboxItem> _comboboxItems = new();

        public ZapretSettingsPage()
        {
            InitializeComponent();

            ConfigChooseCombobox.ItemsSource = _comboboxItems;
            ConfigChooseCombobox.DisplayMemberPath = nameof(ConfigItem.name);

            _comboboxItems.CollectionChanged += ComboboxItems_CollectionChanged; ;
        }

        private void LoadConfigItems()
        {
            ComponentItemsLoaderHelper.Instance.Init();

            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    SettingsManager.Instance.GetValue<string>("COMPONENTS", "nowUsed"));

            List<ConfigItem> items = componentHelper.GetConfigHelper().GetConfigItems();

            _comboboxItems.Clear();

            foreach (ConfigItem item in items)
            {
                ComboboxItem comboboxItem = new ComboboxItem();

                comboboxItem.file_name = item.file_name;
                comboboxItem.packId = item.packId;
                comboboxItem.name = $"{item.name}";

                _comboboxItems.Add(comboboxItem);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadConfigItems();
        }

        private void ComboboxItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ApplySavedSelection();
        }

        private void ConfigChooseCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigChooseCombobox.SelectedItem is ComboboxItem sel)
            {
                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configFile", sel.file_name);
                SettingsManager.Instance.SetValue<string>(["CONFIGS", ComponentId], "configId", sel.packId);
            }
        }

        private void ApplySavedSelection()
        {
            var savedFile = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configFile");
            var savedPackId = SettingsManager.Instance.GetValue<string>(["CONFIGS", ComponentId], "configId");

            if (string.IsNullOrEmpty(savedFile) || string.IsNullOrEmpty(savedPackId))
                return;

            var match = _comboboxItems
                .FirstOrDefault(ci => ci.file_name == savedFile
                                   && ci.packId == savedPackId);
            if (match != null)
                ConfigChooseCombobox.SelectedItem = match;
        }
    }
}
