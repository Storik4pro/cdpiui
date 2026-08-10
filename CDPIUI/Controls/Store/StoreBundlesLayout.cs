using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Foundation;

namespace CDPIUI.Controls.Store
{
    public enum StoreBundlesLayoutMode
    {
        Compact,
        Medium,
        Wide
    }

    public sealed class StoreBundlesLayout : NonVirtualizingLayout
    {
        public StoreBundlesLayoutMode LayoutMode
        {
            get => (StoreBundlesLayoutMode)GetValue(LayoutModeProperty);
            set => SetValue(LayoutModeProperty, value);
        }

        public static readonly DependencyProperty LayoutModeProperty = DependencyProperty.Register(
            nameof(LayoutMode),
            typeof(StoreBundlesLayoutMode),
            typeof(StoreBundlesLayout),
            new PropertyMetadata(StoreBundlesLayoutMode.Wide, OnLayoutPropertyChanged));

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
            nameof(ItemHeight),
            typeof(double),
            typeof(StoreBundlesLayout),
            new PropertyMetadata(110d, OnLayoutPropertyChanged));

        public double MinimumItemWidth
        {
            get => (double)GetValue(MinimumItemWidthProperty);
            set => SetValue(MinimumItemWidthProperty, value);
        }

        public static readonly DependencyProperty MinimumItemWidthProperty = DependencyProperty.Register(
            nameof(MinimumItemWidth),
            typeof(double),
            typeof(StoreBundlesLayout),
            new PropertyMetadata(260d, OnLayoutPropertyChanged));

        public double ColumnSpacing
        {
            get => (double)GetValue(ColumnSpacingProperty);
            set => SetValue(ColumnSpacingProperty, value);
        }

