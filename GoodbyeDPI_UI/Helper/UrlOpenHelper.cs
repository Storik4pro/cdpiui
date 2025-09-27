using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.Helper
{
    public static class UrlOpenHelper
    {
        public static async void LaunchReportUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Storik4pro/cdpiui/issues"));
        }
        public static async void LaunchMainRepoUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Storik4pro/cdpiui"));
        }
        public static async void LaunchWikiUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://storik4pro.github.io/cdpiui/"));
        }
        public static async void LaunchLicenseUrl()
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Storik4pro/cdpiui/blob/main/LICENSE.txt"));
        }
    }
}
