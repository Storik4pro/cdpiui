using CDPIUI.Core.Store.Data;

namespace CDPIUI.Core.Proxy
{
    public enum ProxySetupTypes
    {
        None,
        AllSystem,
        ProxiFyre,
        NoActions,
        AsInConfig,
    }

    public static class ProxyHelper
    {
        public static List<string> Tokens = new() { "-p", "--port", "-i", "--ip", "-addr", "--host" };

        public static string ReplaseIp(string args)
        {
            string[] splittedArgs = args.Split(' ');
            string finalArgs = string.Empty;
            for (int i = 0; i < splittedArgs.Length; i++)
            {
                var spA = splittedArgs[i].Split("=");
                string token = spA[0];
                string value = spA.Length > 1 ? spA[1] + " " : string.Empty;
                if (Tokens.Contains(token))
                {
                    if (splittedArgs[i].Contains('='))
                    {
                        continue;
                    }
                    else
                    {
                        i++;
                        continue;
                    }
                }
                if (!string.IsNullOrEmpty(value))
                {
                    finalArgs += $"{token}={value}";
                }
                else
                {
                    finalArgs += $"{token} ";
                }

            }
            return finalArgs;
        }

        public static List<string> ProxyLikeComponents = [
            HardcodedItemIds.ComponentIds[Components.ByeDPI],
            HardcodedItemIds.ComponentIds[Components.NoDPI],
            HardcodedItemIds.ComponentIds[Components.SpoofDPI]
                ];

        internal static string ReplaceArgsForProxy(
            string args, 
            string ip, 
            string port, 
            string componentId)
        {
            string finalArgs = ReplaseIp(args);
            if (componentId == "CSSIXC048")
                finalArgs = $"-addr={ip} -port={port} " + finalArgs;
            else if (componentId == "CSNIG9025")
                finalArgs = $"--host={ip} --port={port} " + finalArgs;
            else
                finalArgs = $"--ip={ip} --port={port} " + finalArgs;
            return finalArgs;

        }
    }
}
