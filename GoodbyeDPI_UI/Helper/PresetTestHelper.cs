using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.Static;
using WinUI3Localizer;

namespace CDPI_UI.Helper
{
    public enum PresetTestType
    {
        Standard,
        DpiChecker
    }

    public enum TestLogColor
    {
        Default,
        Green,
        Cyan,
        Yellow,
        Red,
        Gray,
        White
    }

    public class TestLogSegment
    {
        public string Text;
        public TestLogColor Color;

        public TestLogSegment(string text, TestLogColor color = TestLogColor.Default)
        {
            Text = text;
            Color = color;
        }
    }

    public class PresetTestTarget
    {
        public string Name;
        public string Url;
        public string PingHost;

        public bool IsPing => string.IsNullOrEmpty(Url);
    }

    public class PresetTestResult
    {
        public ConfigItem Preset;
        public string PresetName;
        public string PackName;
        public int HttpOk;
        public int HttpErr;
        public int Unsupported;
        public int PingOk;
        public int PingFail;
        public int DpiOk;
        public int DpiFail;
        public int DpiUnsupported;
        public int DpiBlocked;
        public bool Failed;

        public int GetScore(PresetTestType testType)
        {
            if (Failed) return int.MinValue;
            if (testType == PresetTestType.Standard)
                return (HttpOk * 1000) + (PingOk * 10) - (HttpErr * 100) - (PingFail * 5);
            return (DpiOk * 1000) - (DpiBlocked * 5000) - (DpiFail * 10);
        }

        public string GetMetricsSummary(PresetTestType testType, ILocalizer localizer)
        {
            if (Failed)
                return localizer.GetLocalizedString("PT_FailedToStart");

            if (testType == PresetTestType.Standard)
            {
                return string.Format(localizer.GetLocalizedString("PT_MetricsStandard"),
                    HttpOk, PingOk, HttpErr);
            }

            return string.Format(localizer.GetLocalizedString("PT_MetricsDpi"),
                DpiOk, DpiBlocked, DpiFail);
        }
    }

    public class PresetTestProgress
    {
        public int CompletedPresets;
        public int TotalPresets;
        public int CurrentIndex;
        public string CurrentPresetName;
        public double Percent;
        public TimeSpan? EstimatedRemaining;
    }

    public class PresetTestRunResult
    {
        public IReadOnlyList<PresetTestResult> Ranked { get; init; } = Array.Empty<PresetTestResult>();
        public PresetTestResult Best { get; init; }
        public bool WasCancelled { get; init; }
    }

    public class PresetTestHelper
    {
        private const int StartWaitMs = 5000;
        private const int StopWaitMs = 1500;
        private const int CurlTimeoutSeconds = 5;
        private const int DpiRangeBytes = 65536;
        private const int MaxParallel = 8;
        private const string DpiSuiteUrl = "https://hyperion-cs.github.io/dpi-checkers/ru/tcp-16-20/suite.v2.json";

        private readonly object _curlLock = new();
        private readonly List<Process> _activeCurls = new();

