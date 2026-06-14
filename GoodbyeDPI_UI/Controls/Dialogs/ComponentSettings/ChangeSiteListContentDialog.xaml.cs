using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CDPI_UI.ViewModels;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;
using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI.Controls.Dialogs.ComponentSettings
{
    public enum FileSelectionType
    {
        None,
        FromTheStore,
        FromLocalDrive,
    }
    public sealed partial class ChangeSiteListContentDialog : ContentDialog
    {
        private string _listTitle = string.Empty;
        public string ListTitle
        {
            get => _listTitle;
            set
            {
                _listTitle = value;
                this.Title = string.Format(localizer.GetLocalizedString("ChangeListContentDialogTitle"), ListTitle.ToLower());
            }
        }
        public string PackId = string.Empty;
        public string FileName = string.Empty;
        public string NewFileName { get; private set; } = string.Empty;
        public FileSelectionType SelectionType { get; private set; } = FileSelectionType.None;

        private bool isIpSet = false;
        public bool IsIpSet
        {
            get
            {
                return isIpSet;
            }
            set
            {
                isIpSet = value;
                Init();
            }
        }

        public bool IsDialogFinishedSuccessfully = false;

        private ILocalizer localizer = Localizer.Get();

        private ObservableCollection<ViewSiteListModel> SiteListModels = [];
        private ObservableCollection<ViewSiteListModel> ManualSiteListModels = [];

        public ChangeSiteListContentDialog()
        {
            InitializeComponent();

            StoreListView.ItemsSource = SiteListModels;
            ManualListView.ItemsSource = ManualSiteListModels;
            MainSelectorBar.SelectedItem = FromTheStoreSelectorBarItem;

            AuditIsApplyButtonAvailable();
        }

        private void Init()
        {
            SiteListModels.Clear();
            ManualSiteListModels.Clear();

            var items = DatabaseHelper.Instance.GetItemsByType(IsIpSet ? "isubscription" : "lsubscription");

            foreach (var item in items)
            {
                foreach (var file in Directory.EnumerateFiles(item.Directory))
                {
                    SiteListModels.Add(new()
                    {
                        Title = Path.GetFileName(file),
                        Developer = $"{item.ShortName} – {item.Developer}",
                        FileName = file,
                        Size = Utils.FormatSize(Utils.GetFileSize(file))
                    });
                }
            }

            StoreNoItemsPlaceholder.Visibility = SiteListModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            
            var localItems = SettingsManager.Instance.GetValue<List<string>>("USERCHOISES", IsIpSet ? "ipSets" : "siteLists");
            foreach (var item in localItems)
            {
                if (!Path.Exists(item)) continue;
                ManualSiteListModels.Add(new()
                {
                    Title = Path.GetFileName(item),
                    Developer = Path.GetFullPath(item),
                    FileName = item,
                    Size = Utils.FormatSize(Utils.GetFileSize(item))
                });
            }
            RecentFilesNotFoundPlaceholder.Visibility = ManualSiteListModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void ViewInStoreButton_Click(object sender, RoutedEventArgs e)
        {
            var window = await ((App)Application.Current).SafeCreateNewWindow<StoreWindow>();
            window.NavigateSubPage(typeof(Views.Store.CategoryViewPage), "C004SS", new SuppressNavigationTransitionInfo());
        }

        private void SelectFileManuallyButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = IsIpSet ? localizer.GetLocalizedString("ChooseIpList") : localizer.GetLocalizedString("ChooseSiteList");
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                openFileDialog.Filter = $"{localizer.GetLocalizedString("TextFiles")} (*.txt)|*.txt";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }
            var model = ManualSiteListModels.FirstOrDefault(x => x.FileName == filePath);
            if (model != null)
            {
                ManualListView.SelectedItem = model;
                return;
            }

            model = new()
            {
                Title = Path.GetFileName(filePath),
                Developer = Path.GetFullPath(filePath),
                FileName = filePath,
                Size = Utils.FormatSize(Utils.GetFileSize(filePath))
            };
            ManualSiteListModels.Add(model);

            ManualListView.SelectedItem = model;

            List<string> localItems = [];
            foreach (var item in ManualSiteListModels)
            {
                localItems.Add(item.FileName);
            }
            
            if (localItems.Count > 0) SettingsManager.Instance.SetValue<List<string>>("USERCHOISES", IsIpSet ? "ipSets" : "siteLists", localItems);

            RecentFilesNotFoundPlaceholder.Visibility = ManualSiteListModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AuditIsApplyButtonAvailable()
        {
            bool isApplyAvailable = 
                (MainSelectorBar.SelectedItem == FromTheStoreSelectorBarItem && StoreListView.SelectedItem != null) ||
                (MainSelectorBar.SelectedItem != FromTheStoreSelectorBarItem && ManualListView.SelectedItem != null);

            IsPrimaryButtonEnabled = isApplyAvailable;
        }

        private void MainSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            AuditIsApplyButtonAvailable();
            if (MainSelectorBar.SelectedItem == FromTheStoreSelectorBarItem)
            {
                StoreGrid.Visibility = Visibility.Visible;
                LocalGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                StoreGrid.Visibility = Visibility.Collapsed;
                LocalGrid.Visibility = Visibility.Visible;
            }
        }

        private void StoreListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AuditIsApplyButtonAvailable();
        }

        private void ManualListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AuditIsApplyButtonAvailable();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (MainSelectorBar.SelectedItem == FromTheStoreSelectorBarItem)
            {
                var item = StoreListView.SelectedItem;
                if (item != null && item is ViewSiteListModel siteListItem)
                {
                    NewFileName = siteListItem.FileName;
                    SelectionType = FileSelectionType.FromTheStore;
                }
            }
            else
            {
                var item = ManualListView.SelectedItem;
                if (item != null && item is ViewSiteListModel siteListItem)
                {
                    NewFileName = siteListItem.FileName;
                    SelectionType = FileSelectionType.FromLocalDrive;
                }
            }

                IsDialogFinishedSuccessfully = true;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsDialogFinishedSuccessfully = false;
        }
    }
}
