using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI_TrayIcon.Helper
{
    public class MsiInstallerHelper
    {
        public enum MsiState
        {
            GettingReady,
            Installing,
            Complete,
            CompleteRestartRequest,
            ExceptionHappens
        }

        private static MsiInstallerHelper? _instance;
        private static readonly object _lock = new object();

        public static MsiInstallerHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new MsiInstallerHelper();
                    return _instance;
                }
            }
        }

        private MsiInstallerHelper() { }

        private class QueueItem
        {
            public string OperationId { get; init; } = "";
            public string FilePath { get; init; } = "";
        }

        private readonly Queue<QueueItem> _queue = new Queue<QueueItem>();
        private readonly HashSet<string> _pendingOps = new HashSet<string>();

        private string? _currentOperationId;
        private Process? _currentProcess;

        public void AddToQueue(string operationId, string fileName)
        {
            lock (_lock)
            {
                if (_pendingOps.Contains(operationId) || _currentOperationId == operationId)
                    return;

                var item = new QueueItem
                {
                    OperationId = operationId,
                    FilePath = fileName
                };

                _queue.Enqueue(item);
                _pendingOps.Add(operationId);
            }

            _ = PipeServer.Instance.SendMessage($"MSI:SETSTATUS({operationId}$SEPARATOR{(int)MsiState.GettingReady})");

            TryStartNext();
        }

        public void RemoveFromQueue(string operationId)
        {
            bool removedFromWaiting = false;
            bool wasRunning = false;

            lock (_lock)
            {
                if (_currentOperationId == operationId)
                {
                    wasRunning = true;
                    try
                    {
                        _currentProcess?.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance?.CreateErrorLog(nameof(MsiInstallerHelper), $"Failed to kill process for {operationId}: {ex.Message}");
                    }
                }
                else if (_pendingOps.Contains(operationId))
                {
                    var newQ = new Queue<QueueItem>(_queue.Count);
                    while (_queue.Count > 0)
                    {
                        var item = _queue.Dequeue();
                        if (item.OperationId == operationId)
                        {
                            removedFromWaiting = true;
                            continue;
                        }
                        newQ.Enqueue(item);
                    }
                    while (newQ.Count > 0)
                        _queue.Enqueue(newQ.Dequeue());

                    _pendingOps.Remove(operationId);
                }
            }

            if (removedFromWaiting)
            {
                _ = PipeServer.Instance.SendMessage($"MSI:REMOVED({operationId})");
            }

            if (wasRunning)
            {
                _ = PipeServer.Instance.SendMessage($"MSI:SETSTATUS({operationId}$SEPARATOR{(int)MsiState.ExceptionHappens})");
                Logger.Instance?.CreateErrorLog(nameof(MsiInstallerHelper), $"Installation for {operationId} was cancelled by RemoveFromQueue.");
            }

            TryStartNext();
        }

        private void TryStartNext()
        {
            QueueItem? next = null;

            lock (_lock)
            {
                if (_currentOperationId != null)
                    return; 

                if (_queue.Count == 0)
                    return;

                next = _queue.Dequeue();
                if (next != null)
                    _pendingOps.Remove(next.OperationId);

                if (next != null)
                    _currentOperationId = next.OperationId;
            }

            if (next != null)
            {
                _ = ProcessMsi(next.OperationId, next.FilePath);
            }
        }


        private async Task ProcessMsi(string operationId, string filePath)
        {
            try
            {
                await PipeServer.Instance.SendMessage($"MSI:SETSTATUS({operationId}$SEPARATOR{(int)MsiState.Installing})");

                lock (_lock)
                {
                    if (Path.GetExtension(filePath) == ".msi")
                        _currentProcess = RunHelper.Run("msiexec.exe", $"/i \"{filePath}\" /passive /norestart");
                    else
                        _currentProcess = RunHelper.Run(filePath, $"/install /passive /norestart");
                }

                if (_currentProcess == null)
                {
                    await PipeServer.Instance.SendMessage($"MSI:SETSTATUS({operationId}$SEPARATOR{(int)MsiState.ExceptionHappens})");
                    Logger.Instance.CreateErrorLog(nameof(MsiInstallerHelper), $"UNKNOWN");
                    throw new NullReferenceException();
                }

                await _currentProcess.WaitForExitAsync().ConfigureAwait(false);
                int exitCode = _currentProcess.ExitCode;

                if (exitCode == 3010)
                {
                    await PipeServer.Instance.SendMessage($"MSI:SETSTATUS({operationId}$SEPARATOR{(int)MsiState.CompleteRestartRequest})");
                }
                else if (exitCode == 0 || exitCode == 1603)
                {
                    await PipeServer.Instance.SendMessage($"MSI:SETSTATUS({operationId}$SEPARATOR{(int)MsiState.Complete})");
                }
                else
                {
                    await PipeServer.Instance.SendMessage($"MSI:SETSTATUS({operationId}$SEPARATOR{(int)MsiState.ExceptionHappens})");
                    Logger.Instance.CreateErrorLog(nameof(MsiInstallerHelper), $"Cannot install package {exitCode}");

                    TrayIconHelper.Instance.ShowMessage(
                        LocaleHelper.GetLocaleString("MsiInstallerHelper"),
                        string.Format(LocaleHelper.GetLocaleString("MsiInstallerHelperErr"), Path.GetFileNameWithoutExtension(filePath), exitCode),
                        "LOGGER:OPEN_MSI_LOG"
                    );
                }
            }
            catch (Exception ex)
            {
                await PipeServer.Instance.SendMessage($"MSI:SETSTATUS({operationId}$SEPARATOR{(int)MsiState.ExceptionHappens})");
                Logger.Instance.CreateErrorLog(nameof(MsiInstallerHelper), $"Exception while installing {filePath}: {ex}");

                TrayIconHelper.Instance.ShowMessage(
                        LocaleHelper.GetLocaleString("MsiInstallerHelper"),
                        string.Format(LocaleHelper.GetLocaleString("MsiInstallerHelperErr"), Path.GetFileNameWithoutExtension(filePath), "ERR_UNKNOWN"),
                        "LOGGER:OPEN_MSI_LOG"
                    );
            }
            finally
            {
                lock (_lock)
                {
                    try { _currentProcess?.Dispose(); } catch { }
                    _currentProcess = null;
                    _currentOperationId = null;
                }

                TryStartNext();
            }
        }
    }
}
