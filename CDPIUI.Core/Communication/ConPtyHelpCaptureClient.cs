using CDPIUI.Shared.Pipe;
using CDPIUI.Shared.Pipe.Models;
using System.Collections.Concurrent;
using System.Text;

namespace CDPIUI.Core.Communication;

public sealed class ConPtyHelpCaptureResult
{
    public string Output { get; init; } = string.Empty;
    public int ExitCode { get; init; } = -1;
    public bool TimedOut { get; init; }
    public string Error { get; init; } = string.Empty;
}

public static class ConPtyHelpCaptureClient
{
    private static readonly ConcurrentDictionary<string, PendingCapture> Pending = new();

    public static async Task<ConPtyHelpCaptureResult> CaptureHelpAsync(
        string componentId,
        string executablePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        string requestId = Guid.NewGuid().ToString("N");
        PendingCapture capture = new();
        if (!Pending.TryAdd(requestId, capture))
        {
            return CreateError("Could not allocate a help capture request.");
        }

        try
        {
            bool sent = await PipeHelper.SendConPTYHelpCaptureRequest(
                componentId,
                requestId,
                executablePath);
            if (!sent)
            {
                return CreateError("The elevated TrayIcon process is not connected.");
            }

            try
            {
                return await capture.Completion.Task.WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                return CreateError("The TrayIcon process did not return help before the timeout.");
            }
        }
        finally
        {
            Pending.TryRemove(requestId, out _);
        }
    }

    public static bool HandleMessage(CONPTYMessageModel model)
    {
        if (model.MessageType is not (
            CONPTYMessageIds.HelpOutputChunk or
            CONPTYMessageIds.HelpOutputCompleted))
        {
            return false;
        }

        string requestId = model.MessageData?["requestId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requestId) || !Pending.TryGetValue(requestId, out PendingCapture? capture))
        {
            return true;
        }

        if (model.MessageType == CONPTYMessageIds.HelpOutputChunk)
        {
            string indexText = model.MessageData?["chunkIndex"] ?? string.Empty;
            string encodedOutput = model.MessageData?["output"] ?? string.Empty;
            if (!int.TryParse(indexText, out int index) || index < 0)
            {
                capture.Fail("The TrayIcon process returned an invalid help chunk index.");
                return true;
            }

            try
            {
                capture.AddChunk(index, PipePayloadCodec.Decode(encodedOutput));
            }
            catch (FormatException)
            {
                capture.Fail("The TrayIcon process returned an invalid help chunk.");
            }

            return true;
        }

        int.TryParse(model.MessageData?["totalChunks"], out int totalChunks);
        int.TryParse(model.MessageData?["exitCode"], out int exitCode);
        bool.TryParse(model.MessageData?["timedOut"], out bool timedOut);
        string error = string.Empty;
        try
        {
            error = PipePayloadCodec.DecodeString(model.MessageData?["error"] ?? string.Empty);
        }
        catch (FormatException)
        {
            error = "The TrayIcon process returned an invalid help capture error.";
        }

        capture.Complete(totalChunks, exitCode, timedOut, error);
        return true;
    }

    private static ConPtyHelpCaptureResult CreateError(string error) => new()
    {
        Error = error,
    };

    private sealed class PendingCapture
    {
        private readonly object sync = new();
        private readonly SortedDictionary<int, byte[]> chunks = [];

        public TaskCompletionSource<ConPtyHelpCaptureResult> Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public void AddChunk(int index, byte[] output)
        {
            lock (sync)
            {
                chunks[index] = output;
            }
        }

        public void Fail(string error) => Completion.TrySetResult(CreateError(error));

        public void Complete(int totalChunks, int exitCode, bool timedOut, string error)
        {
            byte[] output;
            lock (sync)
            {
                if (totalChunks < 0 || chunks.Count != totalChunks ||
                    Enumerable.Range(0, totalChunks).Any(index => !chunks.ContainsKey(index)))
                {
                    Completion.TrySetResult(CreateError(
                        "The help response was incomplete while crossing the application pipe."));
                    return;
                }

                int length = chunks.Values.Sum(chunk => chunk.Length);
                output = new byte[length];
                int offset = 0;
                foreach (byte[] chunk in chunks.OrderBy(item => item.Key).Select(item => item.Value))
                {
                    Buffer.BlockCopy(chunk, 0, output, offset, chunk.Length);
                    offset += chunk.Length;
                }
            }

            Completion.TrySetResult(new ConPtyHelpCaptureResult
            {
                Output = Encoding.UTF8.GetString(output),
                ExitCode = exitCode,
                TimedOut = timedOut,
                Error = error,
            });
        }
    }
}