        private static readonly Regex CertRegex = new(
            "Could not resolve host|certificate|SSL certificate problem|self[- ]?signed|certificate verify failed|unable to get local issuer certificate",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UnsupportedRegex = new(
            "does not support|not supported|unsupported protocol|unsupported option|unsupported feature|Unrecognized option|Unknown option|schannel",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DpiMetricsRegex = new(
            @"^(?<code>\d{3})\s+(?<up>\d+)\s+(?<down>\d+)\s+(?<time>[\d\.]+)$",
            RegexOptions.Compiled);

        private SemaphoreSlim _gate;

        private ILocalizer L => Localizer.Get();
        private string S(string key) => L.GetLocalizedString(key);

        #region targets file

        public static string GetTargetsFilePath()
        {
            return Path.Combine(StateHelper.GetDataDirectory(), StateHelper.SettingsDir, "test_targets.txt");
        }

        public static void EnsureTargetsFile()
        {
            string path = GetTargetsFilePath();
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (!File.Exists(path))
                    File.WriteAllText(path, GetDefaultTargetsContent(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(PresetTestHelper), $"Can't create targets file: {ex.Message}");
            }
        }

        private static string GetDefaultTargetsContent()
        {
            return
                "# test_targets.txt - endpoints checked for each preset (standard mode)" + Environment.NewLine +
                "# Format:  Name = \"https://host\"  (HTTPS+ping)  or  Name = \"PING:1.2.3.4\"  (ping only)" + Environment.NewLine +
                Environment.NewLine +
                "DiscordMain          = \"https://discord.com\"" + Environment.NewLine +
                "DiscordGateway       = \"https://gateway.discord.gg\"" + Environment.NewLine +
                "DiscordCDN           = \"https://cdn.discordapp.com\"" + Environment.NewLine +
                "DiscordUpdates       = \"https://updates.discord.com\"" + Environment.NewLine +
                "YouTubeWeb           = \"https://www.youtube.com\"" + Environment.NewLine +
                "YouTubeShort         = \"https://youtu.be\"" + Environment.NewLine +
                "YouTubeImage         = \"https://i.ytimg.com\"" + Environment.NewLine +
                "YouTubeVideoRedirect = \"https://redirector.googlevideo.com\"" + Environment.NewLine +
                "GoogleMain           = \"https://www.google.com\"" + Environment.NewLine +
                "GoogleGstatic        = \"https://www.gstatic.com\"" + Environment.NewLine +
                "CloudflareWeb        = \"https://www.cloudflare.com\"" + Environment.NewLine +
                "CloudflareCDN        = \"https://cdnjs.cloudflare.com\"" + Environment.NewLine +
                "CloudflareDNS1111    = \"PING:1.1.1.1\"" + Environment.NewLine +
                "CloudflareDNS1001    = \"PING:1.0.0.1\"" + Environment.NewLine +
                "GoogleDNS8888        = \"PING:8.8.8.8\"" + Environment.NewLine +
                "GoogleDNS8844        = \"PING:8.8.4.4\"" + Environment.NewLine +
                "Quad9DNS9999         = \"PING:9.9.9.9\"" + Environment.NewLine;
        }

        public static List<PresetTestTarget> LoadTargets()
        {
            EnsureTargetsFile();
            var targets = new List<PresetTestTarget>();
            try
            {
                foreach (var rawLine in File.ReadAllLines(GetTargetsFilePath()))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;

                    string name = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim().Trim('"').Trim();

                    if (name.Length == 0 || value.Length == 0) continue;

                    if (value.StartsWith("PING:", StringComparison.OrdinalIgnoreCase))
                    {
                        targets.Add(new PresetTestTarget { Name = name, PingHost = value.Substring("PING:".Length).Trim() });
                    }
                    else
                    {
                        string host = value;
                        try { host = new Uri(value).Host; } catch { }
                        targets.Add(new PresetTestTarget { Name = name, Url = value, PingHost = host });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(PresetTestHelper), $"Can't read targets file: {ex.Message}");
            }
            return targets;
        }

        #endregion

        #region log helpers

        private static void Line(IProgress<List<TestLogSegment>> log, params TestLogSegment[] segments)
        {
            log.Report(segments.ToList());
        }

        private void Tagged(IProgress<List<TestLogSegment>> log, string tagKey, TestLogColor tagColor, string text)
        {
            Line(log,
                new TestLogSegment($"[{S(tagKey)}] ", tagColor),
                new TestLogSegment(text));
        }

        #endregion

        public void CancelAllCurls()
        {
            lock (_curlLock)
            {
                foreach (Process p in _activeCurls.ToList())
                {
                    try
                    {
                        if (p != null && !p.HasExited)
                            p.Kill(entireProcessTree: true);
                    }
                    catch { }
                }
                _activeCurls.Clear();
            }
        }

        public static PresetTestRunResult BuildRunResult(List<PresetTestResult> results, PresetTestType testType, bool wasCancelled)
        {
            var ranked = results
                .OrderByDescending(r => r.GetScore(testType))
                .ThenByDescending(r => r.PingOk)
                .ToList();

            PresetTestResult best = ranked.FirstOrDefault(r => !r.Failed && r.GetScore(testType) > int.MinValue + 1);

            return new PresetTestRunResult
            {
                Ranked = ranked,
                Best = best,
                WasCancelled = wasCancelled
            };
        }

        public static string GetPackDisplayName(ConfigItem preset)
        {
            if (preset == null || string.IsNullOrEmpty(preset.packId))
                return string.Empty;
            try
            {
                var item = DatabaseHelper.Instance.GetItemById(preset.packId);
                if (item != null && !string.IsNullOrEmpty(item.ShortName))
                    return item.ShortName;
            }
            catch { }
            return preset.packId;
        }

        public async Task<PresetTestRunResult> RunAsync(
            string componentId,
            ProcessManager processManager,
            PresetTestType testType,
            List<ConfigItem> presets,
            IProgress<List<TestLogSegment>> log,
            IProgress<PresetTestProgress> progress,
            CancellationToken ct)
        {
            var results = new List<PresetTestResult>();
            _gate = new SemaphoreSlim(MaxParallel);
            bool wasCancelled = false;
            var runStopwatch = Stopwatch.StartNew();

            if (!IsCurlAvailable())
            {
                Tagged(log, "PT_Warn", TestLogColor.Red, S("PT_NoCurl"));
                return BuildRunResult(results, testType, false);
            }
            Tagged(log, "PT_Ok", TestLogColor.Green, S("PT_CurlFound"));

            if (presets == null || presets.Count == 0)
            {
                Tagged(log, "PT_Warn", TestLogColor.Yellow, S("PT_NoPresets"));
                return BuildRunResult(results, testType, false);
            }

            // standard targets
            List<PresetTestTarget> targets = null;
            int maxNameLen = 10;
            if (testType == PresetTestType.Standard)
            {
                targets = LoadTargets();
                if (targets.Count == 0)
                {
                    Tagged(log, "PT_Warn", TestLogColor.Yellow, S("PT_NoTargets"));
                    return BuildRunResult(results, testType, false);
                }
                maxNameLen = Math.Max(10, targets.Max(t => t.Name.Length));
                Tagged(log, "PT_Info", TestLogColor.Cyan, string.Format(S("PT_TargetsLoaded"), targets.Count));
            }

            // dpi targets (fetched from online suite)
            List<DpiSuiteEntry> dpiTargets = null;
            string payloadFile = null;
            if (testType == PresetTestType.DpiChecker)
            {
                dpiTargets = await FetchDpiSuiteAsync(ct);
                if (dpiTargets == null || dpiTargets.Count == 0)
                {
                    Tagged(log, "PT_Warn", TestLogColor.Yellow, S("PT_DpiSuiteFailed"));
                    return BuildRunResult(results, testType, false);
                }
                Tagged(log, "PT_Info", TestLogColor.Cyan, string.Format(S("PT_DpiTargetsLoaded"), dpiTargets.Count));
                payloadFile = CreatePayloadFile();
            }

            Line(log, new TestLogSegment(""));
            Line(log, new TestLogSegment(new string('=', 60), TestLogColor.Gray));
            Line(log, new TestLogSegment("            " + S("PT_Header"), TestLogColor.White));
            Line(log, new TestLogSegment("            " + S("PT_ModeLabel") + " " +
                (testType == PresetTestType.Standard ? S("PT_ModeStandard") : S("PT_ModeDpi")), TestLogColor.White));
            Line(log, new TestLogSegment("            " + string.Format(S("PT_TotalConfigs"), presets.Count), TestLogColor.White));
            Line(log, new TestLogSegment(new string('=', 60), TestLogColor.Gray));
            Tagged(log, "PT_Warn", TestLogColor.Yellow, S("PT_MayTakeTime"));

            try
            {
                int index = 0;
                foreach (ConfigItem preset in presets)
                {
                    ct.ThrowIfCancellationRequested();
                    index++;

                    string presetName = string.IsNullOrEmpty(preset.name) ? (preset.file_name ?? $"#{index}") : preset.name;
                    ReportProgress(progress, index - 1, presets.Count, presetName, runStopwatch);

                    Line(log, new TestLogSegment(""));
                    Line(log, new TestLogSegment(new string('-', 60), TestLogColor.Gray));
                    Line(log, new TestLogSegment("  " + string.Format(S("PT_Preset"), index, presets.Count, presetName), TestLogColor.White));
                    Line(log, new TestLogSegment(new string('-', 60), TestLogColor.Gray));

                    var result = new PresetTestResult
                    {
                        Preset = preset,
                        PresetName = presetName,
                        PackName = GetPackDisplayName(preset)
                    };

                    await processManager.StopProcess(false);
                    await SafeDelay(StopWaitMs, ct);

                    string args = ConfigHelper.GetStartupParametersByConfigItem(preset);
                    if (string.IsNullOrWhiteSpace(args))
                    {
                        Line(log, new TestLogSegment("  > ", TestLogColor.Gray), new TestLogSegment(S("PT_Skipped"), TestLogColor.Yellow));
                        result.Failed = true;
                        results.Add(result);
                        continue;
                    }

                    Line(log, new TestLogSegment("  > " + S("PT_Starting"), TestLogColor.Cyan));
                    await processManager.StartProcess(componentId, args);
                    await SafeDelay(StartWaitMs, ct);

                    if (!processManager.processState || processManager.isErrorHappens)
                    {
                        Line(log, new TestLogSegment("  ", TestLogColor.Gray), new TestLogSegment(S("PT_FailedToStart"), TestLogColor.Red));
                        result.Failed = true;
                        results.Add(result);
                        continue;
                    }

                    Line(log, new TestLogSegment("  > " + S("PT_RunningTests"), TestLogColor.Gray));

                    if (testType == PresetTestType.Standard)
                        await RunStandardConfigAsync(targets, maxNameLen, result, log, ct);
                    else
                        await RunDpiConfigAsync(dpiTargets, payloadFile, result, log, ct);

                    results.Add(result);
                    ReportProgress(progress, index, presets.Count, presetName, runStopwatch);
                }

                await processManager.StopProcess(false);

                WriteSummary(results, testType, log);
                SaveResultsToFile(componentId, testType, results, log);
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                CancelAllCurls();
            }
            finally
            {
                CancelAllCurls();
                if (payloadFile != null)
                {
                    try { File.Delete(payloadFile); } catch { }
                }
            }

            return BuildRunResult(results, testType, wasCancelled);
        }

        private static void ReportProgress(IProgress<PresetTestProgress> progress, int completed, int total, string currentName, Stopwatch sw)
        {
            if (progress == null || total <= 0) return;

            TimeSpan? eta = null;
            if (completed > 0 && completed < total)
            {
                double avgMs = sw.Elapsed.TotalMilliseconds / completed;
                eta = TimeSpan.FromMilliseconds(avgMs * (total - completed));
            }

            progress.Report(new PresetTestProgress
            {
                CompletedPresets = completed,
                TotalPresets = total,
                CurrentIndex = Math.Min(completed + 1, total),
                CurrentPresetName = currentName ?? string.Empty,
                Percent = total > 0 ? (100.0 * completed / total) : 0,
                EstimatedRemaining = eta
            });
        }

        #region standard test

        private async Task RunStandardConfigAsync(List<PresetTestTarget> targets, int maxNameLen, PresetTestResult result, IProgress<List<TestLogSegment>> log, CancellationToken ct)
        {
            foreach (PresetTestTarget target in targets)
            {
                ct.ThrowIfCancellationRequested();
                StandardOutcome outcome = await CheckStandardTargetAsync(target, ct);

                var segments = new List<TestLogSegment>
                {
                    new TestLogSegment("    " + outcome.Name.PadRight(maxNameLen))
                };

                if (outcome.IsUrl)
                {
                    foreach (StandardToken token in outcome.Tokens)
                    {
                        segments.Add(new TestLogSegment(" | " + token.Label + ":", TestLogColor.Gray));
                        segments.Add(new TestLogSegment(token.StatusText, token.Color));
                    }
                    segments.Add(new TestLogSegment(" | " + S("PT_ColPing") + ": ", TestLogColor.Gray));
                    if (outcome.PingOk)
                        segments.Add(new TestLogSegment(string.Format(S("PT_Ms"), outcome.PingMs), TestLogColor.Cyan));
                    else
                        segments.Add(new TestLogSegment(S("PT_StatusTimeout"), TestLogColor.Yellow));
                }
                else
                {
                    segments.Add(new TestLogSegment(" " + S("PT_ColPing") + ": ", TestLogColor.Gray));
                    if (outcome.PingOk)
                        segments.Add(new TestLogSegment(string.Format(S("PT_Ms"), outcome.PingMs), TestLogColor.Cyan));
                    else
                        segments.Add(new TestLogSegment(S("PT_StatusTimeout"), TestLogColor.Red));
                }

                if (outcome.PingOk) result.PingOk++; else result.PingFail++;
                if (outcome.IsUrl)
                {
                    foreach (StandardToken token in outcome.Tokens)
                    {
                        switch (token.Kind)
                        {
                            case StdStatus.Ok: result.HttpOk++; break;
                            case StdStatus.Unsupported: result.Unsupported++; break;
                            default: result.HttpErr++; break;
                        }
                    }
                }

                log.Report(segments);
            }
        }

        private async Task<StandardOutcome> CheckStandardTargetAsync(PresetTestTarget target, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var outcome = new StandardOutcome { Name = target.Name, IsUrl = !target.IsPing, Tokens = new List<StandardToken>() };

                if (!target.IsPing)
                {
                    foreach (var test in StandardTests)
                    {
                        var argList = new List<string> { "-I", "-s", "-m", CurlTimeoutSeconds.ToString(), "-o", "NUL", "-w", "%{http_code}", "--show-error" };
                        argList.AddRange(test.Args);
                        argList.Add(target.Url);

                        var (exit, _, stderr) = await RunCurlAsync(argList, ct);

                        StdStatus kind;
                        if (CertRegex.IsMatch(stderr ?? string.Empty))
                            kind = StdStatus.Ssl;
                        else if (exit == 35 || UnsupportedRegex.IsMatch(stderr ?? string.Empty))
                            kind = StdStatus.Unsupported;
                        else if (exit == 0)
                            kind = StdStatus.Ok;
                        else
                            kind = StdStatus.Error;

                        outcome.Tokens.Add(MakeToken(test.Label, kind));
                    }
                }

                var (pingOk, pingMs) = await CheckPingAsync(target.PingHost, ct);
                outcome.PingOk = pingOk;
                outcome.PingMs = pingMs;
                return outcome;
            }
            finally
            {
                _gate.Release();
            }
        }

        private StandardToken MakeToken(string label, StdStatus kind)
        {
            return kind switch
            {
                StdStatus.Ok => new StandardToken { Label = label, Kind = kind, StatusText = S("PT_StatusOk"), Color = TestLogColor.Green },
                StdStatus.Unsupported => new StandardToken { Label = label, Kind = kind, StatusText = S("PT_StatusUnsupported"), Color = TestLogColor.Yellow },
                StdStatus.Ssl => new StandardToken { Label = label, Kind = kind, StatusText = S("PT_StatusSsl"), Color = TestLogColor.Red },
                _ => new StandardToken { Label = label, Kind = kind, StatusText = S("PT_StatusFail"), Color = TestLogColor.Red },
            };
        }

        #endregion

        #region dpi test

        private async Task RunDpiConfigAsync(List<DpiSuiteEntry> targets, string payloadFile, PresetTestResult result, IProgress<List<TestLogSegment>> log, CancellationToken ct)
        {
            string rangeSpec = $"0-{DpiRangeBytes - 1}";

            bool warnDetected = false;
            foreach (DpiSuiteEntry target in targets)
            {
                ct.ThrowIfCancellationRequested();
                DpiOutcome outcome = await CheckDpiTargetAsync(target, payloadFile, rangeSpec, ct);

                Line(log, new TestLogSegment($"  === [{outcome.Country}][{outcome.Provider}] {outcome.Id} ===", TestLogColor.Cyan));
                foreach (DpiLine dl in outcome.Lines)
                {
                    Line(log,
                        new TestLogSegment("    " + dl.Label.PadRight(8) + " ", TestLogColor.Gray),
                        new TestLogSegment($"code={dl.Code} ", TestLogColor.Gray),
                        new TestLogSegment($"up={dl.UpKb:0.0}KB down={dl.DownKb:0.0}KB time={dl.Time:0.00}s ", TestLogColor.Gray),
                        new TestLogSegment(dl.StatusText, dl.Color));

                    switch (dl.Kind)
                    {
                        case DpiStatus.Ok: result.DpiOk++; break;
                        case DpiStatus.Unsupported: result.DpiUnsupported++; break;
                        case DpiStatus.Blocked: result.DpiBlocked++; break;
                        default: result.DpiFail++; break;
                    }
                }
                if (outcome.Warned)
                {
                    warnDetected = true;
                    Line(log, new TestLogSegment("    " + S("PT_DpiFreezeNote"), TestLogColor.Yellow));
                }
                else
                {
                    Line(log, new TestLogSegment("    " + S("PT_DpiNoFreeze"), TestLogColor.Green));
                }
            }

            Line(log, new TestLogSegment(""));
            if (warnDetected)
                Tagged(log, "PT_Warn", TestLogColor.Red, S("PT_DpiBlockedWarn"));
            else
                Tagged(log, "PT_Ok", TestLogColor.Green, S("PT_DpiOkAll"));
        }

        private async Task<DpiOutcome> CheckDpiTargetAsync(DpiSuiteEntry target, string payloadFile, string rangeSpec, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var outcome = new DpiOutcome { Id = target.id, Provider = target.provider, Country = target.country, Lines = new List<DpiLine>() };

                foreach (var test in StandardTests)
                {
                    var argList = new List<string>
                    {
                        "--range", rangeSpec,
                        "-m", CurlTimeoutSeconds.ToString(),
                        "-w", "%{http_code} %{size_upload} %{size_download} %{time_total}",
                        "-o", "NUL",
                        "-X", "POST",
                        "--data-binary", "@" + payloadFile,
                        "-s"
                    };
                    argList.AddRange(test.Args);
                    argList.Add($"https://{target.host}");

                    var (exit, stdout, stderr) = await RunCurlAsync(argList, ct);
                    string text = (stdout ?? string.Empty).Trim();

                    string code = "NA";
                    long up = 0, down = 0;
                    double time = -1;

                    Match m = DpiMetricsRegex.Match(text);
                    if (m.Success)
                    {
                        code = m.Groups["code"].Value;
                        up = long.Parse(m.Groups["up"].Value);
                        down = long.Parse(m.Groups["down"].Value);
                        time = double.Parse(m.Groups["time"].Value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else if (exit == 35 || UnsupportedRegex.IsMatch((stderr ?? string.Empty) + " " + text))
                    {
                        code = "UNSUP";
                    }
                    else if (text.Length > 0)
                    {
                        code = "ERR";
                    }

                    DpiStatus kind;
                    if (code == "UNSUP") kind = DpiStatus.Unsupported;
                    else if (exit != 0 || code == "ERR" || code == "NA") kind = DpiStatus.Fail;
                    else kind = DpiStatus.Ok;

                    bool warned = false;
                    if (up > 0 && down == 0 && time >= CurlTimeoutSeconds && exit != 0)
                    {
                        kind = DpiStatus.Blocked;
                        warned = true;
                    }

                    outcome.Lines.Add(MakeDpiLine(test.Label, code, up, down, time, kind));
                    if (warned) outcome.Warned = true;
                }

                return outcome;
            }
            finally
            {
                _gate.Release();
            }
        }

        private DpiLine MakeDpiLine(string label, string code, long up, long down, double time, DpiStatus kind)
        {
            (string text, TestLogColor color) = kind switch
            {
                DpiStatus.Ok => (S("PT_StatusOk"), TestLogColor.Green),
                DpiStatus.Unsupported => (S("PT_StatusUnsupported"), TestLogColor.Yellow),
                DpiStatus.Blocked => (S("PT_StatusBlocked"), TestLogColor.Yellow),
                _ => (S("PT_StatusFail"), TestLogColor.Red),
            };

            return new DpiLine
            {
                Label = label,
                Code = code,
                UpKb = Math.Round(up / 1024.0, 1),
                DownKb = Math.Round(down / 1024.0, 1),
                Time = time,
                Kind = kind,
                StatusText = text,
                Color = color
            };
        }

        private async Task<List<DpiSuiteEntry>> FetchDpiSuiteAsync(CancellationToken ct)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(CurlTimeoutSeconds + 2) };
                string json = await client.GetStringAsync(DpiSuiteUrl, ct);
                var entries = JsonSerializer.Deserialize<List<DpiSuiteEntry>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return entries?.Where(e => e != null && !string.IsNullOrEmpty(e.host)).ToList();
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateWarningLog(nameof(PresetTestHelper), $"Can't fetch DPI suite: {ex.Message}");
                return null;
            }
        }

