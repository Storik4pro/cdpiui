using CDPIUI.Core.Basic;
using CDPIUI.Shared.Migration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Migration;

internal enum MigrationSessionState
{
    Accepted,
    Preparing,
    ReadyForComponents,
    WaitingForLicense,
    LoadingComponents,
    DownloadingComponents,
    ReadyToImport,
    Importing,
    Completed,
    Failed
}

internal sealed class GoodbyeDpiMigrationSession : IDisposable
{
    private readonly object syncRoot = new();
    private readonly MigrationArchiveInspectionService inspectionService = new();
    private readonly CancellationTokenSource cancellation = new();
    private Task? preparationTask;

    public GoodbyeDpiMigrationActivationRequest Request { get; }
    public VerifiedMigrationPackage? Package { get; private set; }
    public MigrationSessionState State { get; private set; } = MigrationSessionState.Accepted;
    public double Progress { get; private set; }
    public string? Message { get; private set; }
    public string? ErrorCode { get; private set; }
    public bool IsTerminal => State is MigrationSessionState.Completed or MigrationSessionState.Failed;

    public event EventHandler? Changed;

    public GoodbyeDpiMigrationSession(GoodbyeDpiMigrationActivationRequest request)
    {
        Request = request;
    }

    public void BeginPreparation()
    {
        lock (syncRoot)
        {
            if (preparationTask is { IsCompleted: false })
                return;
            preparationTask = PrepareAsync();
        }
    }

    public async Task RetryPreparationAsync()
    {
        lock (syncRoot)
        {
            if (preparationTask is { IsCompleted: false })
                return;
            Package = null;
            preparationTask = PrepareAsync();
        }
        await preparationTask;
    }

    public Task UpdateAsync(
        MigrationSessionState state,
        double progress,
        string? message = null,
        string? errorCode = null)
    {
        SetState(state, progress, message, errorCode);
        return MigrationStatusPipeClient.SendAsync(Request, state, progress, message, errorCode);
    }

    public Task ReannounceAsync() =>
        MigrationStatusPipeClient.SendAsync(Request, State, Progress, Message, ErrorCode);

    private async Task PrepareAsync()
    {
        await UpdateAsync(MigrationSessionState.Preparing, 5);
        try
        {
            VerifiedMigrationPackage package = await Task.Run(
                () => inspectionService.InspectAndStage(Request, cancellation.Token),
                cancellation.Token);
            Package = package;
            await UpdateAsync(MigrationSessionState.ReadyForComponents, 20);
        }
        catch (OperationCanceledException)
        {
            await UpdateAsync(MigrationSessionState.Failed, Progress, "Migration preparation was canceled.", "MIGRATION_CANCELED");
        }
        catch (Exception exception)
        {
            Logger.Instance.CreateWarningLog(nameof(GoodbyeDpiMigrationSession), exception.ToString());
            string code = exception switch
            {
                FileNotFoundException => "MIGRATION_ARCHIVE_NOT_FOUND",
                InvalidDataException => "MIGRATION_ARCHIVE_INVALID",
                UnauthorizedAccessException => "MIGRATION_ARCHIVE_ACCESS_DENIED",
                _ => "MIGRATION_PREPARATION_FAILED"
            };
            await UpdateAsync(MigrationSessionState.Failed, Progress, exception.Message, code);
        }
    }

    private void SetState(
        MigrationSessionState state,
        double progress,
        string? message,
        string? errorCode)
    {
        lock (syncRoot)
        {
            State = state;
            Progress = Math.Clamp(progress, 0, 100);
            Message = message;
            ErrorCode = errorCode;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