        public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
            nameof(ColumnSpacing),
            typeof(double),
            typeof(StoreBundlesLayout),
            new PropertyMetadata(10d, OnLayoutPropertyChanged));

        public double RowSpacing
        {
            get => (double)GetValue(RowSpacingProperty);
            set => SetValue(RowSpacingProperty, value);
        }

        public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
            nameof(RowSpacing),
            typeof(double),
            typeof(StoreBundlesLayout),
            new PropertyMetadata(10d, OnLayoutPropertyChanged));

        public double LargeColumnWeight
        {
            get => (double)GetValue(LargeColumnWeightProperty);
            set => SetValue(LargeColumnWeightProperty, value);
        }

        public static readonly DependencyProperty LargeColumnWeightProperty = DependencyProperty.Register(
            nameof(LargeColumnWeight),
            typeof(double),
            typeof(StoreBundlesLayout),
            new PropertyMetadata(7d, OnLayoutPropertyChanged));

        public double SmallColumnWeight
        {
            get => (double)GetValue(SmallColumnWeightProperty);
            set => SetValue(SmallColumnWeightProperty, value);
        }

        public static readonly DependencyProperty SmallColumnWeightProperty = DependencyProperty.Register(
            nameof(SmallColumnWeight),
            typeof(double),
            typeof(StoreBundlesLayout),
            new PropertyMetadata(5d, OnLayoutPropertyChanged));

        public double BasicColumnWeight
        {
            get => (double)GetValue(BasicColumnWeightProperty);
            set => SetValue(BasicColumnWeightProperty, value);
        }

        public static readonly DependencyProperty BasicColumnWeightProperty = DependencyProperty.Register(
            nameof(BasicColumnWeight),
            typeof(double),
            typeof(StoreBundlesLayout),
            new PropertyMetadata(1d, OnLayoutPropertyChanged));

        protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
        {
            int itemCount = context.Children.Count;
            Size layoutSize = GetLayoutSize(itemCount, availableSize.Width);

            for (int index = 0; index < context.Children.Count; index++)
            {
                UIElement child = context.Children[index];
                Rect bounds = GetItemBounds(index, itemCount, layoutSize);
                bool isVisible = !bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0;

                child.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                child.Measure(isVisible
                    ? new Size(bounds.Width, bounds.Height)
                    : new Size(0, 0));
            }

            return layoutSize;
        }

        protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
        {
            int itemCount = context.Children.Count;
            Size layoutSize = GetLayoutSize(itemCount, finalSize.Width);

            for (int index = 0; index < context.Children.Count; index++)
            {
                Rect bounds = GetItemBounds(index, itemCount, layoutSize);
                context.Children[index].Arrange(bounds.IsEmpty
                    ? new Rect(0, 0, 0, 0)
                    : bounds);
            }

            return layoutSize;
        }

        private Size GetLayoutSize(int itemCount, double availableWidth)
        {
            if (itemCount == 0)
                return new Size(0, 0);

            double width = double.IsFinite(availableWidth)
                ? Math.Max(0, availableWidth)
                : GetMinimumLayoutWidth(itemCount);

            return new Size(width, ItemHeight * 2 + RowSpacing);
        }

        private double GetMinimumLayoutWidth(int itemCount)
        {
            int columnCount = LayoutMode switch
            {
                StoreBundlesLayoutMode.Compact => 1,
                StoreBundlesLayoutMode.Medium => itemCount > 1 ? 2 : 1,
                _ => itemCount switch
                {
                    >= 4 => 3,
                    >= 2 => 2,
                    _ => 1
                }
            };

            return MinimumItemWidth * columnCount + ColumnSpacing * (columnCount - 1);
        }

        private Rect GetItemBounds(int index, int itemCount, Size layoutSize)
        {
            if (index < 0 || index >= itemCount)
                return Rect.Empty;

            return LayoutMode switch
            {
                StoreBundlesLayoutMode.Compact => GetCompactBounds(index, itemCount, layoutSize),
                StoreBundlesLayoutMode.Medium => GetMediumBounds(index, itemCount, layoutSize),
                _ => GetWideBounds(index, itemCount, layoutSize)
            };
        }

        private Rect GetCompactBounds(int index, int itemCount, Size layoutSize)
        {
            if (index == 0)
            {
                double height = itemCount == 1 ? layoutSize.Height : ItemHeight;
                return new Rect(0, 0, layoutSize.Width, height);
            }

            if (index == itemCount - 1)
                return new Rect(0, ItemHeight + RowSpacing, layoutSize.Width, ItemHeight);

            return Rect.Empty;
        }

        private Rect GetMediumBounds(int index, int itemCount, Size layoutSize)
        {
            if (itemCount == 1)
                return index == 0 ? new Rect(0, 0, layoutSize.Width, layoutSize.Height) : Rect.Empty;

            (double largeWidth, double basicWidth) = SplitColumns(
                layoutSize.Width,
                LargeColumnWeight,
                SmallColumnWeight);

            if (index == 0)
                return new Rect(0, 0, largeWidth, layoutSize.Height);

            if (index == itemCount - 1)
                return new Rect(largeWidth + ColumnSpacing, 0, basicWidth, layoutSize.Height);

            return Rect.Empty;
        }

        private Rect GetWideBounds(int index, int itemCount, Size layoutSize)
        {
            if (itemCount == 1)
                return index == 0 ? new Rect(0, 0, layoutSize.Width, layoutSize.Height) : Rect.Empty;

            if (itemCount <= 3)
            {
                (double leftWidth, double rightWidth) = SplitColumns(
                    layoutSize.Width,
                    LargeColumnWeight,
                    SmallColumnWeight);

                if (index == 0)
                    return new Rect(0, 0, leftWidth, layoutSize.Height);

                if (itemCount == 2 && index == 1)
                    return new Rect(leftWidth + ColumnSpacing, 0, rightWidth, layoutSize.Height);

                if (itemCount == 3 && index is 1 or 2)
                {
                    double y = index == 1 ? 0 : ItemHeight + RowSpacing;
                    return new Rect(leftWidth + ColumnSpacing, y, rightWidth, ItemHeight);
                }

                return Rect.Empty;
            }

            double usableWidth = Math.Max(0, layoutSize.Width - ColumnSpacing * 2);
            double totalWeight = Positive(LargeColumnWeight) + Positive(SmallColumnWeight) + Positive(BasicColumnWeight);
            double proportionalBasicWidth = usableWidth * Positive(BasicColumnWeight) / totalWeight;
            double maximumBasicWidth = Math.Max(0, usableWidth - MinimumItemWidth * 2);
            double basicWidth = Math.Min(maximumBasicWidth, Math.Max(MinimumItemWidth, proportionalBasicWidth));
            double sharedWidth = Math.Max(0, usableWidth - basicWidth);
            double sharedWeight = Positive(LargeColumnWeight) + Positive(SmallColumnWeight);
            double largeWidth = sharedWidth * Positive(LargeColumnWeight) / sharedWeight;
            double smallWidth = Math.Max(0, sharedWidth - largeWidth);
            double smallX = largeWidth + ColumnSpacing;
            double basicX = smallX + smallWidth + ColumnSpacing;

            return index switch
            {
                0 => new Rect(0, 0, largeWidth, layoutSize.Height),
                1 => new Rect(smallX, 0, smallWidth, ItemHeight),
                2 => new Rect(smallX, ItemHeight + RowSpacing, smallWidth, ItemHeight),
                3 => new Rect(basicX, 0, basicWidth, layoutSize.Height),
                _ => Rect.Empty
            };
        }

        private (double First, double Second) SplitColumns(double totalWidth, double firstWeight, double secondWeight)
        {
            double usableWidth = Math.Max(0, totalWidth - ColumnSpacing);
            double weight = Positive(firstWeight) + Positive(secondWeight);
            double firstWidth = usableWidth * Positive(firstWeight) / weight;

            if (usableWidth >= MinimumItemWidth * 2)
                firstWidth = Math.Clamp(firstWidth, MinimumItemWidth, usableWidth - MinimumItemWidth);

            return (firstWidth, Math.Max(0, usableWidth - firstWidth));
        }

        private static double Positive(double value) => Math.Max(value, double.Epsilon);

        private static void OnLayoutPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
            ((StoreBundlesLayout)sender).InvalidateMeasure();
    }
}
