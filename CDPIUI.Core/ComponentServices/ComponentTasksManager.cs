using CDPIUI.Core.Communication;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.ComponentsTask;
using System.Diagnostics;

namespace CDPIUI.Core.ComponentServices
{
    public class ComponentTasksManager : TasksManageService<ProcessService>
    {
        private static ComponentTasksManager? _instance;
        private static readonly object _lock = new object();

        public static ComponentTasksManager Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new ComponentTasksManager();
                    return _instance;
                }
            }
        }

        private ComponentTasksManager()
        {
            UpdateTaskList();

            ComponentItemsLoaderHelper.Instance.InitRequested += SendTasksData;
            StoreHelper.Instance.ItemActionsStopped += (id) => RequestComponentItemsInit();
            StoreHelper.Instance.ItemRemoved += (id) => RequestComponentItemsInit();
        }

        public void UpdateTaskList()
        {
            var _tasks = Tasks;
            StopAllTasks();
            foreach (var item in DatabaseHelper.Instance.GetItemsByType("component"))
            {
                AddNewTask(item.Id!);
                if (_tasks.FirstOrDefault(x => x.Id == item.Id)?.ProcessManager?.IsProcessRunning ?? false)
                    CreateAndRunNewTask(item.Id!);
            }
        }

        public void RequestComponentItemsInit()
        {
            ComponentItemsLoaderHelper.Instance.Init();
        }

        public async Task SendTaskData(string id)
        {
            var task = await GetTaskFromId(id);
            if (task != null && DatabaseHelper.Instance.IsItemInstalled(task.Id))
            {
                ComponentHelper componentHelper =
                    ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(task.Id);

                try
                {
                    if (componentHelper != null && string.IsNullOrEmpty(componentHelper.GetStartupParams()))
                    {
                        await PipeHelper.SendSettingsPacket(
                            Shared.Pipe.Models.SettingsMessageIds.ComponentSetupNotFinished,
                            new() { { "componentId", task.Id } });
                        return;
                    }
                    await PipeHelper.SendSettingsPacket(
                            Shared.Pipe.Models.SettingsMessageIds.ComponentSetupFinished,
                            new() { { "componentId", task.Id } });
                }
                catch
                {
                    await PipeHelper.SendSettingsPacket(
                            Shared.Pipe.Models.SettingsMessageIds.ComponentSetupNotFinished,
                            new() { { "componentId", task.Id } });
                }
            }
            else
            {
                await PipeHelper.SendSettingsPacket(
                            Shared.Pipe.Models.SettingsMessageIds.ComponentNotInstalled,
                            new() { { "componentId", task.Id } });
            }
        }

        public async void SendTasksData()
        {
            foreach (var task in Tasks)
            {
                await SendTaskData(task.Id);
            }
        }

        


        // TODO: Remove hardcoded Ids
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
            await ProcessService.StopService();
        }

        public async void RunAllPreferredActions()
        {
            foreach (var task in Tasks)
            {
                await task.ProcessManager?.RunActionsIfAutorunSelected();
            }

            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Contains("--exit-after-action")) Process.GetCurrentProcess().Kill();
        }

        [Obsolete("Not supported for UI")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override void CreateAndRunNewTask(string id, string executable, string args)
        {
            throw new NotImplementedException();
        }
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    }
}
