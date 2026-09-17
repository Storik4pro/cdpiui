using System.Globalization;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

public sealed class CurlBlockCheckProbeRunnerOptions
{
    public string Executable { get; init; } = "curl.exe";
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public string UserAgent { get; init; } = "CDPIUI-BlockCheck2/1.0";
    public bool UseHeadForTls { get; init; }
    public bool UseHeadForQuic { get; init; } = true;
}

public sealed class CurlCapabilities
{
    public bool IsAvailable { get; init; }
    public bool SupportsTlsMax { get; init; }
    public bool SupportsTls13Option { get; init; }
    public bool SupportsHttp3 { get; init; }
    public string Version { get; init; } = string.Empty;
    public string Diagnostic { get; init; } = string.Empty;
}

public sealed class CurlBlockCheckProbeRunner : IBlockCheckProbeRunner, IBlockCheckProbePreflight
{
    private const string WriteOutMarker = "__CDPIUI_BLOCKCHECK2__";
    private const string WriteOutFormat = WriteOutMarker + "%{http_code}|%{time_starttransfer}";

    private readonly CurlBlockCheckProbeRunnerOptions _options;
    private readonly ICommandExecutor _executor;
    private readonly SemaphoreSlim _capabilityLock = new(1, 1);
    private CurlCapabilities? _capabilities;

    public CurlBlockCheckProbeRunner(
        CurlBlockCheckProbeRunnerOptions? options = null,
        ICommandExecutor? executor = null)
    {
        _options = options ?? new CurlBlockCheckProbeRunnerOptions();
        _executor = executor ?? new ProcessCommandExecutor();
    }

    public async Task<IReadOnlyList<BlockCheckIssue>> CheckAsync(
        IEnumerable<BlockCheckTarget> targets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        BlockCheckTarget[] targetArray = targets.ToArray();
        CurlCapabilities capabilities = await DetectCapabilitiesAsync(cancellationToken)
            .ConfigureAwait(false);
        List<BlockCheckIssue> issues = [];

        if (!capabilities.IsAvailable)
        {
            issues.Add(Error(
                "CURL_NOT_AVAILABLE",
                $"curl could not be started: {capabilities.Diagnostic}"));
            return issues;
        }

        if (targetArray.Any(target => target.Protocol is BlockCheckProtocol.Tls12 or BlockCheckProtocol.Tls13) &&
            !capabilities.SupportsTlsMax)
        {
            issues.Add(Error(
                "CURL_TLS_MAX_UNSUPPORTED",
                "curl does not support --tls-max, so exact TLS versions cannot be verified."));
        }

        if (targetArray.Any(target => target.Protocol == BlockCheckProtocol.Tls13) &&
            !capabilities.SupportsTls13Option)
        {
            issues.Add(Error(
                "CURL_TLS13_UNSUPPORTED",
                "curl does not recognize the TLS 1.3 options required by BlockCheck."));
        }

        if (targetArray.Any(target => target.Protocol == BlockCheckProtocol.Quic) &&
            !capabilities.SupportsHttp3)
        {
            issues.Add(Error(
                "CURL_HTTP3_UNSUPPORTED",
                "The installed curl build has no HTTP/3 support required for QUIC probes."));
        }

        return issues;
    }

    public async Task<CurlCapabilities> DetectCapabilitiesAsync(CancellationToken cancellationToken)
    {
        if (_capabilities != null)
        {
            return _capabilities;
        }

        await _capabilityLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_capabilities != null)
            {
                return _capabilities;
            }

