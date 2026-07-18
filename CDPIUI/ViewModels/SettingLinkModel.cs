using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CDPI_UI.ViewModels
{
    public enum LinkedActions
    {
        CreateNewConfigForComponent,
        EditCurrentConfig,

        // TgWsProxy only
        OpenProxyInTelegram,

    }

    public class SettingLinkModel
    {
        public string DisplayName { get; set; }
        public LinkedActions Action {  get; set; }
    }
}
