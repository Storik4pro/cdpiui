using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.ViewModels
{
    public class LanguageSelectModel
    {
        public string Id { get; set; }
        public string DisplayId { get => (string)new Converters.StringToUpperConverter().Convert(Id, null, null, null); }
        public string DisplayName { get; set; }
    }
}
