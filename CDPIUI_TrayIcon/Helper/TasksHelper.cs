using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI_TrayIcon.Helper
{
    public class TasksHelper
    {
        public class TaskModel
        {
            public required string Id { get; set; }
            public required ProcessManager ProcessManager { get; set; }
        }

        public List<TaskModel> Tasks { get; private set; } = [];

        public Action? TaskListUpdated;
        public Action<Tuple<string, bool>>? TaskStateUpdated;

        private static TasksHelper? _instance;
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

        public TasksHelper() { }

        private readonly SemaphoreSlim _taskOperationLock = new SemaphoreSlim(1, 1);

        public async Task<TaskModel?> GetTaskFromId(string id)
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

        public async void AddNewTask(string id)
        {
            var t = await GetTaskFromId(id);
            if (t != null) return;

            ProcessManager processManager = new() { Id = id };
            processManager.ProcessStateChanged += HandleProcessStateUpdate;

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
                processManager.ProcessStateChanged += HandleProcessStateUpdate;
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

        public async void CreateAndRunNewTask(string id, string executable, string args)
        {
            var t = await GetTaskFromId(id);
            if (t != null)
            {
                await StopTask(id);
                await t.ProcessManager.StartProcess(executable, args);
            }
            else
            {
                ProcessManager processManager = new() { Id = id };
                processManager.ProcessStateChanged += HandleProcessStateUpdate;
                await processManager.StartProcess(executable, args);

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

        public async Task StopTask(string id)
        {
            var existTask = await GetTaskFromId(id);
            if (existTask != null)
            {
                await existTask.ProcessManager.StopProcess();
            }
        }

        public async Task StopAndRemoveTaskAsync(string id)
        {
            var existTask = await GetTaskFromId(id);

            await _taskOperationLock.WaitAsync();
            try
            {
                
                if (existTask != null)
                {
                    await existTask.ProcessManager.StopProcess();
                    Tasks.Remove(existTask);

                    TaskListUpdated?.Invoke();
                }
            }
            catch { }
            finally { _taskOperationLock.Release(); }

        }

        public async Task RestartTask(string id)
        {
            var existTask = await GetTaskFromId(id);
            if (existTask != null)
            {
                await existTask.ProcessManager.RestartProcess();
            }
        }

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

        public async void StopAllTasks()
        {
            await _taskOperationLock.WaitAsync();
            try
            {
                foreach (var task in Tasks)
                {
                    await task.ProcessManager.StopProcess();
                    task.ProcessManager.ProcessStateChanged -= HandleProcessStateUpdate;
                }
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

        private void HandleProcessStateUpdate(Tuple<string, bool> tuple)
        {
            TaskStateUpdated?.Invoke(tuple);
        }

        public async Task<bool> IsTaskRunned(string id)
        {
            var task = await GetTaskFromId(id);
            if (task == null) return false;

            return task.ProcessManager.GetState();
        }
    }
}
