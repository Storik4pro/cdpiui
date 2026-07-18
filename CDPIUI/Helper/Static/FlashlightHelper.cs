using CDPIUI.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.Helper.Static
{
    public static class FlashlightHelper
    {
        private const int TipsCount = 14;

        private const int UnluckyDay = 8;
        private const int UnluckyMonth = 2;

        private static readonly List<int> RareTipsIds = [3, 4, 6, 10];

        private static bool AddRareTip()
        {
            if (Random.Shared.Next(13) == 4) return true;
            return false;
        }

        public static bool LoadFlashlightTips(List<string> collection)
        {
            ILocalizer localizer = Localizer.Get();

            if (DateTime.Now.Day == UnluckyDay && DateTime.Now.Month == UnluckyMonth) return false;

            for (int i = 1; i <= TipsCount; i++)
            {
                if (RareTipsIds.Contains(i) && !AddRareTip()) continue;

                string tip = localizer.GetLocalizedString($"/Flashlight/Tip_{i}");
                if (string.IsNullOrEmpty(tip)) return false;
                collection.Add(tip);
            }
            collection.Shuffle();
            return true;
        }
    }
}
