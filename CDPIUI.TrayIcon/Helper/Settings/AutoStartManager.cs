using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.TaskScheduler;

namespace CDPIUI_TrayIcon.Helper
{
    public static class AutoStartManager
    {
        private const string TaskName = "CDPIUI_Autostart";

        public static bool AddToAutorun()
        {
            try
            {
                string? executablePath = Environment.ProcessPath;
                if (executablePath == null)
                    return false;

                using (TaskService taskService = new TaskService())
                {
                    Microsoft.Win32.TaskScheduler.Task existingTask = taskService.GetTask(TaskName);
                    if (existingTask != null)
                    {
                        taskService.RootFolder.DeleteTask(TaskName);
                    }

                    TaskDefinition taskDefinition = taskService.NewTask();
                    taskDefinition.RegistrationInfo.Description = "Starting CDPI UI with highest rights when user logs in.";
                    taskDefinition.RegistrationInfo.Author = "CDPI UI Tray Icon";

                    LogonTrigger trigger = new LogonTrigger
                    {
                        UserId = System.Security.Principal.WindowsIdentity.GetCurrent().Name,
                    };
                    taskDefinition.Triggers.Add(trigger);

                    taskDefinition.Actions.Add(new ExecAction(executablePath, "--autorun", null));

                    taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;

                    taskDefinition.Principal.UserId = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;

                    taskService.RootFolder.RegisterTaskDefinition(TaskName, taskDefinition);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(AutoStartManager), $"Autorun error: {ex.Message}");
            }
            return false;
        }

        public static bool RemoveFromAutorun()
        {
            try
            {
                using (TaskService taskService = new TaskService())
                {
                    taskService.RootFolder.DeleteTask(TaskName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(AutoStartManager), $"Autorun error: {ex}");
            }
            return false;
        }
    }
}
