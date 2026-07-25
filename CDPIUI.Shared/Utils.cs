using System;
using System.Diagnostics;
using System.Linq;

namespace CDPIUI.Shared
{
    public static class SharedUtils
    {
        public static string GenerateNewId()
        {
            return Guid.NewGuid().ToString().Replace("{", "").Replace("}", "");
        }

        public static bool IsOsSupportedNewGlyph()
        {
            Debug.WriteLine(Environment.OSVersion.ToString());
            var version1 = Environment.OSVersion.Version;
            string v2 = "10.0.22000.194";

            var version2 = new Version(v2);
            if (version1 >= version2) return true;
            return false;
        }

        static Random random = new();
        public static string GetRandomHexNumber(int digits)
        {
            byte[] buffer = new byte[digits / 2];
            random.NextBytes(buffer);
            string result = String.Concat(buffer.Select(x => x.ToString("X2")).ToArray());
            if (digits % 2 == 0)
                return result;
            return result + random.Next(16).ToString("X");
        }
    }
}
