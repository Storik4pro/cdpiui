using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CDPIUI.Shared.ComponentsTask
{
    public static class OutputCleanupHelper
    {
        public static string ReplaceSymbols(string str)
        {
            str = str.Replace("[?25l\u001b[2J\u001b[m\u001b[H", "");
            str = str.Replace("[K", "");
            str = str.Replace("[4;1H", "\n");
            str = str.Replace("[12;1H", "\n");
            str = str.Replace("[32m", "\n");
            str = str.Replace("[90m", "\n");
            str = str.Replace("[23;1H", "\n\t");
            str = Regex.Replace(str, @"\u001b\]0;.*?\[\?25h", "");
            str = Regex.Replace(str, @"\[\?25l|\[1C|", "");
            str = Regex.Replace(str, @"\[\?\d{4}\w", "");
            str = Regex.Replace(str, @"\[\d[A-Z]", "");
            str = Regex.Replace(str, @"\[\d{1,2};\d{1,2}[A-Z]", "");
            str = Regex.Replace(str, @"\[\?\d{1,2}[a-z]", "");
            str = Regex.Replace(str, @"(\[\d{0,2}m)?(\[H)?", "");
            str = Regex.Replace(str, @"(\[\d{0,2}C);", "\t");
            str = str.Replace("]0;", "");
            return str;
        }
    }
}
