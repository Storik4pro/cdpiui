using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
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
using WinUI3Localizer;
using WinUIEx;
using static System.Net.Mime.MediaTypeNames;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPI_UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    
    public class HelpNavigationViewItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string IconGlyph { get; set; }
        public ObservableCollection<HelpNavigationViewItem> Items { get; set; }
    }

    public sealed partial class OfflineHelpWindow : WindowEx
    {
        private MarkdownConfig _config;

        public MarkdownConfig MarkdownConfig
        {
            get => _config;
            set => _config = value;
        }


        private ILocalizer localizer = Localizer.Get();

        public OfflineHelpWindow()
        {
            InitializeComponent();
            SetTitleBar(WindowMoveAera);
            this.Title = UIHelper.GetWindowName(localizer.GetLocalizedString("OfflineHelpWindowTitle"));
            this.ExtendsContentIntoTitleBar = true;

            this.MinWidth = 484;
            this.MinHeight = 300;

            MarkdownConfig = new MarkdownConfig();

            InitHelp();
        }

        private void InitHelp()
        {
            HelpNavigationView.MenuItems.Clear();
            var items = HelpParserHelper.GetHelpItemsForLanguage(Utils.GetStoreLikeLocale());

            if (items == null)
            {
                return; // TODO: error message
            }

            NavigationViewItem mainItem = new();

            foreach (var item in items)
            {
                var homeItem = new NavigationViewItem()
                {
                    Content = item.DisplayName,
                    Tag = item.Path,
                };
                
                
                if (!string.IsNullOrEmpty(item.IconGlyph))
                {
                    homeItem.Icon = new FontIcon()
                    {
                        Glyph = item.IconGlyph,
                    };
                }

                foreach (var subItem in item.Items)
                {
                    homeItem.MenuItems.Add(new NavigationViewItem()
                    {
                        Content = subItem.DisplayName,
                        Tag = subItem.Path,
                    });
                    Debug.WriteLine(subItem.Path);
                }

                if (item.Id == "WelcomeToHelp")
                {
                    mainItem = homeItem;
                }

                HelpNavigationView.MenuItems.Add(homeItem);
            }

            HelpNavigationView.SelectedItem = mainItem;
        }

        public void NavigateToPage(string uri)
        {
            if (!TryNavigateToPage(uri))
            {
                HelpNavigationView.SelectedItem = null;
                MarkdownTextBlock.Text = string.Format(localizer.GetLocalizedString("/Help/NotFoundTemplate"), uri);
            }
        }

        private bool TryNavigateToPage(string uri)
        {
            string[] uriParts = uri.Split('/');
            if (uriParts.Length < 2) return false;

            string category = uriParts[1];
            string page = string.Empty;
            if (uriParts.Length >= 3)
            {
                page = uriParts[2];
            }

            var item = HelpNavigationView.MenuItems
                .Cast<NavigationViewItem>()
                .FirstOrDefault(
                (x) => string.Equals(
                    ((string)x.Tag).Split('\\').Length >= 2 ? ((string)x.Tag).Split('\\')[^1] : string.Empty, 
                    category, 
                    StringComparison.OrdinalIgnoreCase), 
                null);

            if (item == null) return false;

            if (string.IsNullOrEmpty(page))
            {
                HelpNavigationView.SelectedItem = item;
            }
            else
            {
                var subItem = item.MenuItems
                    .Cast<NavigationViewItem>()
                    .FirstOrDefault(
                    (x) => string.Equals(
                        ((string)x.Tag).Split('\\').Length > 2 ? ((string)x.Tag).Split('\\')[^1].Replace(".md", "") : string.Empty, 
                        page, 
                        StringComparison.OrdinalIgnoreCase), 
                    null);

                if (subItem == null) return false;

                HelpNavigationView.SelectedItem = subItem;
            }
            return true;
        }

        private void BackButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(this.SearchAnimatedIcon, "PointerOver");
        }

        private void BackButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            AnimatedIcon.SetState(this.SearchAnimatedIcon, "Normal");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
            */
        }

        private void HelpNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedItem = HelpNavigationView.SelectedItem;
            if (selectedItem == null) return;

            string tag = (string)((NavigationViewItem)selectedItem).Tag;
            if (string.IsNullOrEmpty(tag)) return;

            if (Path.Exists(tag) && File.Exists(tag))
            {
                MarkdownTextBlock.Text = Utils.LoadAllTextFromFile(tag);
            }
        }
    }
}
