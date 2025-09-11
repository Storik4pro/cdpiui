using CDPI_UI.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

namespace CDPI_UI
{
    public sealed partial class StoreItemMicroButton : UserControl
    {
        private bool IsReady = false;
        public StoreItemMicroButton()
        {
            InitializeComponent();

        }

        public string StoreId
        {
            get { return (string)GetValue(StoreIdProperty); }
            set { 
                SetValue(StoreIdProperty, value);
                GetReady();
            }
        }

        public static readonly DependencyProperty StoreIdProperty =
            DependencyProperty.Register(
                nameof(StoreId), typeof(string), typeof(StoreItemMicroButton), new PropertyMetadata(string.Empty)
            );

        public ImageSource CardImageSource
        {
            get { return (ImageSource)GetValue(CardImageSourceProperty); }
            set { SetValue(CardImageSourceProperty, value); }
        }

        public static readonly DependencyProperty CardImageSourceProperty =
            DependencyProperty.Register(
                "ImageSource", typeof(ImageSource), typeof(StoreItemMicroButton), new PropertyMetadata(null)
            );

        public string CardTitle
        {
            get { return (string)GetValue(CardTitleProperty); }
            set { SetValue(CardTitleProperty, value); }
        }

        public static readonly DependencyProperty CardTitleProperty =
            DependencyProperty.Register(
                "Title", typeof(string), typeof(StoreItemMicroButton), new PropertyMetadata(string.Empty)
            );

        private async void GetReady()
        {
            IsReady = false;
            TextShimmer.Visibility = Visibility.Visible;
            ImageShimmer.Visibility = Visibility.Visible;
            PART_Image.Visibility = Visibility.Collapsed;
            TextGrid.Visibility = Visibility.Collapsed;

            if (string.IsNullOrEmpty(StoreId))
                return;

            if (DatabaseHelper.Instance.IsItemInstalled(StoreId))
            {
                InstallationFontIcon.Glyph = "\uE930";
            }
            else
            {
                InstallationFontIcon.Glyph = "\uEBD3";
            }
            await StoreHelper.Instance.LoadAllStoreDatabase(forseSync:false);

            if (StoreHelper.Instance.GetItemInfoFromStoreId(StoreId) == null)
            {
                PART_Button.IsEnabled = false;
            }

            TextShimmer.Visibility = Visibility.Collapsed;
            ImageShimmer.Visibility = Visibility.Collapsed;
            PART_Image.Visibility = Visibility.Visible;
            TextGrid.Visibility = Visibility.Visible;
            IsReady = true;
            string operationId = StoreHelper.Instance.GetOperationIdFromItemId(StoreId);

            RemoveHandlers();
            ConnectHandlers();
        }

        private Action<Tuple<string, double>> _itemDownloadProgressChangedHandler;
        private Action<Tuple<string, string>> _itemDownloadStageChangedHandler;
        private Action<string> _itemActionsStoppedHandler;
        private Action<string> _itemRemovedHandler;

        private void ConnectHandlers()
        {
            _itemDownloadProgressChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                double progress = data.Item2;

                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                    return;

                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = progress;
            };
            StoreHelper.Instance.ItemDownloadProgressChanged += _itemDownloadProgressChangedHandler;
            _itemDownloadStageChangedHandler = (data) =>
            {
                string operationId = data.Item1;
                string stage = data.Item2;
                
                if (StoreHelper.Instance.GetItemIdFromOperationId(operationId) != StoreId)
                    return;

                ProgressBar.Visibility = Visibility.Visible;

                switch (stage)
                {
                    case "Downloading":
                        ProgressBar.IsIndeterminate = false;
                        break;
                    default:
                        ProgressBar.IsIndeterminate = true;
                        break;
                }
            };
            StoreHelper.Instance.ItemDownloadStageChanged += _itemDownloadStageChangedHandler;

            _itemActionsStoppedHandler = (id) =>
            {
                RemoveHandlers();
                GetReady();
            };
            StoreHelper.Instance.ItemActionsStopped += _itemActionsStoppedHandler;

            _itemRemovedHandler = (data) =>
            {
                if (data != StoreId)
                    return;
                RemoveHandlers();
                GetReady();
            };
            StoreHelper.Instance.ItemRemoved += _itemRemovedHandler;
        }

        private void RemoveHandlers()
        {
            ProgressBar.Visibility = Visibility.Collapsed;
            if (_itemDownloadStageChangedHandler != null)
                StoreHelper.Instance.ItemDownloadStageChanged -= _itemDownloadStageChangedHandler;

            if (_itemDownloadProgressChangedHandler != null)
                StoreHelper.Instance.ItemDownloadProgressChanged -= _itemDownloadProgressChangedHandler;

            if (_itemActionsStoppedHandler != null)
                StoreHelper.Instance.ItemActionsStopped -= _itemActionsStoppedHandler;

            if (_itemRemovedHandler != null)
                StoreHelper.Instance.ItemRemoved -= _itemRemovedHandler;
        }

        private void PART_Button_Click(object sender, RoutedEventArgs e)
        {
            if (IsReady)
            {
                if (DatabaseHelper.Instance.IsItemInstalled(StoreId) || StoreHelper.Instance.GetOperationIdFromItemId(StoreId) != null)
                    return;

                ConnectHandlers();
                StoreHelper.Instance.AddItemToQueue(StoreId, string.Empty);
            }
        }

        private void PART_Button_PointerEntered(object sender, PointerRoutedEventArgs e)
        {

        }

        private void PART_Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {

        }
    }
}
