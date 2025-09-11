using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.Helper.Items;
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
using System.ComponentModel;
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
    public enum SelectResult
    {
        Selected,
        Canceled,
        Nothing
    }
    public sealed partial class SelectConfigToEditContentDialog : ContentDialog
    {
        private class ComponentModel
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        public ICommand SelectConfigCommand { get; }

        public ConfigItem SelectedConfigItem { get; private set; }
        public SelectResult SelectedConfigResult { get; private set; } = SelectResult.Nothing;

        private ObservableCollection<ConfigToEditModel> ConfigModels = new();
        public SelectConfigToEditContentDialog()
        {
            InitializeComponent();
            this.DataContext = this;

            SelectConfigCommand = new RelayCommand(p => ConfigSelected((Tuple<string, string>)p));
            ConfigsListView.ItemsSource = ConfigModels;

            InitDialog();
        }

        private void InitDialog()
        {
            List<ComponentModel> components = new();
            foreach (var component in StateHelper.Instance.ComponentIdPairs)
            {
                if (component.Key == "ASGKOI001")
                    continue;
                components.Add(new()
                {
                    Id = component.Key,
                    Name = component.Value
                });
            }
            ComponentChooseComboBox.ItemsSource = components;
        }

        private void InitModel(string componentId)
        {
            ConfigModels.Clear();
            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    componentId);

            List<ConfigItem> items = componentHelper.GetConfigHelper().GetConfigItems();

            foreach (var item in items)
            {
                ConfigModels.Add(
                    new()
                    {
                        DisplayName = item.name,
                        Directory = item.file_name,
                        PackId = item.packId,
                        PackName = DatabaseHelper.Instance.GetItemById(item.packId).ShortName,
                    });
            }

            if (ConfigModels.Count == 0)
            {
                SelectText.Visibility = Visibility.Collapsed;
                ConfigsScrollViewer.Visibility = Visibility.Collapsed;
            }
            else
            {
                SelectText.Visibility = Visibility.Visible;
                ConfigsScrollViewer.Visibility = Visibility.Visible;
            }
        }

        private void ComponentChooseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitModel((ComponentChooseComboBox.SelectedItem as ComponentModel).Id);
        }

        private void ConfigSelected(Tuple<string, string> tuple)
        {
            ComponentHelper componentHelper =
                ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(
                    (ComponentChooseComboBox.SelectedItem as ComponentModel).Id);
            List<ConfigItem> configItems = componentHelper.GetConfigHelper().GetConfigItems();

            foreach (var item in configItems)
            {
                if (item.packId == tuple.Item2 && item.file_name == tuple.Item1)
                {
                    SelectedConfigItem = item;
                    break;
                } 
            }
            SelectedConfigResult = SelectResult.Selected;
            this.Hide();
        }

        
    }
}
