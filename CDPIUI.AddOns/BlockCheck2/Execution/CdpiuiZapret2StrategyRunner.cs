using System.Collections.Concurrent;
using CDPIUI.Core.ComponentServices;
using CDPIUI.Core.ComponentServices.Helpers;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Shared.ComponentsTask;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

public sealed class CdpiuiZapret2StrategyRunnerOptions
{
    public string ComponentId { get; init; } =
        HardcodedItemIds.ComponentIds[Components.Zapret2];

    public TimeSpan TaskDiscoveryTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan StartupGracePeriod { get; init; } = TimeSpan.FromMilliseconds(750);
    public TimeSpan StopGracePeriod { get; init; } = TimeSpan.FromSeconds(1);
    public bool RestorePreviouslyRunningConfiguration { get; init; } = true;
    public IReadOnlyList<string> ConflictingComponentIds { get; init; } =
    [
        HardcodedItemIds.ComponentIds[Components.Zapret],
        HardcodedItemIds.ComponentIds[Components.GoodbyeDPI],
        HardcodedItemIds.ComponentIds[Components.ByeDPI],
        HardcodedItemIds.ComponentIds[Components.SpoofDPI],
        HardcodedItemIds.ComponentIds[Components.NoDPI],
    ];
}

/// <summary>
/// Runs temporary Zapret2 arguments through the same Core-to-Tray process path as
/// normal CDPIUI components. A previously running Zapret2 configuration is stopped
/// before baseline probes and restored when the complete scan finishes.
/// </summary>
public sealed class CdpiuiZapret2StrategyRunner : IBlockCheckStrategyRunner
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ComponentGates =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly CdpiuiZapret2StrategyRunnerOptions _options;
    private readonly ComponentTasksManager _tasksManager;
    private SemaphoreSlim? _componentGate;
    private bool _gateHeld;
    private bool _prepared;
    private readonly List<string> _previouslyRunning = [];

    public CdpiuiZapret2StrategyRunner(
        CdpiuiZapret2StrategyRunnerOptions? options = null,
        ComponentTasksManager? tasksManager = null)
    {
        _options = options ?? new CdpiuiZapret2StrategyRunnerOptions();
        _tasksManager = tasksManager ?? ComponentTasksManager.Instance;
    }

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (_prepared)
        {
            throw new InvalidOperationException("The Zapret2 scan runner is already prepared.");
        }

        if (!DatabaseHelper.Instance.IsItemInstalled(_options.ComponentId))
        {
            throw new InvalidOperationException(
                $"Zapret2 component '{_options.ComponentId}' is not installed.");
        }

        ComponentItemsLoaderHelper.Instance.Init();
        if (ComponentItemsLoaderHelper.Instance.GetComponentHelperFromId(_options.ComponentId) == null)
        {
            throw new FileNotFoundException(
                $"Zapret2 executable for component '{_options.ComponentId}' was not found.");
        }

        _componentGate = ComponentGates.GetOrAdd(
            _options.ComponentId,
            static _ => new SemaphoreSlim(1, 1));
        await _componentGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _gateHeld = true;

        _ = await GetProcessServiceAsync(cancellationToken).ConfigureAwait(false);
        _previouslyRunning.Clear();
        string[] managedComponentIds = _options.ConflictingComponentIds
            .Append(_options.ComponentId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (string componentId in managedComponentIds)
        {
            TaskModel<ProcessService>? task = await _tasksManager.GetTaskFromId(componentId)
                .ConfigureAwait(false);
            if (task?.ProcessManager.IsProcessRunning == true)
            {
                _previouslyRunning.Add(componentId);
            }
        }
        _prepared = true;

        foreach (string componentId in _previouslyRunning)
        {
            await StopComponentCoreAsync(componentId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StartAsync(string arguments, CancellationToken cancellationToken)
    {
        if (!_prepared)
        {
            throw new InvalidOperationException("PrepareAsync must be called before starting a strategy.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments);

        ProcessService current = await GetProcessServiceAsync(cancellationToken).ConfigureAwait(false);
        if (current.IsProcessRunning)
        {
            await StopCoreAsync(cancellationToken).ConfigureAwait(false);
        }

        await _tasksManager.CreateAndRunNewTask(_options.ComponentId, arguments)
            .ConfigureAwait(false);
        await Task.Delay(_options.StartupGracePeriod, cancellationToken).ConfigureAwait(false);

        ProcessService started = await GetProcessServiceAsync(cancellationToken).ConfigureAwait(false);
        if (started.IsErrorHappens)
        {
            string details = started.LastError?.ErrorCode ?? "unknown process error";
            throw new InvalidOperationException(
                $"Zapret2 failed to start: {AppendProcessOutput(details, started)}");
        }
        if (!started.IsProcessRunning)
        {
            throw new InvalidOperationException(
                AppendProcessOutput(
                    "Zapret2 stopped during the startup grace period.",
                    started));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        _prepared ? StopCoreAsync(cancellationToken) : Task.CompletedTask;

    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        if (!_gateHeld)
        {
            return;
        }

        try
        {
            if (_prepared)
            {
                List<string> errors = [];
                try
                {
                    await StopCoreAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    errors.Add(exception.Message);
                }

                if (_options.RestorePreviouslyRunningConfiguration)
                {
                    foreach (string componentId in _previouslyRunning)
                    {
                        try
                        {
                            await _tasksManager.CreateAndRunNewTask(componentId).ConfigureAwait(false);
                            await Task.Delay(_options.StartupGracePeriod, cancellationToken)
                                .ConfigureAwait(false);

                            TaskModel<ProcessService>? restored = await _tasksManager
                                .GetTaskFromId(componentId)
                                .ConfigureAwait(false);
                            if (restored?.ProcessManager.IsProcessRunning != true ||
                                restored.ProcessManager.IsErrorHappens)
                            {
                                throw new InvalidOperationException(
                                    $"Component '{componentId}' that was active before BlockCheck could not be restored.");
                            }
                        }
                        catch (Exception exception)
                        {
                            errors.Add(exception.Message);
                        }
                    }
                }

                if (errors.Count > 0)
                {
                    throw new InvalidOperationException(string.Join(" ", errors));
                }
            }
        }
        finally
        {
            _prepared = false;
            _previouslyRunning.Clear();
            _gateHeld = false;
            _componentGate?.Release();
            _componentGate = null;
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        await StopComponentCoreAsync(_options.ComponentId, cancellationToken).ConfigureAwait(false);
    }

    private async Task StopComponentCoreAsync(
        string componentId,
        CancellationToken cancellationToken)
    {
        TaskModel<ProcessService>? existing = await _tasksManager.GetTaskFromId(componentId)
            .ConfigureAwait(false);
        if (existing == null || !existing.ProcessManager.IsProcessRunning)
        {
            return;
        }

        await _tasksManager.StopTask(componentId).ConfigureAwait(false);
        await Task.Delay(_options.StopGracePeriod, cancellationToken).ConfigureAwait(false);

        TaskModel<ProcessService>? process = await _tasksManager.GetTaskFromId(componentId)
            .ConfigureAwait(false);
        if (process?.ProcessManager.IsProcessRunning == true)
        {
            throw new InvalidOperationException(
                $"Component '{componentId}' did not stop within the expected interval.");
        }
    }

    private async Task<ProcessService> GetProcessServiceAsync(CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + _options.TaskDiscoveryTimeout;
        bool addRequested = false;

        while (DateTime.UtcNow < deadline)
        {
            TaskModel<ProcessService>? task = await _tasksManager
                .GetTaskFromId(_options.ComponentId)
                .ConfigureAwait(false);
            if (task != null)
            {
                return task.ProcessManager;
            }

            if (!addRequested)
            {
                _tasksManager.AddNewTask(_options.ComponentId);
                addRequested = true;
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"CDPIUI process task '{_options.ComponentId}' was not initialized in time.");
    }

    private static string AppendProcessOutput(string message, ProcessService process)
    {
        string output = process.GetDefaultProcessOutput().Trim();
        if (output.Length == 0)
        {
            return message;
        }

        const int maximumOutputLength = 800;
        if (output.Length > maximumOutputLength)
        {
            output = output[^maximumOutputLength..];
        }
        return $"{message} Process output: {output}";
    }
}
