using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CDPI_UI.Helper
{
    public class TasksHelper
    {
        public class TaskModel
        {
            public required string Id { get; set; }
            public required ProcessManager ProcessManager { get; set; }
        }

        private static TasksHelper _instance;
        private static readonly object _lock = new object();

        public static TasksHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new TasksHelper();
                    return _instance;
                }
            }
        }

        private TasksHelper()
        {
            UpdateTaskList();

            Items.ComponentItemsLoaderHelper.Instance.InitRequested += SendTasksData;
            StoreHelper.Instance.ItemActionsStopped += (id) => RequestComponentItemsInit();
            StoreHelper.Instance.ItemRemoved += (id) => RequestComponentItemsInit();
        }

        public void UpdateTaskList()
        {
            var _tasks = Tasks;
            StopAllTasks();
            foreach (var item in DatabaseHelper.Instance.GetItemsByType("component"))
            {
                AddNewTask(item.Id);
                if (_tasks.FirstOrDefault(x => x.Id == item.Id)?.ProcessManager?.processState ?? false)
                    CreateAndRunNewTask(item.Id);
            }
        }

        public List<TaskModel> Tasks { get; private set; } = [];

        public Action TaskListUpdated;
        public Action<Tuple<string, bool>> TaskStateUpdated;

        private readonly SemaphoreSlim _taskOperationLock = new SemaphoreSlim(1, 1);

        public void RequestComponentItemsInit()
        {
            Items.ComponentItemsLoaderHelper.Instance.Init();
        }

        public async Task SendTaskData(string id)
        {
            var task = await GetTaskFromId(id);
            if (task != null && DatabaseHelper.Instance.IsItemInstalled(task.Id))
            {
                Items.ComponentHelper componentHelper =
                    Items.ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(task.Id);

                try
                {
                    if (string.IsNullOrEmpty(componentHelper.GetStartupParams()))
                    {
                        await PipeClient.Instance.SendMessage($"SETTINGS:COMPONENT_SETUP_NOT_FINISHED({task.Id})");
                        return;
                    }
                    await PipeClient.Instance.SendMessage($"SETTINGS:COMPONENT_READY({task.Id})");
                }
                catch
                {
                    await PipeClient.Instance.SendMessage($"SETTINGS:COMPONENT_SETUP_NOT_FINISHED({task.Id})");
                }
            }
            else
            {
                await PipeClient.Instance.SendMessage($"SETTINGS:COMPONENT_NOT_INSTALLED({task.Id})");
            }
        }

        public async void SendTasksData()
        {
            foreach (var task in Tasks)
            {
                await SendTaskData(task.Id);
            }
        }

        public async Task<TaskModel> GetTaskFromId(string id)
        {
            await _taskOperationLock.WaitAsync();
            try
            {
                var existTask = Tasks.FirstOrDefault(t => t.Id == id);
                if (existTask != null)
                {
                    return existTask;
                }
            }
            catch { }
            finally
            {
                _taskOperationLock.Release();
            }
            return null;
        }

        private async void AddNewTask(string id)
        {
            var t = await GetTaskFromId(id);
            if (t != null)
            {

            }
            else
            {
                ProcessManager processManager = new() { Id = id };
                processManager.onProcessStateChanged += ProcessManager_onProcessStateChanged;

                await _taskOperationLock.WaitAsync();
                try
                {
                    Tasks.Add(new() { Id = id, ProcessManager = processManager });
                }
                catch { }
                finally
                {
                    _taskOperationLock.Release();
                }
                TaskListUpdated?.Invoke();
            }
        }

        public async void CreateAndRunNewTask(string id)
        {
            var t = await GetTaskFromId(id);
            if (t != null)
            {
                await StopTask(id);
                await t.ProcessManager.StartProcess();
            }
            else
            {
                ProcessManager processManager = new() { Id = id };
                processManager.onProcessStateChanged += ProcessManager_onProcessStateChanged;

                await processManager.StartProcess();

                await _taskOperationLock.WaitAsync();
                try
                {
                    Tasks.Add(new() { Id = id, ProcessManager = processManager });
                }
                catch { }
                finally
                {
                    _taskOperationLock.Release();
                }
                TaskListUpdated?.Invoke();
            }
        }

        public async void CreateAndRunNewTask(string id, string args)
        {
            var t = await GetTaskFromId(id);
            if (t != null)
            {
                await StopTask(id);
                await t.ProcessManager.StartProcess(id, args);
            }
            else
            {
                ProcessManager processManager = new() { Id = id };
                processManager.onProcessStateChanged += ProcessManager_onProcessStateChanged;

                await processManager.StartProcess(id, args);

                await _taskOperationLock.WaitAsync();
                try
                {
                    Tasks.Add(new() { Id = id, ProcessManager = processManager });
                }
                catch { }
                finally
                {
                    _taskOperationLock.Release();
                }
                TaskListUpdated?.Invoke();
            }
        }

        private void ProcessManager_onProcessStateChanged(Tuple<string, bool> tuple)
        {
            TaskStateUpdated?.Invoke(Tuple.Create(tuple.Item1, tuple.Item2));
        }

        public async Task StopTask(string id)
        {
            var existTask = await GetTaskFromId(id);
            if (existTask != null)
            {
                await existTask.ProcessManager.StopProcess();
            }
        }

        public async Task RestartTask(string id)
        {
            var existTask = await GetTaskFromId(id);
            if (existTask != null)
            {
                await existTask.ProcessManager.RestartProcess();
            }
        }

        

        public async void StopAllTasks()
        {
            try
            {
                foreach (var task in Tasks)
                {
                    await task.ProcessManager.StopProcess();
                }
                await _taskOperationLock.WaitAsync();
                Tasks.Clear();
            }
            catch { }
            finally
            {
                _taskOperationLock.Release();
            }
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

        public async Task<bool> IsTaskRunned(string id)
        {
            var task = await GetTaskFromId(id);
            if (task == null) return false;

            return task.ProcessManager.processState;
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
    }
}
