using GoodbyeDPI_UI.Helper;
using Microsoft.UI;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Controls.Store
{
    public sealed partial class StoreLibraryItemButton : UserControl
    {
        public StoreLibraryItemButton()
        {
            InitializeComponent();
        }

        public string StoreId
        {
            get { return (string)GetValue(StoreIdProperty); }
            set { 
                SetValue(StoreIdProperty, value);
                CheckAvailiableActions();
            }
        }

        public static readonly DependencyProperty StoreIdProperty =
            DependencyProperty.Register(
                nameof(StoreId), typeof(string), typeof(StoreLibraryItemButton), new PropertyMetadata(string.Empty)
            );

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(StoreLibraryItemButton), new PropertyMetadata(string.Empty)
            );

        public string Developer
        {
            get { return (string)GetValue(DeveloperProperty); }
            set { SetValue(DeveloperProperty, value); }
        }

        public static readonly DependencyProperty DeveloperProperty =
            DependencyProperty.Register(
                nameof(Developer), typeof(string), typeof(StoreLibraryItemButton), new PropertyMetadata(string.Empty)
            );

        public string Category
        {
            get { return (string)GetValue(CategoryProperty); }
            set { SetValue(CategoryProperty, value); }
        }

        public static readonly DependencyProperty CategoryProperty =
            DependencyProperty.Register(
                nameof(Category), typeof(string), typeof(StoreLibraryItemButton), new PropertyMetadata(string.Empty)
            );

        public ImageSource CardImageSource
        {
            get { return (ImageSource)GetValue(CardImageSourceProperty); }
            set { SetValue(CardImageSourceProperty, value); }
        }

        public static readonly DependencyProperty CardImageSourceProperty =
            DependencyProperty.Register(
                nameof(CardImageSource), typeof(ImageSource), typeof(StoreLibraryItemButton), new PropertyMetadata(null)
            );

        public Brush CardBackgroundBrush
        {
            get { return (Brush)GetValue(CardBackgroundProperty); }
            set { 
                SetValue(CardBackgroundProperty, value); 
                Rectangle.Fill = value;
            }
        }

        public static readonly DependencyProperty CardBackgroundProperty =
            DependencyProperty.Register(
                nameof(CardBackgroundBrush),
                typeof(Brush),
                typeof(StoreLibraryItemButton),
                new PropertyMetadata(new SolidColorBrush(Colors.Transparent))
            );

        private void CheckAvailiableActions()
        {
            DatabaseStoreItem databaseStoreItem = DatabaseHelper.Instance.GetItemById(StoreId);

            BigActionButton.IsEnabled = true;
            ActionButton.IsEnabled = true;
            DeleteButton.IsEnabled = true;
            ReinstallButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;

            if (databaseStoreItem.Type == "configlist")
            {
                BigActionButton.IsEnabled = false;
                ActionButton.IsEnabled = false;
            }

            if (databaseStoreItem.Id == StateHelper.LocalUserItemsId)
            {
                DeleteButton.IsEnabled = false;
                ReinstallButton.IsEnabled = false;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate("ForwardConnectedAnimation", PART_Image);

            StoreWindow.Instance.NavigateSubPage(typeof(Views.Store.ItemViewPage), StoreId, new SuppressNavigationTransitionInfo());
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ReinstallButton_Click(object sender, RoutedEventArgs e)
        {
            StoreHelper.Instance.RemoveItem(StoreId);
            DeleteButton.IsEnabled = false;
            ReinstallButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            StoreHelper.Instance.AddItemToQueue(StoreId, string.Empty);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            StoreHelper.Instance.RemoveItem(StoreId);
            ProgressBar.Visibility = Visibility.Visible;
            DeleteButton.IsEnabled = false;
            ReinstallButton.IsEnabled = false;
        }
    }
}
