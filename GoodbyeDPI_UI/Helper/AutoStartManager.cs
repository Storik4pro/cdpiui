using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.TaskScheduler;

namespace GoodbyeDPI_UI.Helper
{
    public class AutoStartManager
    {
        private const string TaskName = "CDPIUI_Autostart";

        public void AddToAutorun()
        {
            try
            {
                string executablePath = Process.GetCurrentProcess().MainModule.FileName;

                using (TaskService taskService = new TaskService())
                {
                    Microsoft.Win32.TaskScheduler.Task existingTask = taskService.GetTask(TaskName);
                    if (existingTask != null)
                    {
                        taskService.RootFolder.DeleteTask(TaskName);
                    }

                    TaskDefinition taskDefinition = taskService.NewTask();
                    taskDefinition.RegistrationInfo.Description = "Starting CDPI UI with highest rights when user logs in.";
                    taskDefinition.RegistrationInfo.Author = "goodbyeDPI.exe";

                    LogonTrigger trigger = new LogonTrigger
                    {
                        UserId = Environment.UserName 
                    };
                    taskDefinition.Triggers.Add(trigger);

                    taskDefinition.Actions.Add(new ExecAction(executablePath, "--autorun", null));

                    taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;

                    taskDefinition.Principal.UserId = Environment.UserName;
                    taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;

                    taskService.RootFolder.RegisterTaskDefinition(TaskName, taskDefinition);
                }

                SettingsManager.Instance.SetValue<bool>("SYSTEM", "autorun", true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Autorun error: {ex.Message}");
            }
        }

        public void RemoveFromAutorun()
        {
            try
            {
                using (TaskService taskService = new TaskService())
                {
                    taskService.RootFolder.DeleteTask(TaskName, false);
                }

                SettingsManager.Instance.SetValue<bool>("SYSTEM", "autorun", false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Autorun error: {ex.Message}");
            }
        }
    }
}