            CommandExecutionResult version;
            try
            {
                version = await _executor.ExecuteAsync(
                        _options.Executable,
                        ["--version"],
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return _capabilities = new CurlCapabilities
                {
                    Diagnostic = exception.Message,
                };
            }

            if (version.ExitCode != 0)
            {
                return _capabilities = new CurlCapabilities
                {
                    Diagnostic = FirstUsefulLine(version.StandardError, version.StandardOutput),
                };
            }

            string versionText = version.StandardOutput;
            bool tlsMax = await RecognizesOptionsAsync(
                    ["--tls-max", "1.2"],
                    cancellationToken)
                .ConfigureAwait(false);
            bool tls13 = await RecognizesOptionsAsync(
                    ["--tlsv1.3", "--tls-max", "1.3"],
                    cancellationToken)
                .ConfigureAwait(false);

            return _capabilities = new CurlCapabilities
            {
                IsAvailable = true,
                SupportsTlsMax = tlsMax,
                SupportsTls13Option = tls13,
                SupportsHttp3 = versionText.Contains("HTTP3", StringComparison.OrdinalIgnoreCase),
                Version = versionText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? string.Empty,
            };
        }
        finally
        {
            _capabilityLock.Release();
        }
    }

    public async Task<ProbeAttempt> ProbeAsync(
        BlockCheckTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        IReadOnlyList<string> arguments = BuildArguments(target);
        CommandExecutionResult result;
        try
        {
            result = await _executor.ExecuteAsync(
                    _options.Executable,
                    arguments,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ProbeAttempt
            {
                Success = false,
                TimeToFirstByteMs = -1,
                ExitCode = -1,
                FailureCode = "curl-start-failed",
                Diagnostic = LimitDiagnostic(exception.Message),
            };
        }

        (int statusCode, double timeToFirstByteMs) = ParseWriteOut(result.StandardOutput);
        bool success = result.ExitCode == 0 &&
            statusCode > 0 &&
            !(target.Protocol == BlockCheckProtocol.Http && statusCode == 400);
        string diagnostic = FirstUsefulLine(result.StandardError);
        if (!success && diagnostic.Length == 0 &&
            !result.StandardOutput.Contains(WriteOutMarker, StringComparison.Ordinal))
        {
            diagnostic = FirstUsefulLine(result.StandardOutput);
        }

        return new ProbeAttempt
        {
            Success = success,
            TimeToFirstByteMs = success ? timeToFirstByteMs : -1,
            ExitCode = result.ExitCode,
            HttpStatusCode = statusCode,
            FailureCode = success ? string.Empty : GetFailureCode(result.ExitCode, statusCode, target.Protocol),
            Diagnostic = LimitDiagnostic(diagnostic),
        };
    }

    public IReadOnlyList<string> BuildArguments(BlockCheckTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        string scheme = target.Protocol == BlockCheckProtocol.Http ? "http" : "https";
        string url = $"{scheme}://{BlockCheckTarget.NormalizeHost(target.Host)}:{target.Port}{target.Path}";

        List<string> arguments =
        [
            "--silent",
            "--show-error",
            "--connect-timeout", FormatSeconds(_options.ConnectTimeout),
            "--max-time", FormatSeconds(_options.RequestTimeout),
            "--output", "NUL",
            "--write-out", WriteOutFormat,
            "--user-agent", _options.UserAgent,
            target.IpVersion == BlockCheckIpVersion.IPv4 ? "-4" : "-6",
        ];

        switch (target.Protocol)
        {
            case BlockCheckProtocol.Http:
                arguments.Add("--http1.1");
                break;
            case BlockCheckProtocol.Tls12:
                if (_options.UseHeadForTls)
                {
                    arguments.Add("--head");
                }
                arguments.Add("--http1.1");
                arguments.AddRange(["--tlsv1.2", "--tls-max", "1.2"]);
                break;
            case BlockCheckProtocol.Tls13:
                if (_options.UseHeadForTls)
                {
                    arguments.Add("--head");
                }
                arguments.Add("--http1.1");
                arguments.AddRange(["--tlsv1.3", "--tls-max", "1.3"]);
                break;
            case BlockCheckProtocol.TlsAuto:
                if (_options.UseHeadForTls)
                {
                    arguments.Add("--head");
                }
                // Let curl/Schannel negotiate both TLS and ALPN instead of forcing an
                // exact TLS version or HTTP/1.1. This is closer to a normal HTTPS
                // client, but it is deliberately not presented as a real browser probe.
                break;
            case BlockCheckProtocol.Quic:
                if (_options.UseHeadForQuic)
                {
                    arguments.Add("--head");
                }
                arguments.Add("--http3-only");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target.Protocol));
        }

        arguments.Add(url);
        return arguments;
    }

    private async Task<bool> RecognizesOptionsAsync(
        IReadOnlyList<string> options,
        CancellationToken cancellationToken)
    {
        List<string> arguments =
        [
            .. options,
            "--max-time", "1",
            "--output", "NUL",
            "http://127.0.0.1:1",
        ];
        CommandExecutionResult result = await _executor.ExecuteAsync(
                _options.Executable,
                arguments,
                cancellationToken)
            .ConfigureAwait(false);
        string error = result.StandardError;
        return result.ExitCode != 2 &&
               !error.Contains("unknown option", StringComparison.OrdinalIgnoreCase) &&
               !error.Contains("unsupported option", StringComparison.OrdinalIgnoreCase);
    }

    private static (int StatusCode, double TimeToFirstByteMs) ParseWriteOut(string output)
    {
        int marker = output.LastIndexOf(WriteOutMarker, StringComparison.Ordinal);
        if (marker < 0)
        {
            return (0, -1);
        }

        string[] values = output[(marker + WriteOutMarker.Length)..]
            .Trim()
            .Split('|', 2);
        int.TryParse(values.ElementAtOrDefault(0), NumberStyles.None, CultureInfo.InvariantCulture, out int status);
        bool hasTiming = double.TryParse(
            values.ElementAtOrDefault(1),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double seconds);
        return (status, hasTiming ? seconds * 1000d : -1);
    }

    private static string GetFailureCode(
        int exitCode,
        int statusCode,
        BlockCheckProtocol protocol)
    {
        if (protocol == BlockCheckProtocol.Http && statusCode == 400)
        {
            return "http-400-possible-fake-leak";
        }
        if (exitCode == 0 && statusCode == 0)
        {
            return "http-status-missing";
        }

        return exitCode switch
        {
            2 or 4 => "curl-feature-unsupported",
            5 => "proxy-resolution-failed",
            6 => "dns-resolution-failed",
            7 => "connection-failed",
            28 => "timeout",
            35 => "tls-handshake-failed",
            60 => "certificate-validation-failed",
            _ => exitCode == 0 ? $"http-{statusCode}" : $"curl-exit-{exitCode}",
        };
    }

    private static string FormatSeconds(TimeSpan value) =>
        Math.Max(0.1d, value.TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture);

    private static string FirstUsefulLine(params string[] values) =>
        values.SelectMany(value => (value ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim())
            .FirstOrDefault(value => value.Length > 0) ?? string.Empty;

    private static string LimitDiagnostic(string value) =>
        (value ?? string.Empty).Length <= 1024 ? value ?? string.Empty : value[..1024];

    private static BlockCheckIssue Error(string code, string message) =>
        new(BlockCheckIssueSeverity.Error, code, message);
}
