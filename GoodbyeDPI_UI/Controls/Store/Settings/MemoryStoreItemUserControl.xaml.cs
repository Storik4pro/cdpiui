using CDPI_UI.Helper;
using CDPI_UI.Helper.Static;
using CDPI_UI.Views.Components;
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

namespace CDPI_UI.Controls.Store.Settings
{
    public sealed partial class MemoryStoreItemUserControl : UserControl
    {
        public MemoryStoreItemUserControl()
        {
            InitializeComponent();
        }

        public string StoreId
        {
            get { return (string)GetValue(StoreIdProperty); }
            set
            {
                SetValue(StoreIdProperty, value);
                CheckAvailiableActions();
                VersionTextBlock.Text = DatabaseHelper.Instance.GetItemById(StoreId)?.CurrentVersion.Replace("v", "") ?? string.Empty;
            }
        }

        public static readonly DependencyProperty StoreIdProperty =
            DependencyProperty.Register(
                nameof(StoreId), typeof(string), typeof(MemoryStoreItemUserControl), new PropertyMetadata(string.Empty)
            );

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(MemoryStoreItemUserControl), new PropertyMetadata(string.Empty)
            );

        public string Developer
        {
            get { return (string)GetValue(DeveloperProperty); }
            set { SetValue(DeveloperProperty, value); }
        }

        public static readonly DependencyProperty DeveloperProperty =
            DependencyProperty.Register(
                nameof(Developer), typeof(string), typeof(MemoryStoreItemUserControl), new PropertyMetadata(string.Empty)
            );

        public string Category
        {
            get { return (string)GetValue(CategoryProperty); }
            set { SetValue(CategoryProperty, value); }
        }

        public static readonly DependencyProperty CategoryProperty =
            DependencyProperty.Register(
                nameof(Category), typeof(string), typeof(MemoryStoreItemUserControl), new PropertyMetadata(string.Empty)
            );

        public ImageSource CardImageSource
        {
            get { return (ImageSource)GetValue(CardImageSourceProperty); }
            set { SetValue(CardImageSourceProperty, value); }
        }

        public static readonly DependencyProperty CardImageSourceProperty =
            DependencyProperty.Register(
                nameof(CardImageSource), typeof(ImageSource), typeof(MemoryStoreItemUserControl), new PropertyMetadata(null)
            );

        public Brush CardBackgroundBrush
        {
            get { return (Brush)GetValue(CardBackgroundProperty); }
            set
            {
                SetValue(CardBackgroundProperty, value);
                Rectangle.Fill = value;
            }
        }

        public static readonly DependencyProperty CardBackgroundProperty =
            DependencyProperty.Register(
                nameof(CardBackgroundBrush),
                typeof(Brush),
                typeof(MemoryStoreItemUserControl),
                new PropertyMetadata(new SolidColorBrush(Colors.Transparent))
            );

        public double SizeText
        {
            get { return (double)GetValue(SizeTextProperty); }
            set { 
                SetValue(SizeTextProperty, value);
                SizeTextBlock.Text = Utils.FormatSize((long)value);
            }
        }

        public static readonly DependencyProperty SizeTextProperty =
            DependencyProperty.Register(
                nameof(SizeText), typeof(double), typeof(MemoryStoreItemUserControl), new PropertyMetadata(0)
            );

        private void CheckAvailiableActions()
        {
            DatabaseStoreItem databaseStoreItem = DatabaseHelper.Instance.GetItemById(StoreId);

            SetupMenuFlyuoutItem.IsEnabled = true;
            DeleteButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;

            if (databaseStoreItem.Type == "configlist")
            {
                SetupMenuFlyuoutItem.IsEnabled = false;
            }

            if (databaseStoreItem.Id == StateHelper.LocalUserItemsId || databaseStoreItem.Id == StateHelper.ApplicationStoreId)
            {
                SetupMenuFlyuoutItem.IsEnabled = false;
                DeleteButton.IsEnabled = false;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            StoreHelper.Instance.RemoveItem(StoreId);
            ProgressBar.Visibility = Visibility.Visible;
            DeleteButton.IsEnabled = false;
            SetupMenuFlyuoutItem.IsEnabled = false;
        }

        private void ShowFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Helper.Static.Utils.OpenFileInDefaultApp(DatabaseHelper.Instance.GetItemById(StoreId).Directory);
            }
            catch
            {

            }
        }

        private async void SetupMenuFlyuoutItem_Click(object sender, RoutedEventArgs e)
        {
            ModernMainWindow window = await((App)Application.Current).SafeCreateNewWindow<ModernMainWindow>();

            window.NavView_Navigate(typeof(ViewComponentSettingsPage), StoreId, new DrillInNavigationTransitionInfo());
        }
    }
}
