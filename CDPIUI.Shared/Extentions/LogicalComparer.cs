using CDPIUI.Shared.Models;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CDPIUI.Shared.Extentions
{
    public class LogicalComparer : IComparer<INamedModel>
    {
        public int Compare(INamedModel x, INamedModel y)
        {
            return StrCmpLogicalW(x.name.Normalize(), y.name.Normalize());
        }
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string s1, string s2);
    }
}