        private static string CreatePayloadFile()
        {
            string file = Path.GetTempFileName();
            var payload = new byte[DpiRangeBytes];
            System.Security.Cryptography.RandomNumberGenerator.Fill(payload);
            File.WriteAllBytes(file, payload);
            return file;
        }

        #endregion

        #region summary

        private void WriteSummary(List<PresetTestResult> results, PresetTestType testType, IProgress<List<TestLogSegment>> log)
        {
            Line(log, new TestLogSegment(""));
            Line(log, new TestLogSegment(new string('=', 60), TestLogColor.Gray));
            Line(log, new TestLogSegment("=== " + S("PT_Analytics") + " ===", TestLogColor.White));
            Line(log, new TestLogSegment(new string('=', 60), TestLogColor.Gray));

            foreach (PresetTestResult r in results)
            {
                if (r.Failed)
                {
                    Line(log, new TestLogSegment("  " + r.PresetName.PadRight(28) + " ", TestLogColor.White),
                        new TestLogSegment(S("PT_FailedToStart"), TestLogColor.Red));
                    continue;
                }

                var segs = new List<TestLogSegment> { new TestLogSegment("  " + r.PresetName.PadRight(28) + "  ", TestLogColor.White) };
                if (testType == PresetTestType.Standard)
                {
                    segs.Add(new TestLogSegment(S("PT_AHttpOk") + " ", TestLogColor.Gray));
                    segs.Add(new TestLogSegment(r.HttpOk.ToString(), TestLogColor.Green));
                    segs.Add(new TestLogSegment(" | " + S("PT_AErr") + " ", TestLogColor.Gray));
                    segs.Add(new TestLogSegment(r.HttpErr.ToString(), r.HttpErr > 0 ? TestLogColor.Red : TestLogColor.Green));
                    segs.Add(new TestLogSegment(" | " + S("PT_AUnsupported") + " ", TestLogColor.Gray));
                    segs.Add(new TestLogSegment(r.Unsupported.ToString(), TestLogColor.Yellow));
                    segs.Add(new TestLogSegment(" | " + S("PT_APingOk") + " ", TestLogColor.Gray));
                    segs.Add(new TestLogSegment(r.PingOk.ToString(), TestLogColor.Cyan));
                    segs.Add(new TestLogSegment(" | " + S("PT_AFail") + " ", TestLogColor.Gray));
                    segs.Add(new TestLogSegment(r.PingFail.ToString(), r.PingFail > 0 ? TestLogColor.Yellow : TestLogColor.Green));
                }
                else
                {
                    segs.Add(new TestLogSegment(S("PT_ADpiOk") + " ", TestLogColor.Gray));
                    segs.Add(new TestLogSegment(r.DpiOk.ToString(), TestLogColor.Green));
                    segs.Add(new TestLogSegment(" | " + S("PT_ADpiFail") + " ", TestLogColor.Gray));
                    segs.Add(new TestLogSegment(r.DpiFail.ToString(), r.DpiFail > 0 ? TestLogColor.Red : TestLogColor.Green));
                    segs.Add(new TestLogSegment(" | " + S("PT_AUnsupported") + " ", TestLogColor.Gray));
                    segs.Add(new TestLogSegment(r.DpiUnsupported.ToString(), TestLogColor.Yellow));
                    segs.Add(new TestLogSegment(" | " + S("PT_ABlocked") + " ", TestLogColor.Gray));
                    segs.Add(new TestLogSegment(r.DpiBlocked.ToString(), r.DpiBlocked > 0 ? TestLogColor.Yellow : TestLogColor.Green));
                }
                log.Report(segs);
            }

            PresetTestResult best = results
                .Where(r => !r.Failed)
                .OrderByDescending(r => testType == PresetTestType.Standard ? r.HttpOk : r.DpiOk)
                .ThenByDescending(r => r.PingOk)
                .FirstOrDefault();

            Line(log, new TestLogSegment(""));
            bool hasWinner = best != null &&
                ((testType == PresetTestType.Standard && (best.HttpOk > 0 || best.PingOk > 0)) ||
                 (testType == PresetTestType.DpiChecker && best.DpiOk > 0));

            if (hasWinner)
                Line(log, new TestLogSegment(string.Format(S("PT_BestConfig"), best.PresetName), TestLogColor.Green));
            else
                Line(log, new TestLogSegment(S("PT_NoWorking"), TestLogColor.Yellow));
        }

