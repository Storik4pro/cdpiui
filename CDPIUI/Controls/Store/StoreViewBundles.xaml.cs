using CDPIUI.Core.Store.ViewModels;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CDPIUI.Controls.Store
{
    public sealed class StoreViewBundleItem
    {
        public string KitId { get; set; }
        public ImageSource CardImageSource { get; set; }
        public string CardTitle { get; set; }
        public string CardSubtitle { get; set; }
        public Brush CardBackgroundBrush { get; set; }
        public List<ViewStoreItemModel> Items { get; set; }  
    }

    public sealed partial class StoreViewBundles : UserControl
    {
        private const int MaximumBundleCount = 4;

        public ObservableCollection<StoreViewBundleItem> Bundles { get; } = [];

        public StoreBundlesLayoutMode BundleLayoutMode
        {
            get => (StoreBundlesLayoutMode)GetValue(BundleLayoutModeProperty);
            set => SetValue(BundleLayoutModeProperty, value);
        }

        public static readonly DependencyProperty BundleLayoutModeProperty = DependencyProperty.Register(
            nameof(BundleLayoutMode),
            typeof(StoreBundlesLayoutMode),
            typeof(StoreViewBundles),
            new PropertyMetadata(StoreBundlesLayoutMode.Wide));

        public event Action<StoreReadyKitButton> BundleClick;

        public StoreViewBundles() => InitializeComponent();

        public void SetBundles(IEnumerable<StoreViewBundleItem> bundles)
        {
            List<StoreViewBundleItem> items = (bundles ?? [])
                .Take(MaximumBundleCount)
                .ToList();

            Bundles.Clear();
            foreach (StoreViewBundleItem item in items)
                Bundles.Add(item);

            Visibility = Bundles.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public void ClearBundles()
        {
            Bundles.Clear();
            Visibility = Visibility.Collapsed;
        }

        private void BundleButton_Click(StoreReadyKitButton button) => BundleClick?.Invoke(button);
    }
}
