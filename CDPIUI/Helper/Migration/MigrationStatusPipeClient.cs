using CDPIUI.Shared.Migration;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Migration;

internal static class MigrationStatusPipeClient
{
    public static async Task SendAsync(
        GoodbyeDpiMigrationActivationRequest request,
        MigrationSessionState state,
        double progress,
        string? message = null,
        string? errorCode = null)
    {
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromMilliseconds(350));
            using NamedPipeClientStream pipe = new(
                ".", request.ResponsePipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token);
            var status = new
            {
                protocolVersion = request.ProtocolVersion,
                migrationId = request.MigrationId.ToString("D"),
                sessionToken = request.SessionToken,
                state = state.ToString(),
                progress = Math.Clamp(progress, 0, 100),
                message,
                errorCode,
                timestampUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            string json = JsonSerializer.Serialize(status);
            using StreamWriter writer = new(pipe, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            };
            await writer.WriteLineAsync(json).WaitAsync(timeout.Token);
        }
        catch
        {
            // Status delivery is best effort. The UI remains the source of truth.
        }
    }
}
