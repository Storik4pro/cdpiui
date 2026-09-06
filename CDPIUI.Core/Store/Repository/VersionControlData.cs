using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core.Store.Repository
{
    public enum SupportedVersionControls
    {
        None,
        GitHub,
        GitLab
    }

    internal class VersionControlData
    {
        public static Dictionary<SupportedVersionControls, string> VersionControlsLinks = new()
        {
            { SupportedVersionControls.GitHub, "https://api.github.com/repos/{0}/zipball/main" },
            { SupportedVersionControls.GitLab, "https://gitlab.com/{0}/-/archive/main/CDPIUI-Store-main.zip" },
        };


    }
}
