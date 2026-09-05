using CDPIUI.Shared.PrettyErrorConvertionService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Shared.ComponentsTask
{
    /// <summary>
    /// Default tasks management service for <see cref="IProcessService"/>
    /// </summary>
    /// <typeparam name="T">Process service</typeparam>
    public class TasksManageService<T> where T : IProcessService, new()
    {
        /// <summary>
        /// Tasks list
        /// </summary>
        public readonly List<TaskModel<T>> Tasks = [];

        /// <summary>
        /// Invokes where some task added or removed
        /// </summary>
        public Action? TaskListUpdated;

        /// <summary>
        /// Invokes when task with <see cref="string"/> Id changed <see cref="bool"/> State
        /// </summary>
        public Action<Tuple<string, bool>>? TaskStateUpdated;

        /// <summary>
        /// Invokes when task with <see cref="string"/> Id changes <see cref="bool"/> SetupState
        /// </summary>
        public Action<Tuple<string, bool>>? TaskSetupStateUpdated;

        /// <summary>
        /// Show error message for task id.
        /// </summary>
        public Action<string>? ShowErrorMessageForTaskId;

        public TasksManageService() { }

        private readonly SemaphoreSlim _taskOperationLock = new(1, 1);

        /// <summary>
        /// Set lock; get <see cref="TaskModel{T}"/> from <see cref="string"/> Id; then release lock
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns>Requested <see cref="TaskModel{T}"/> if task with <see cref="TaskModel{T}.Id"/> exist, otherwise <see cref="null"/></returns>
        public async Task<TaskModel<T>?> GetTaskFromId(string id)
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

        /// <summary>
        /// Add new task to task list
        /// </summary>
        /// <param name="id">Task Id</param>
        public async void AddNewTask(string id)
        {
            var t = await GetTaskFromId(id);
            if (t != null) return;

            await _taskOperationLock.WaitAsync();

            var processManager = new T() { Id = id };
            processManager.ProcessStateChanged += HandleProcessStateUpdate;
            processManager.ShowErrorMessageWindow += HandleShowErrorMessageWindow;

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

        /// <summary>
        /// Add new task to task list, run associated <see cref="IProcessService"/>
        /// </summary>
        /// <param name="id">Task Id</param>
        public async Task CreateAndRunNewTask(string id)
        {
            var t = await GetTaskFromId(id);
            if (t != null)
            {
                await StopTask(id);
                await t.ProcessManager.StartProcess();
            }
            else
            {
                T processManager = new() { Id = id };
                processManager.ProcessStateChanged += HandleProcessStateUpdate;
                processManager.ShowErrorMessageWindow += HandleShowErrorMessageWindow;
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
        /// <summary>
        /// Add new task to task list, run associated <see cref="IProcessService"/> with custom executable
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <param name="executable">Executable</param>
        /// <param name="args">Command-line flags</param>
        public virtual async Task CreateAndRunNewTask(string id, string executable, string args)
        {
            var t = await GetTaskFromId(id);
            if (t != null)
            {
                await StopTask(id);
                await t.ProcessManager.StartProcess(executable, args);
            }
            else
            {
                await _taskOperationLock.WaitAsync();

                T processManager = new() { Id = id };
                processManager.ProcessStateChanged += HandleProcessStateUpdate;
                processManager.ShowErrorMessageWindow += HandleShowErrorMessageWindow;
                await processManager.StartProcess(executable, args);

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

        /// <summary>
        /// Add new task to task list, run associated <see cref="IProcessService"/> only with Id
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <param name="args">Command-line flags</param>
        public async Task CreateAndRunNewTask(string id, string args)
        {
            var t = await GetTaskFromId(id);
            if (t != null)
            {
                await StopTask(id);
                await t.ProcessManager.StartProcess(args, null);
            }
            else
            {
                await _taskOperationLock.WaitAsync();

                T processManager = new() { Id = id };
                processManager.ProcessStateChanged += HandleProcessStateUpdate;
                processManager.ShowErrorMessageWindow += HandleShowErrorMessageWindow;
                await processManager.StartProcess(args, null);

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

        /// <summary>
        /// Set setup status for task
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <param name="isSetupComplete">Is task with <see cref="string"/> Id ready to run</param>
        public async void SetTaskStatus(string id, bool isSetupComplete)
        {
            var targetTask = await GetTaskFromId(id);
            if (targetTask != null)
            {
                targetTask.IsSetupComplete = isSetupComplete;
            }
            else
            {
                await _taskOperationLock.WaitAsync();

                T processManager = new() { Id = id };
                processManager.ProcessStateChanged += HandleProcessStateUpdate;
                processManager.ShowErrorMessageWindow += HandleShowErrorMessageWindow;

                try
                {
                    Tasks.Add(new() { Id = id, ProcessManager = processManager, IsSetupComplete = isSetupComplete });
                }
                catch { }
                finally
                {
                    _taskOperationLock.Release();
                }

                TaskListUpdated?.Invoke();
            }

            TaskSetupStateUpdated?.Invoke(Tuple.Create(id, isSetupComplete));
        }

        /// <summary>
        /// Apply setup status to all existed tasks
        /// </summary>
        /// <param name="status">Target status</param>
        public async void ApplyStatusToAllTasks(bool status)
        {
            foreach (var task in Tasks)
            {
                task.IsSetupComplete = status;
                TaskSetupStateUpdated?.Invoke(Tuple.Create(task.Id, status));
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Stop task asynchronously
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns></returns>
        public async Task StopTask(string id)
        {
            var existTask = await GetTaskFromId(id);
            if (existTask != null)
            {
                await existTask.ProcessManager.StopProcess();
            }
        }

        /// <summary>
        /// Stop task asynchronously, then remove it from tasks list
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns></returns>
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

        /// <summary>
        /// Restart task asynchronously
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns></returns>
        public async Task RestartTask(string id)
        {
            var existTask = await GetTaskFromId(id);
            if (existTask != null)
            {
                await existTask.ProcessManager.RestartProcess();
            }
        }


        /// <summary>
        /// Stop all tasks asynchronously
        /// </summary>
        public async Task StopAllTasks()
        {
            await _taskOperationLock.WaitAsync();
            try
            {
                foreach (var task in Tasks)
                {
                    await task.ProcessManager.StopProcess();
                }
            }
            catch { }
            finally
            {
                _taskOperationLock.Release();
            }
        }

        /// <summary>
        /// Is task wuth <see cref="string"/> Id runned
        /// </summary>
        /// <param name="id">Task Id</param>
        /// <returns>true if runned, otherwise false</returns>
        public async Task<bool> IsTaskRunned(string id)
        {
            var task = await GetTaskFromId(id);
            if (task == null) return false;

            return task.ProcessManager.IsProcessRunning;
        }

        private void HandleProcessStateUpdate(Tuple<string, bool> tuple)
        {
            TaskStateUpdated?.Invoke(tuple);
        }
        private void HandleShowErrorMessageWindow(string id)
        {
            ShowErrorMessageForTaskId?.Invoke(id);
        }
    }
}
