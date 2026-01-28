using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper
{
    public static class UrlOpenHelper
    {
        public const string MainRepoUrl = "https://github.com/Storik4pro/cdpiui";
        public const string LicenseUrl = "https://github.com/Storik4pro/cdpiui/blob/main/LICENSE.txt";
        public static async void LaunchUrl(string uri)
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(uri));
        }
        public static async void LaunchReportUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Storik4pro/cdpiui/issues"));
        }
        public static async void LaunchMainRepoUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(MainRepoUrl));
        }
        public static async void LaunchWikiUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://storik4pro.github.io/cdpiui/"));
        }
        public static async void LaunchLicenseUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(LicenseUrl));
        }
        public static async void LaunchDonateUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://pay.cloudtips.ru/p/5bb7ff74"));
        }
        public static async void LaunchTelegramUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://t.me/storik4dev"));
        }
        public static async void LaunchComponentForumUrl(string componentId)
        {
            switch (componentId)
            {
                case "CSZTBN012":
                    _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://ntc.party/c/community-software/zapret-antidpi/20"));
                    break;
                case "CSGIVS036":
                    _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://ntc.party/c/community-software/goodbyedpi/8"));
                    break;
                case "CSBIHA024":
                    _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://ntc.party/c/community-software/byedpi/39"));
                    break;
                case "CSSIXC048":
                    break;

            }
        }
    }
}
