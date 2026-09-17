using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.ViewModels
{
    public enum AvailableConfigCreationActios
    {
        ViewInStore,
        CreateManually,
        CreateAutomatically,
        CreateFromTemplate,
        ImportFromFile,
    }
    public class AvailableConfigCreationActionModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Uri IconUri { get; set; }

        public string ActionIconGlyph { get; set; }
        public AvailableConfigCreationActios Action { get; set; }
    }
}
