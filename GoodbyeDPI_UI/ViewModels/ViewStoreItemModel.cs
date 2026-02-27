using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CDPI_UI.ViewModels
{
    public class ViewStoreItemModel
    {
        public string StoreId { get; set; }
        public string Name { get; set; }
        public string Developer { get; set; }
        public string Color { get; set; }
        
        public ImageSource ImageSource { get; set; }

    }
}
