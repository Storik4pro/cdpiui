using CDPIUI.Core.Communication;
using CDPIUI.Core.Store.Data;
using System.Diagnostics;

namespace CDPIUI.Core.Features
{
    public static class ApplicationAutorunManager
    {
        /// <summary>
        /// Add application to autorun
        /// </summary>
        public static void AddToAutorun()
        {
            try
            {
                _ = PipeHelper.SendSettingsPacket(
                    Shared.Pipe.Models.SettingsMessageIds.AddToAutorun);

                SettingsManager.Instance.SetValue("SYSTEM", "autorun", true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Autorun error: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove application from autorun
        /// </summary>
        public static void RemoveFromAutorun()
        {
            try
            {
                _ = PipeHelper.SendSettingsPacket(
                    Shared.Pipe.Models.SettingsMessageIds.RemoveFromAutorun);


                SettingsManager.Instance.SetValue("SYSTEM", "autorun", false);

                foreach (var id in HardcodedItemIds.ComponentIds.Values)
                {
                    if (SettingsManager.Instance.GetValue<bool>(["CONFIGS", id], "usedForAutorun"))
                    {
                        SettingsManager.Instance.SetValue(["CONFIGS", id], "usedForAutorun", false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Autorun error: {ex.Message}");
            }
        }
    }
}
