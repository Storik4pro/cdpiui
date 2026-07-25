using System;
using System.Collections.Generic;
using WinUI3Localizer;

namespace CDPIUI.Helper.Parsers
{
    internal static class UnitsParser
    {
        public static string FormatSize(long sizeInBytes)
        {
            List<string> suffixes = [];

            ILocalizer localizer = Localizer.Get();

            suffixes.Add(localizer.GetLocalizedString("/UIHelper/Bytes"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/KiloBytes"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/MegaBytes"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/GB"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/TB"));

            int order = sizeInBytes > 0
                ? Math.Min((int)Math.Floor(Math.Log(sizeInBytes, 1024)), suffixes.Count - 1)
                : 0;

            double adjustedSize = sizeInBytes / Math.Pow(1024, order);

            return $"{adjustedSize:0.#} {suffixes[order]}";
        }

        public static string FormatSpeed(double speedInBytes)
        {
            List<string> suffixes = [];

            ILocalizer localizer = Localizer.Get();

            suffixes.Add(localizer.GetLocalizedString("/UIHelper/BytesPs"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/KiloBytesPs"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/MegaBytesPs"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/GBPs"));
            suffixes.Add(localizer.GetLocalizedString("/UIHelper/TBPs"));

            int order = speedInBytes > 0
                ? Math.Min((int)Math.Floor(Math.Log(speedInBytes, 1024)), suffixes.Count - 1)
                : 0;

            double adjustedSpeed = speedInBytes / Math.Pow(1024, order);

            return $"{adjustedSpeed:0.##} {suffixes[order]}";
        }

        public static string ConvertMinutesToPrettyText(double min)
        {
            ILocalizer localizer = Localizer.Get();
            if (min > 60)
            {
                double hours = min / 60;
                if (hours < 1.5)
                    return localizer.GetLocalizedString("/UIHelper/Hour");
                else if (hours >= 1.5 && hours <= 2.5)
                    return localizer.GetLocalizedString("/UIHelper/TwoHour");
                else
                    return localizer.GetLocalizedString("/UIHelper/MoreThanThreeHours");
            }
            else
            {
                if (min > 1)
                    return $"{min:F0} {localizer.GetLocalizedString("/UIHelper/Min")}";
                else if (min == 1)
                    return localizer.GetLocalizedString("/UIHelper/OneMinute");
                else
                    return localizer.GetLocalizedString("/UIHelper/Sec");
            }
        }
    }
}
