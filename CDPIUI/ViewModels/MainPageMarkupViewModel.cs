using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.ViewModels
{
    public enum MarkupTypes
    {
        Classic,
        Modern
    }
    public class MainPageMarkupViewModel
    {
        public string Name { get; set; }
        
        public MarkupTypes Type { get; set; }

        public ImageSource ImageSource { get; set; }
    }
}
