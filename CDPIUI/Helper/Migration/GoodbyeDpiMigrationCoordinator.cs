using CDPIUI.Shared.Migration;

namespace CDPIUI.Helper.Migration;

internal sealed class GoodbyeDpiMigrationCoordinator
{
    private readonly object syncRoot = new();
    private GoodbyeDpiMigrationSession? currentSession;

    public static GoodbyeDpiMigrationCoordinator Instance { get; } = new();

    public bool TryAccept(
        GoodbyeDpiMigrationActivationRequest request,
        out GoodbyeDpiMigrationSession? session)
    {
        lock (syncRoot)
        {
            if (currentSession != null && !currentSession.IsTerminal)
            {
                if (IsSameSession(currentSession.Request, request))
                {
                    session = currentSession;
                    _ = session.ReannounceAsync();
                    return true;
                }

                session = null;
                _ = MigrationStatusPipeClient.SendAsync(
                    request, MigrationSessionState.Failed, 0,
                    "Another migration is already active.", "MIGRATION_BUSY");
                return false;
            }

            if (currentSession != null && IsSameSession(currentSession.Request, request))
            {
                session = currentSession;
                _ = session.ReannounceAsync();
                return true;
            }

            currentSession?.Dispose();
            currentSession = new GoodbyeDpiMigrationSession(request);
            session = currentSession;
            _ = session.UpdateAsync(MigrationSessionState.Accepted, 0);
            session.BeginPreparation();
            return true;
        }
    }

    private static bool IsSameSession(
        GoodbyeDpiMigrationActivationRequest left,
        GoodbyeDpiMigrationActivationRequest right) =>
        left.MigrationId == right.MigrationId &&
        left.ArchiveSha256 == right.ArchiveSha256 &&
        left.ResponsePipeName == right.ResponsePipeName &&
        left.SessionToken == right.SessionToken;
}
