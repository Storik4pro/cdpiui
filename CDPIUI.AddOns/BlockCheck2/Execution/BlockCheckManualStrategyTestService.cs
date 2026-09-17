using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Synthesis;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConvertionService;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

public sealed class BlockCheckManualStrategyTestService
{
    private readonly Zapret2ConfigWriter _writer;

    public BlockCheckManualStrategyTestService(Zapret2ConfigWriter? writer = null)
    {
        _writer = writer ?? new Zapret2ConfigWriter();
    }

    public async Task<BlockCheckManualStrategyTestResult> TestAsync(
        StrategyDefinition strategy,
        BlockCheckTarget target,
        IBlockCheckStrategyRunner strategyRunner,
        IBlockCheckProbeRunner probeRunner,
        BlockCheckManualStrategyTestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(strategyRunner);
        ArgumentNullException.ThrowIfNull(probeRunner);
        options ??= new BlockCheckManualStrategyTestOptions();
        List<BlockCheckIssue> issues = [];
        if (options.Attempts < 1)
        {
            issues.Add(Error(
                "MANUAL_TEST_ATTEMPTS_INVALID",
                "Manual strategy test attempt count must be at least one."));
        }
        if (!strategy.AppliesTo(target))
        {
            issues.Add(Error(
                "MANUAL_TEST_STRATEGY_INAPPLICABLE",
                "The selected strategy does not support this connection.",
                strategy.Id));
        }
        if (issues.Count > 0)
        {
            return new BlockCheckManualStrategyTestResult { Issues = issues };
        }

        Zapret2ProfilePlan profile = new()
        {
            Name = "bc_manual_test",
            Filter = new Zapret2ProfileFilter
            {
                IpVersion = target.IpVersion,
                Transport = target.Transport,
                Port = target.Port,
                Layer7Protocol = target.Layer7Protocol,
                Domains = [BlockCheckTarget.NormalizeHost(target.Host)],
            },
            Primary = strategy,
            TargetIds = new HashSet<string>([target.Id], StringComparer.Ordinal),
        };
        Zapret2WriteResult configuration = _writer.Write([profile], options.WriterOptions);
        issues.AddRange(configuration.Issues);
        if (!configuration.Success)
        {
            return new BlockCheckManualStrategyTestResult
            {
                CommandLine = configuration.CommandLine,
                Issues = issues,
            };
        }

        List<ProbeAttempt> attempts = [];
        bool prepareAttempted = false;
        bool startAttempted = false;
        try
        {
            prepareAttempted = true;
            await strategyRunner.PrepareAsync(cancellationToken).ConfigureAwait(false);
            startAttempted = true;
            await strategyRunner.StartAsync(configuration.CommandLine, cancellationToken)
                .ConfigureAwait(false);
            for (int attempt = 0; attempt < options.Attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempts.Add(await probeRunner.ProbeAsync(target, cancellationToken)
                    .ConfigureAwait(false));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            issues.Add(Error(
                startAttempted ? "MANUAL_TEST_FAILED" : "MANUAL_TEST_PREPARE_FAILED",
                $"The strategy test could not be completed: {exception.Message}",
                strategy.Id));
        }
        finally
        {
            if (startAttempted)
            {
                try
                {
                    await strategyRunner.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    issues.Add(Error(
                        "MANUAL_TEST_STOP_FAILED",
                        $"The temporary strategy could not be stopped: {exception.Message}",
                        strategy.Id));
                }
            }
            if (prepareAttempted)
            {
                try
                {
                    await strategyRunner.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    issues.Add(Error(
                        "MANUAL_TEST_RESTORE_FAILED",
                        $"The previous component state could not be restored: {exception.Message}",
                        strategy.Id));
                }
            }
        }

        return new BlockCheckManualStrategyTestResult
        {
            CommandLine = configuration.CommandLine,
            ProbeResult = new ProbeResult
            {
                StrategyId = strategy.Id,
                TargetId = target.Id,
                Attempts = attempts,
            },
            Issues = issues,
        };
    }

    public OperationResultModel<string> GetFullStrategyArguments(
        StrategyDefinition strategy,
        BlockCheckTarget target,
        BlockCheckManualStrategyTestOptions? options = null)
    {
        options ??= new BlockCheckManualStrategyTestOptions();
        List<BlockCheckIssue> issues = [];

        Zapret2ProfilePlan profile = new()
        {
            Name = "bc_manual_test",
            Filter = new Zapret2ProfileFilter
            {
                IpVersion = target.IpVersion,
                Transport = target.Transport,
                Port = target.Port,
                Layer7Protocol = target.Layer7Protocol,
                Domains = [BlockCheckTarget.NormalizeHost(target.Host)],
            },
            Primary = strategy,
            TargetIds = new HashSet<string>([target.Id], StringComparer.Ordinal),
        };
        Zapret2WriteResult configuration = _writer.Write([profile], options.WriterOptions);
        issues.AddRange(configuration.Issues);
        if (!configuration.Success)
        {
            return OperationResultModel<string>.FailureResult(ErrorModel.OnlyErrorCode(issues.First().Code));
        }

        return OperationResultModel<string>.SuccessResult(configuration.CommandLine);
    }

    public Task<BlockCheckManualStrategyTestResult> TestWithCdpiuiAdaptersAsync(
        StrategyDefinition strategy,
        BlockCheckTarget target,
        BlockCheckManualStrategyTestOptions? options = null,
        CdpiuiZapret2StrategyRunnerOptions? strategyRunnerOptions = null,
        CurlBlockCheckProbeRunnerOptions? probeRunnerOptions = null,
        CancellationToken cancellationToken = default) =>
        TestAsync(
            strategy,
            target,
            new CdpiuiZapret2StrategyRunner(strategyRunnerOptions),
            new CurlBlockCheckProbeRunner(probeRunnerOptions),
            options,
            cancellationToken);

    private static BlockCheckIssue Error(string code, string message, string? subjectId = null) =>
        new(BlockCheckIssueSeverity.Error, code, message, subjectId);
}
