using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Core
{
    public static class State
    {
#if SINGLEFILE
        public static bool IsApplicationBuildAsSingleFile = true;
        public static bool IsApplicationBuildAsMsi = false;
#elif MSIFILE
        public static bool IsApplicationBuildAsSingleFile = false;
        public static bool IsApplicationBuildAsMsi = true;
#elif Release
        public static bool IsApplicationBuildAsSingleFile = true;
        public static bool IsApplicationBuildAsMsi = false;
#else
        public static bool IsApplicationBuildAsSingleFile = false;
        public static bool IsApplicationBuildAsMsi = false;
#endif
    }
}
