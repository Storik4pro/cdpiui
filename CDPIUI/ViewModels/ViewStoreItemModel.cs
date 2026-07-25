using CDPIUI.Core.Static;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.UI;

namespace CDPIUI.ViewModels
{
    public class ViewStoreItemModel
    {
        public string StoreId { get; set; }
        public string Name { get; set; }
        public string Developer { get; set; }
        public string ColorHEX { get; set; }
        public Brush ColorBrush
        {
            get => UIHelper.HexToSolidColorBrushConverter(ColorHEX);
        }
        
        public ImageSource ImageSource { get; set; }

    }
}
