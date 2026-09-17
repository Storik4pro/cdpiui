using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Foundation;

namespace CDPIUI.Controls.Universal
{
    /// <summary>
    /// Keeps the layout host at a stable width and arranges visible children from
    /// the right edge. A collapsed child is not rearranged, so its implicit hide
    /// animation remains at the last visible position.
    /// </summary>
    public sealed class RightAlignedStackLayout : NonVirtualizingLayout
    {
        public double Spacing { get; set; }

        protected override Size MeasureOverride(
            NonVirtualizingLayoutContext context,
            Size availableSize)
        {
            double contentWidth = 0;
            double contentHeight = 0;
            int visibleElementCount = 0;

            foreach (UIElement element in context.Children)
            {
                element.Measure(new Size(double.PositiveInfinity, availableSize.Height));

                if (element.Visibility == Visibility.Collapsed)
                    continue;

                contentWidth += element.DesiredSize.Width;
                contentHeight = Math.Max(contentHeight, element.DesiredSize.Height);
                visibleElementCount++;
            }

            if (visibleElementCount > 1)
                contentWidth += Spacing * (visibleElementCount - 1);

            double desiredWidth = double.IsInfinity(availableSize.Width)
                ? contentWidth
                : availableSize.Width;

            return new Size(desiredWidth, contentHeight);
        }

        protected override Size ArrangeOverride(
            NonVirtualizingLayoutContext context,
            Size finalSize)
        {
            double horizontalOffset = finalSize.Width;
            bool hasElementToTheRight = false;

            for (int index = context.Children.Count - 1; index >= 0; index--)
            {
                UIElement element = context.Children[index];

                if (element.Visibility == Visibility.Collapsed)
                    continue;

                if (hasElementToTheRight)
                    horizontalOffset -= Spacing;

                horizontalOffset -= element.DesiredSize.Width;
                double verticalOffset = Math.Max(
                    0,
                    (finalSize.Height - element.DesiredSize.Height) / 2);

                element.Arrange(new Rect(
                    horizontalOffset,
                    verticalOffset,
                    element.DesiredSize.Width,
                    element.DesiredSize.Height));

                hasElementToTheRight = true;
            }

            return finalSize;
        }
    }
}
