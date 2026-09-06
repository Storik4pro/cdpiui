using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace CDPIUI.Helper
{
    internal static class ReadyKitBrushFactory
    {
        public static Brush Create(IEnumerable<string> colors, string fallbackColor = null)
        {
            List<Color> parsedColors = colors
                .Append(fallbackColor)
                .Where(color => !string.IsNullOrWhiteSpace(color))
                .Select(TryParseColor)
                .Where(color => color.HasValue)
                .Select(color => color!.Value)
                .Distinct()
                .ToList();

            if (parsedColors.Count == 0)
                parsedColors.Add(Color.FromArgb(255, 55, 78, 140));

            if (parsedColors.Count == 1)
                parsedColors.Add(CreateSecondShade(parsedColors[0]));

            LinearGradientBrush brush = new()
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };

            for (int index = 0; index < parsedColors.Count; index++)
            {
                brush.GradientStops.Add(new GradientStop
                {
                    Color = parsedColors[index],
                    Offset = (double)index / (parsedColors.Count - 1)
                });
            }

            return brush;
        }

        private static Color? TryParseColor(string value)
        {
            try
            {
                return UIHelper.HexToColorConverter(value!);
            }
            catch
            {
                return null;
            }
        }

        private static Color CreateSecondShade(Color color)
        {
            double luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
            double multiplier = luminance < 95 ? 1.45 : 0.55;

            return Color.FromArgb(
                color.A,
                (byte)Math.Clamp(color.R * multiplier, 0, 255),
                (byte)Math.Clamp(color.G * multiplier, 0, 255),
                (byte)Math.Clamp(color.B * multiplier, 0, 255));
        }
    }
}
