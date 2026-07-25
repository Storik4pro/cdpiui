using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core.Store.Network.Models
{
    internal class APILinkModel : ILinkModel
    {
        public string? link { get; set; }
        public string? version { get; set; }
    }
}
