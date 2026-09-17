using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core.Store.Network.Models
{
    public class ReleaseInfoModel
    {
        public string? ReleaseTag { get; set; }
        public string? ReleaseNotes { get; set; }

        public static ReleaseInfoModel BasicReleaseInfo(string? tag, string? notes) => new() { ReleaseTag = tag, ReleaseNotes = notes };
    }
}
