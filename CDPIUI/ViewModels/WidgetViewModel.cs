using CDPIUI.Default;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.ViewModels
{
    public class WidgetViewModel
    {
        public Guid Id { get; set; }
        public WidgetType Type { get; set; }
        public object ActionObject { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public Uri UriImageSource { get; set; }
        public bool ShowAsMonochrome { get; set; } = false;


        public bool ShowOpenInNewWindowBadge { get; set; } = false;
    }

    public enum WidgetType
    {
        OpenWindow,
        OpenDialog,
        LaunchUrl,
        NavigateToPage,
    }
}
