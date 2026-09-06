using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CDPIUI.ViewModels
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
