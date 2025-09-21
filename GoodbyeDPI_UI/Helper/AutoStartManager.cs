using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.TaskScheduler;

namespace CDPI_UI.Helper
{
    public static class AutoStartManager
    {
        public static void AddToAutorun()
        {
            try
            {
                _ = PipeClient.Instance.SendMessage("SETTINGS:ADD_TO_AUTORUN");

                SettingsManager.Instance.SetValue<bool>("SYSTEM", "autorun", true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Autorun error: {ex.Message}");
            }
        }

        public static void RemoveFromAutorun()
        {
            try
            {
                _ = PipeClient.Instance.SendMessage("SETTINGS:REMOVE_FROM_AUTORUN");


                SettingsManager.Instance.SetValue<bool>("SYSTEM", "autorun", false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Autorun error: {ex.Message}");
            }
        }
    }
}
