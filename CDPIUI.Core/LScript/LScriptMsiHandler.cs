using CDPIUI.Core.Basic;
using CDPIUI.Core.Store.MSI;
using CDPIUI.Shared.Exceptions;
using System.Diagnostics;
using static CDPIUI.Core.Store.MSI.MsiInstallerService;

namespace CDPIUI.Core.LScript
{
    public class LScriptMsiHandler()
    {
        public static async void InstallMsi(
            string[] scriptArgs, Dictionary<string, string>? extraArgs, CancellationToken cancellationToken)
        {
            string path = Path.Combine(
                extraArgs?.GetValueOrDefault("CurrentDirectory", string.Empty) ?? string.Empty, 
                scriptArgs[0]);
            if (bool.TryParse(scriptArgs[1], out bool removeAfterAction))
            {
                Debug.WriteLine(path);
                if (path.EndsWith("$ALL"))
                {
                    var _result = await InstallAllMsiFromPath(path.Replace("$ALL", ""), removeAfterAction, cancellationToken);
                    if (!_result.Item1) throw new MsiInstallException("Install failure");
                    if (_result.Item2)
                    {
                        // TODO: ask restart
                    }
                }
                else
                {
                    var _result = await InstallMsi(path, removeAfterAction, cancellationToken);
                    if (!_result.Item1) throw new MsiInstallException("Install failure");
                    if (_result.Item2)
                    {
                        // TODO: ask restart
                    }
                }
            }
            else
            {
                throw new MsiInstallException("Argument is null");
            }
        }

        private static async Task<Tuple<bool, bool>> InstallAllMsiFromPath(string directory, bool removeAfterAction, CancellationToken cancellationToken)
        {
            bool requestRestart = false;
            foreach (string filepath in Directory.EnumerateFiles(directory))
            {
                var result = await InstallMsi(filepath, removeAfterAction, cancellationToken);
                if (!result.Item1) return Tuple.Create(false, false);

                if (result.Item2) requestRestart = result.Item2;

            }
            return Tuple.Create(true, requestRestart);
        }

        private static async Task<Tuple<bool, bool>> InstallMsi(string filepath, bool removeAfterAction, CancellationToken cancellationToken)
        {
            bool success = true;
            bool isRestartNeeded = false;

            string msiPath = Path.Combine(filepath);
            string msiGUID = Guid.NewGuid().ToString();
            MsiInstallerService msiInstallerHelper = new(msiGUID, msiPath);
            msiInstallerHelper.callbackAction += HandleMsiInstallerMessage;
            MsiCallback callback = await msiInstallerHelper.Run(cancellationToken);
            msiInstallerHelper.callbackAction -= HandleMsiInstallerMessage;

            Logger.Instance.CreateDebugLog(nameof(MsiInstallerService), "TRY");

            if (callback.State == MsiState.ExceptionHappens)
            {
                success = false;
                throw new MsiInstallException("MSI_UNKNOWN");
            }
            else if (callback.State == MsiState.CompleteRestartRequest)
            {
                isRestartNeeded = true;
            }

            if (removeAfterAction)
            {
                File.Delete(msiPath);
            }

            return Tuple.Create(success, isRestartNeeded);

        }

        private static void HandleMsiInstallerMessage(MsiCallback callback)
        {
            Logger.Instance.CreateDebugLog(nameof(LScriptCore), $"MSI installing callback: {callback}");
        }
    }
}
