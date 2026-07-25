using System;

namespace CDPIUI.Helper.Static
{
    public static partial class Utils
    {
        static Utils()
        {

        }

        public static string GetValueFromCommmandLineParameter(string commmandLineParameter)
        {
            string[] arguments = Environment.GetCommandLineArgs();

            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].StartsWith(commmandLineParameter))
                {
                    string[] parValPair = arguments[i].Split('=');
                    if (parValPair.Length > 1)
                    {
                        return parValPair[1];
                    }
                    else
                    {
                        if (arguments.Length >= i + 1 && !arguments[i + 1].StartsWith("--"))
                        {
                            return arguments[i + 1];
                        }
                    }
                }
            }
            return string.Empty;
        }        
    }
}