        private void SaveResultsToFile(string componentId, PresetTestType testType, List<PresetTestResult> results, IProgress<List<TestLogSegment>> log)
        {
            try
            {
                string logsDir = Path.Combine(StateHelper.GetDataDirectory(), "Logs", "test results");
                if (!Directory.Exists(logsDir))
                    Directory.CreateDirectory(logsDir);

                string file = Path.Combine(logsDir, $"test_results_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                var sb = new StringBuilder();
                sb.AppendLine($"Preset test results for component {componentId} ({testType})");
                sb.AppendLine();
                foreach (PresetTestResult r in results)
                {
                    if (r.Failed)
                        sb.AppendLine($"{r.PresetName} : did not start");
                    else if (testType == PresetTestType.Standard)
                        sb.AppendLine($"{r.PresetName} : HTTP OK {r.HttpOk}, ERR {r.HttpErr}, UNSUP {r.Unsupported}, Ping OK {r.PingOk}, Fail {r.PingFail}");
                    else
                        sb.AppendLine($"{r.PresetName} : OK {r.DpiOk}, FAIL {r.DpiFail}, UNSUP {r.DpiUnsupported}, BLOCKED {r.DpiBlocked}");
                }

                PresetTestResult best = results
                    .Where(r => !r.Failed)
                    .OrderByDescending(r => testType == PresetTestType.Standard ? r.HttpOk : r.DpiOk)
                    .ThenByDescending(r => r.PingOk)
                    .FirstOrDefault();
                sb.AppendLine();
                sb.AppendLine($"Best config: {(best != null ? best.PresetName : "none")}");

                File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
                Tagged(log, "PT_Info", TestLogColor.Cyan, string.Format(S("PT_ResultsSaved"), file));
            }
            catch (Exception ex)
            {
                Logger.Instance.CreateErrorLog(nameof(PresetTestHelper), $"Can't save test results: {ex.Message}");
            }
        }

        #endregion

        #region low level helpers

        private static readonly (string Label, string[] Args)[] StandardTests =
        {
            ("HTTP", new[] { "--http1.1" }),
            ("TLS1.2", new[] { "--tlsv1.2", "--tls-max", "1.2" }),
            ("TLS1.3", new[] { "--tlsv1.3", "--tls-max", "1.3" }),
        };

        private static bool IsCurlAvailable()
        {
            try
            {
                string system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
                if (File.Exists(system32)) return true;

                string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (string dir in pathVar.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    try { if (File.Exists(Path.Combine(dir.Trim(), "curl.exe"))) return true; } catch { }
                }
            }
            catch { }
            return false;
        }

        private async Task<(int exit, string stdout, string stderr)> RunCurlAsync(IEnumerable<string> args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo("curl.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string a in args) psi.ArgumentList.Add(a);

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            lock (_curlLock) _activeCurls.Add(process);
            try
            {
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    return (-1, string.Empty, ex.Message);
                }

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
                Task<string> stderrTask = process.StandardError.ReadToEndAsync(ct);

                using (ct.Register(() =>
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                }))
                {
                    try
                    {
                        await process.WaitForExitAsync(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                        throw;
                    }
                }

                string stdout = await stdoutTask;
                string stderr = await stderrTask;
                return (process.ExitCode, stdout, stderr);
            }
            finally
            {
                lock (_curlLock) _activeCurls.Remove(process);
            }
        }

        private async Task<(bool ok, long ms)> CheckPingAsync(string host, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(host)) return (false, -1);
            try
            {
                using var ping = new Ping();
                long sum = 0;
                int ok = 0;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    PingReply reply = await ping.SendPingAsync(host, 3000);
                    if (reply.Status == IPStatus.Success)
                    {
                        sum += reply.RoundtripTime;
                        ok++;
                    }
                }
                if (ok > 0) return (true, sum / ok);
            }
            catch (OperationCanceledException) { throw; }
            catch { }
            return (false, -1);
        }

        private static async Task SafeDelay(int ms, CancellationToken ct)
        {
            try { await Task.Delay(ms, ct); }
            catch (TaskCanceledException) { throw new OperationCanceledException(ct); }
        }

        #endregion

        #region models

        private enum StdStatus { Ok, Error, Unsupported, Ssl }
        private enum DpiStatus { Ok, Fail, Unsupported, Blocked }

        private class StandardToken
        {
            public string Label;
            public StdStatus Kind;
            public string StatusText;
            public TestLogColor Color;
        }

        private class StandardOutcome
        {
            public string Name;
            public bool IsUrl;
            public List<StandardToken> Tokens;
            public bool PingOk;
            public long PingMs;
        }

        private class DpiLine
        {
            public string Label;
            public string Code;
            public double UpKb;
            public double DownKb;
            public double Time;
            public DpiStatus Kind;
            public string StatusText;
            public TestLogColor Color;
        }

        private class DpiOutcome
        {
            public string Id;
            public string Provider;
            public string Country;
            public List<DpiLine> Lines;
            public bool Warned;
        }

        private class DpiSuiteEntry
        {
            public string id { get; set; }
            public string provider { get; set; }
            public string country { get; set; }
            public string host { get; set; }
        }

        #endregion
    }
}