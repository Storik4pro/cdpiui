namespace CDPIUI.TrayIcon.Helper
{
    internal static class StartupActionDispatcher
    {
        internal static async Task DispatchAsync(
            Func<Task<bool>> send,
            Action<string> log,
            CancellationToken cancellationToken,
            TimeSpan? retryInterval = null)
        {
            int attempt = 0;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    attempt++;
                    try
                    {
                        if (await send())
                        {
                            log($"Startup request dispatched on attempt {attempt}. Delivery is not an execution acknowledgement.");
                            return;
                        }
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        if (attempt == 1 || attempt % 12 == 0)
                            log($"Startup dispatch attempt {attempt} failed: {ex}");
                    }

                    if (attempt == 1 || attempt % 12 == 0)
                        log($"Startup request pending (attempt {attempt}); PIPE or desktop-user launch unavailable. Will retry.");

                    await Task.Delay(retryInterval ?? TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // pass
            }
        }
    }
}
