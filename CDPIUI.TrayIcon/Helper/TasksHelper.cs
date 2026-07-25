using CDPIUI.Shared.ComponentsTask;

namespace CDPIUI.TrayIcon.Helper
{
    public class TasksHelper : TasksManageService<ProcessManager>
    {
        private static TasksHelper? _instance;
        private static readonly object _lock = new object();

        public static TasksHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new TasksHelper();
                    return _instance;
                }
            }
        }

        public TasksHelper() { }

        public void SendAllTasksOutput()
        {
            foreach (var task in Tasks)
            {
                task.ProcessManager.SendDefaultProcessOutput();
            }
        }

        public void SendAllTasksState()
        {
            foreach (var task in Tasks)
            {
                task.ProcessManager.SendState();
                task.ProcessManager.SendNowSelectedComponentName();
            }
        }

        public async void SetIsStartArgsChangedProperty(string id, bool value)
        {
            var task = await GetTaskFromId(id);
            if (task == null) return;

            task.ProcessManager.IsProcessInfoChanged = value;
        }

        public async void EnableProxyOnTask(string id, string _proxyType, string ip, string port)
        {
            var existTask = await GetTaskFromId(id);

            foreach (var task in Tasks)
            {
                if (existTask != task && (bool)(task.ProcessManager?.IsProxyEnabled() ?? false))
                {
                    await task.ProcessManager.StopProcess();
                }
            }
            
            existTask?.ProcessManager.StartProxy(_proxyType, ip, port);
        }

        public async void InitProxyOnTask(string id, string proxiFyrePath)
        {
            var existTask = await GetTaskFromId(id);
            existTask?.ProcessManager.InitProxy(proxiFyrePath);
        }

        public async void CleanProxyOnTask(string id)
        {
            var existTask = await GetTaskFromId(id);
            existTask?.ProcessManager.CleanProxy();
        }


        List<string> serviceUsedComponentsIds = ["CSGIVS036", "CSZTBN012"];

        public async void StopService()
        {
            foreach (var task in Tasks)
            {
                if (serviceUsedComponentsIds.Contains(task.Id))
                {
                    await task.ProcessManager.StopProcess();
                }
            }
            await ProcessManager.StopService();
        }
        
    }
}
