using CDPI_UI.Helper;
using CDPI_UI.Helper.LScript;
using CDPI_UI.Helper.Static;
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
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Views.Store
{
    public class LibraryItemModel
    {
        public string StoreId { get; set; }
        public string Title { get; set; }
        public string Developer { get; set; }
        public string Category { get; set; }
        public ImageSource ImageSource { get; set; }
        public Brush CardBackgroundBrush {  get; set; }
    }

    public sealed partial class LibraryPage : Page
    {
        private ObservableCollection<LibraryItemModel> _libraryItems = [];

        private ILocalizer localizer = Localizer.Get();
        public LibraryPage()
        {
            InitializeComponent();

            ItemsListView.ItemsSource = _libraryItems;
            this.Loaded += LibraryPage_Loaded;

            StoreHelper.Instance.ItemRemoved += StoreHelper_ItemRemoved;
            StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemActionsStopped;
        }

        private void LibraryPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadItems();
            this.Loaded -= LibraryPage_Loaded;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            StoreHelper.Instance.ItemRemoved -= StoreHelper_ItemRemoved;
            StoreHelper.Instance.ItemActionsStopped -= StoreHelper_ItemActionsStopped;
        }

        public async void LoadItems()
        {
            _libraryItems.Clear();
            List<DatabaseStoreItem> databaseStoreItems = DatabaseHelper.Instance.GetAllInstalledItems();

            foreach (DatabaseStoreItem item in databaseStoreItems)
            {
                string title = StoreHelper.Instance.GetLocalizedStoreItemName(item.Name, Utils.GetStoreLikeLocale());
                title = title.StartsWith("slocale:")? item.ShortName : title;

                string category = localizer.GetLocalizedString(item.Type);
                category = string.IsNullOrEmpty(category) ? item.Type : category;

                string eImageSource = StoreHelper.Instance.ExecuteScript(item.IconPath);
                BitmapImage image = new BitmapImage(new Uri(eImageSource));

                SolidColorBrush solidColorBrush = UIHelper.HexToSolidColorBrushConverter(item.BackgroudColor);

                LibraryItemModel libraryItemModel = new()
                {
                    StoreId = item.Id,
                    Title = title,
                    Developer = item.Developer,
                    Category = category,
                    ImageSource = image,
                    CardBackgroundBrush = solidColorBrush
                };
                _libraryItems.Add(libraryItemModel);
            }
            // _libraryItems.Reverse();
            await Task.CompletedTask;
        }

        private void StoreHelper_ItemActionsStopped(string obj)
        {
            LoadItems();
        }

        private void StoreHelper_ItemRemoved(string obj)
        {
            LoadItems();
        }
    }
}
