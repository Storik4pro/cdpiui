using CDPI_UI.Helper.Static;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.ViewModels
{
    public class ThemeViewModel
    {
        public Guid Guid { get; set; }
        public ElementTheme FriendlyThemeId { get; set; } // Will be removed soon.

        public string Name { get; set; }
        public string Description { get; set; }

        public bool ShowDescription { get => !string.IsNullOrEmpty(Description); }

        public string FirstBackgroundColorHEX { get; set; } = "#000000";
        public Brush FirstBackgrounBrush { get => UIHelper.HexToSolidColorBrushConverter(FirstBackgroundColorHEX); }
        public string SecondBackgroundColorHEX { get; set; } = "#000000";
        public Brush SecondBackgrounBrush { get => UIHelper.HexToSolidColorBrushConverter(SecondBackgroundColorHEX); }

        public ImageSource ImageSource;
    }
}
