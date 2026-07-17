using CDPI_UI.Helper.Static;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.ViewModels
{
    public class ColorViewModel
    {
        public string Hex { get; set; }
        public Brush Brush { get => UIHelper.HexToSolidColorBrushConverter(Hex); }

        public string DisplayName { get; set; }

        public bool IsAccentColor { get; }

        public ColorViewModel(string hex, bool isAccentColor = false)
        {
            IsAccentColor = isAccentColor;
            Hex = hex;
        }

        public override string ToString() => DisplayName;
    }
}
