using CDPIUI.Shared.ConditionalLaunch;
using CDPIUI.TrayIcon.Helper;
using CDPIUI.TrayIcon.Helper.Basic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CDPIUI.TrayIcon.ConditionalLaunch
{
    internal sealed class ConditionalLaunchEngine : IDisposable
    {
        private const int WmHotKey = 0x0312;
        private const uint ModNoRepeat = 0x4000;

        private static readonly Lazy<ConditionalLaunchEngine> LazyInstance =
            new(() => new ConditionalLaunchEngine());

        private readonly object _sync = new();
        private readonly Dictionary<int, List<ConditionalTask>> _hotKeyTasks = [];
        private readonly Dictionary<string, ProcessTriggerState> _processStates =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _runningTasks = new(StringComparer.OrdinalIgnoreCase);

        private List<ConditionalTask> _tasks = [];
        private List<ConditionalTask> _loadedTasks = [];
        private Control? _messageWindow;
        private IntPtr _windowHandle;
        private string? _tasksDirectory;
        private FileSystemWatcher? _watcher;
        private System.Threading.Timer? _processTimer;
        private System.Threading.Timer? _reloadTimer;
        private int _isPolling;
        private int _nextHotKeyId = 0x4300;
        private bool _disposed;

        public static ConditionalLaunchEngine Instance => LazyInstance.Value;

        private ConditionalLaunchEngine() { }

        public void Start(Control messageWindow)
        {
            ArgumentNullException.ThrowIfNull(messageWindow);
            var windowHandle = messageWindow.Handle;
            if (windowHandle == IntPtr.Zero)
                throw new ArgumentException("A valid window handle is required.", nameof(messageWindow));

            lock (_sync)
            {
                if (_windowHandle != IntPtr.Zero)
                    return;

                _messageWindow = messageWindow;
                ConfigureTasksDirectory(
                    ConditionalTaskFileService.GetTasksDirectoryFromSettingsFile(
                        SettingsManager.Instance.SettingsFilePath));

                Logger.Instance.CreateDebugLog(
                    nameof(ConditionalLaunchEngine),
                    $"Starting conditional launch engine. Tasks directory: '{_tasksDirectory}'.");

                _processTimer = new System.Threading.Timer(
                    _ => PollProcesses(),
                    null,
                    Timeout.InfiniteTimeSpan,
                    Timeout.InfiniteTimeSpan);
                _windowHandle = windowHandle;
            }

            Reload();
            _processTimer?.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        public void Reload(string? tasksDirectory = null)
        {
            var messageWindow = _messageWindow;
            if (messageWindow != null && messageWindow.InvokeRequired)
            {
                try
                {
                    messageWindow.BeginInvoke(new Action(() => Reload(tasksDirectory)));
                }
                catch (InvalidOperationException) { }
                return;
            }

            string? directory;
            lock (_sync)
            {
                if (!string.IsNullOrWhiteSpace(tasksDirectory))
                    ConfigureTasksDirectory(tasksDirectory);
                directory = _tasksDirectory;
            }

            if (string.IsNullOrWhiteSpace(directory))
                return;

            IReadOnlyList<ConditionalTask> loadedTasks;
            try
            {
                loadedTasks = ConditionalTaskFileService.LoadDirectory(directory);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(
                    nameof(ConditionalLaunchEngine),
                    $"Cannot load conditional tasks: {ex}");
                return;
            }

            lock (_sync)
            {
                UnregisterHotKeys();
                foreach (var state in _processStates.Values)
                    state.CancelPending();
                _processStates.Clear();

                _loadedTasks = loadedTasks.ToList();
                _tasks = _loadedTasks.Where(task => task.IsEnabled).ToList();
                RegisterHotKeys();

                var fileCount = Directory.EnumerateFiles(
                    directory,
                    $"*{ConditionalTaskFileService.FileExtension}").Count();
                Logger.Instance.CreateDebugLog(
                    nameof(ConditionalLaunchEngine),
                    $"Conditional tasks reloaded: {loadedTasks.Count} valid, {_tasks.Count} enabled, {fileCount} files total.");
                if (fileCount != loadedTasks.Count)
                {
                    Logger.Instance.CreateWarningLog(
                        nameof(ConditionalLaunchEngine),
                        $"Ignored {fileCount - loadedTasks.Count} invalid conditional task file(s).");
                }
            }
        }

        public bool HandleWindowMessage(ref Message message)
        {
            if (message.Msg != WmHotKey)
                return false;

            List<ConditionalTask>? tasks;
            lock (_sync)
            {
                tasks = _hotKeyTasks.TryGetValue(message.WParam.ToInt32(), out var registered)
                    ? registered.ToList()
                    : null;
            }

            if (tasks == null || tasks.Count == 0)
                return false;

            Logger.Instance.CreateDebugLog(
                nameof(ConditionalLaunchEngine),
                $"Hot key received for {tasks.Count} conditional task(s).");
            RunHighestPriority(tasks);
            return true;
        }

        public bool RunTask(string? taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
                return false;

            ConditionalTask? task;
            lock (_sync)
            {
                task = _loadedTasks.FirstOrDefault(item => string.Equals(
                    item.Id,
                    taskId,
                    StringComparison.OrdinalIgnoreCase));
            }

            if (task == null)
            {
                Logger.Instance.CreateWarningLog(
                    nameof(ConditionalLaunchEngine),
                    $"Cannot run conditional task '{taskId}': task was not found.");
                return false;
            }

            Logger.Instance.CreateDebugLog(
                nameof(ConditionalLaunchEngine),
                $"Manual run requested for conditional task '{task.Name}' ({task.Id}).");
            _ = RunTaskOnceAsync(task);
            return true;
        }

        private void RegisterHotKeys()
        {
            var groups = _tasks
                .SelectMany(task => task.Triggers
                    .Where(trigger => trigger.Type == ConditionalTriggerType.HotKey)
                    .Select(trigger => new TriggerBinding(task, trigger, string.Empty)))
                .GroupBy(binding => GetHotKeyIdentity(binding.Trigger), StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var first = group.First();
                if (!TryGetHotKey(first.Trigger, out var modifiers, out var virtualKey))
                {
                    Logger.Instance.CreateWarningLog(
                        nameof(ConditionalLaunchEngine),
                        $"Invalid hot key in task '{first.Task.Name}'.");
                    continue;
                }

                var id = _nextHotKeyId++;
                if (!RegisterHotKey(_windowHandle, id, modifiers | ModNoRepeat, virtualKey))
                {
                    Logger.Instance.CreateWarningLog(
                        nameof(ConditionalLaunchEngine),
                        $"Cannot register hot key for task '{first.Task.Name}'. Win32: {Marshal.GetLastWin32Error()}.");
                    continue;
                }

                _hotKeyTasks[id] = group
                    .Select(binding => binding.Task)
                    .DistinctBy(task => task.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                Logger.Instance.CreateDebugLog(
                    nameof(ConditionalLaunchEngine),
                    $"Registered hot key '{GetHotKeyIdentity(first.Trigger)}' for {_hotKeyTasks[id].Count} conditional task(s).");
            }
        }

        private void UnregisterHotKeys()
        {
            foreach (var id in _hotKeyTasks.Keys)
                UnregisterHotKey(_windowHandle, id);
            _hotKeyTasks.Clear();
        }

        private void PollProcesses()
        {
            if (Interlocked.Exchange(ref _isPolling, 1) != 0)
                return;

            try
            {
                HashSet<string> runningProcessNames = new(StringComparer.OrdinalIgnoreCase);
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        runningProcessNames.Add(process.ProcessName);
                    }
                    catch { }
                    finally
                    {
                        process.Dispose();
                    }
                }

                List<TriggerBinding> newlyMatched = [];
                lock (_sync)
                {
                    foreach (var binding in EnumerateProcessTriggers())
                    {
                        var task = binding.Task;
                        var trigger = binding.Trigger;
                        var rawProcessName = trigger.GetParameter("processName");
                        if (string.IsNullOrWhiteSpace(rawProcessName))
                            continue;

                        var processName = Path.GetFileNameWithoutExtension(rawProcessName.Trim());
                        var isRunning = runningProcessNames.Contains(processName);

                        if (!_processStates.TryGetValue(binding.StateKey, out var state))
                        {
                            state = new ProcessTriggerState(isRunning);
                            _processStates[binding.StateKey] = state;
                            continue;
                        }

                        var matched = trigger.Type switch
                        {
                            ConditionalTriggerType.ProcessStarted => !state.LastIsRunning && isRunning,
                            ConditionalTriggerType.ProcessStopped => state.LastIsRunning && !isRunning,
                            _ => false
                        };

                        var conditionStillValid = trigger.Type switch
                        {
                            ConditionalTriggerType.ProcessStarted => isRunning,
                            ConditionalTriggerType.ProcessStopped => !isRunning,
                            _ => true
                        };

                        if (!conditionStillValid)
                            state.CancelPending();

                        state.LastIsRunning = isRunning;
                        if (matched)
                            newlyMatched.Add(binding);
                    }
                }

                ScheduleHighestPriority(newlyMatched);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(
                    nameof(ConditionalLaunchEngine),
                    $"Process condition polling failed: {ex}");
            }
            finally
            {
                Interlocked.Exchange(ref _isPolling, 0);
            }
        }

        private void ScheduleHighestPriority(IReadOnlyCollection<TriggerBinding> matches)
        {
            if (matches.Count == 0)
                return;

            var taskMatches = matches
                .GroupBy(match => match.Task.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(match => match.Trigger.DelaySeconds).First())
                .ToList();
            var highestPriority = taskMatches.Max(match => (int)match.Task.Priority);
            Logger.Instance.CreateDebugLog(
                nameof(ConditionalLaunchEngine),
                $"Process condition matched {taskMatches.Count} task(s); scheduling priority {highestPriority}.");
            foreach (var match in taskMatches.Where(match => (int)match.Task.Priority == highestPriority))
            {
                CancellationTokenSource cancellation;
                lock (_sync)
                {
                    if (!_processStates.TryGetValue(match.StateKey, out var state))
                        continue;

                    state.CancelPending();
                    state.Pending = new CancellationTokenSource();
                    cancellation = state.Pending;
                }

                _ = RunAfterDelayAsync(match, cancellation);
            }
        }

        private async Task RunAfterDelayAsync(
            TriggerBinding match,
            CancellationTokenSource cancellation)
        {
            try
            {
                if (match.Trigger.DelaySeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(match.Trigger.DelaySeconds), cancellation.Token);

                if (!cancellation.IsCancellationRequested)
                    await RunTaskOnceAsync(match.Task);
            }
            catch (OperationCanceledException) { }
            finally
            {
                lock (_sync)
                {
                    if (_processStates.TryGetValue(match.StateKey, out var state) &&
                        ReferenceEquals(state.Pending, cancellation))
                    {
                        state.Pending = null;
                    }
                }
                cancellation.Dispose();
            }
        }

        private void RunHighestPriority(IReadOnlyCollection<ConditionalTask> tasks)
        {
            var highestPriority = tasks.Max(task => (int)task.Priority);
            foreach (var task in tasks.Where(task => (int)task.Priority == highestPriority))
                _ = RunTaskOnceAsync(task);
        }

        private async Task RunTaskOnceAsync(ConditionalTask task)
        {
            lock (_sync)
            {
                if (!_runningTasks.Add(task.Id))
                    return;
            }

            try
            {
                Logger.Instance.CreateDebugLog(
                    nameof(ConditionalLaunchEngine),
                    $"Starting conditional task '{task.Name}' ({task.Id}).");
                await ConditionalActionExecutor.ExecuteTaskAsync(task);
                Logger.Instance.CreateDebugLog(
                    nameof(ConditionalLaunchEngine),
                    $"Conditional task '{task.Name}' ({task.Id}) completed.");
                if (SettingsManager.Instance.GetValueOrDefault<bool>(
                    "NOTIFICATIONS",
                    "conditionalLaunchActions",
                    defaultValue: true))
                {
                    NotifyHelper.ShowMessage(
                        LocaleHelper.GetLocaleString("ConditionalLaunchNotificationTitle"),
                        string.Format(
                            LocaleHelper.GetLocaleString("ConditionalLaunchActionCompletedMessage"),
                            task.Name),
                        string.Empty);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(
                    nameof(ConditionalLaunchEngine),
                    $"Task '{task.Name}' failed: {ex}");
            }
            finally
            {
                lock (_sync)
                    _runningTasks.Remove(task.Id);
            }
        }

        private void ConfigureTasksDirectory(string directory)
        {
            directory = Path.GetFullPath(directory);
            if (string.Equals(_tasksDirectory, directory, StringComparison.OrdinalIgnoreCase) &&
                _watcher != null)
            {
                return;
            }

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= TaskFilesChanged;
                _watcher.Created -= TaskFilesChanged;
                _watcher.Deleted -= TaskFilesChanged;
                _watcher.Renamed -= TaskFilesChanged;
                _watcher.Dispose();
            }

            Directory.CreateDirectory(directory);
            _tasksDirectory = directory;
            Logger.Instance.CreateDebugLog(
                nameof(ConditionalLaunchEngine),
                $"Using conditional tasks directory: '{directory}'.");
            _watcher = new FileSystemWatcher(
                directory,
                $"*{ConditionalTaskFileService.FileExtension}")
            {
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };
            _watcher.Changed += TaskFilesChanged;
            _watcher.Created += TaskFilesChanged;
            _watcher.Deleted += TaskFilesChanged;
            _watcher.Renamed += TaskFilesChanged;
        }

        private void TaskFilesChanged(object sender, FileSystemEventArgs e)
        {
            lock (_sync)
            {
                _reloadTimer?.Dispose();
                _reloadTimer = new System.Threading.Timer(
                    _ => Reload(),
                    null,
                    TimeSpan.FromMilliseconds(500),
                    Timeout.InfiniteTimeSpan);
            }
        }

        private static bool TryGetHotKey(
            ConditionalTrigger trigger,
            out uint modifiers,
            out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;

            if (!Enum.TryParse<ConditionalHotKeyModifiers>(
                    trigger.GetParameter("modifiers"),
                    ignoreCase: true,
                    out var parsedModifiers) ||
                !Enum.TryParse<Keys>(
                    trigger.GetParameter("key"),
                    ignoreCase: true,
                    out var key))
            {
                return false;
            }

            modifiers = (uint)parsedModifiers;
            virtualKey = (uint)key;
            return key != Keys.None;
        }

        private static string GetHotKeyIdentity(ConditionalTrigger trigger)
        {
            return $"{trigger.GetParameter("modifiers")}|{trigger.GetParameter("key")}";
        }

        private static bool IsProcessTrigger(ConditionalTriggerType type)
        {
            return type != ConditionalTriggerType.HotKey;
        }

        private IEnumerable<TriggerBinding> EnumerateProcessTriggers()
        {
            foreach (var task in _tasks)
            {
                for (var index = 0; index < task.Triggers.Count; index++)
                {
                    var trigger = task.Triggers[index];
                    if (IsProcessTrigger(trigger.Type))
                        yield return new TriggerBinding(task, trigger, $"{task.Id}|{index}");
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;

                UnregisterHotKeys();
                foreach (var state in _processStates.Values)
                    state.CancelPending();
                _processStates.Clear();
                _loadedTasks.Clear();
                _tasks.Clear();

                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Changed -= TaskFilesChanged;
                    _watcher.Created -= TaskFilesChanged;
                    _watcher.Deleted -= TaskFilesChanged;
                    _watcher.Renamed -= TaskFilesChanged;
                    _watcher.Dispose();
                }

                _processTimer?.Dispose();
                _reloadTimer?.Dispose();
                _messageWindow = null;
            }
        }

        private sealed class ProcessTriggerState(bool lastIsRunning)
        {
            public bool LastIsRunning { get; set; } = lastIsRunning;
            public CancellationTokenSource? Pending { get; set; }

            public void CancelPending()
            {
                Pending?.Cancel();
                Pending = null;
            }
        }

        private sealed record TriggerBinding(
            ConditionalTask Task,
            ConditionalTrigger Trigger,
            string StateKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(
            IntPtr windowHandle,
            int id,
            uint modifiers,
            uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
    }
}
