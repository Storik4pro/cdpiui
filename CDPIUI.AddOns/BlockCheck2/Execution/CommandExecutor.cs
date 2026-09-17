using System.Diagnostics;
using System.Text;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

public sealed record CommandExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface ICommandExecutor
{
    Task<CommandExecutionResult> ExecuteAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}

public sealed class ProcessCommandExecutor : ICommandExecutor
{
    public async Task<CommandExecutionResult> ExecuteAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        ProcessStartInfo startInfo = new(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Preserve the original cancellation.
            }
            throw;
        }

        return new CommandExecutionResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }
}
