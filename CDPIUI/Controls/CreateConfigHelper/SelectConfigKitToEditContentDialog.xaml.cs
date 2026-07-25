using CDPIUI.Core.Static;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.LScript;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.CreateConfigHelper
{
    public sealed partial class SelectConfigKitToEditContentDialog : ContentDialog
    {
        public string Result { get; private set; } = string.Empty;

        private readonly ObservableCollection<ViewStoreItemModel> ConfigKits = [];
        public SelectConfigKitToEditContentDialog()
        {
            InitializeComponent();

            ConfigKitListView.ItemsSource = ConfigKits;

            LoadConfigKits();
        }

        private async void LoadConfigKits()
        {
            ConfigKits.Clear();
            List<DatabaseStoreItem> items = DatabaseHelper.Instance.GetItemsByType("configlist");

            foreach (DatabaseStoreItem item in items)
            {
                ConfigKits.Add(new()
                {
                    StoreId = item.Id,
                    Name = item.ShortName,
                    Developer = item.Developer,
                    ColorHEX = item.BackgroudColor,
                    ImageSource = new BitmapImage(UIHelper.GetUriFromString(LScriptLangHelper.ExecuteScript(item.IconPath, scriptArgs:item.Directory)))
                });
            }

            await Task.CompletedTask;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Result = string.Empty;
            this.Hide();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string id = ((Button)sender).Tag.ToString();

            Result = id;
            this.Hide();
        }
    }
}
