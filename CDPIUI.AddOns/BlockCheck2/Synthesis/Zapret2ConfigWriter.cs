using System.Text.RegularExpressions;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Synthesis;

public sealed partial class Zapret2ConfigWriter
{
    public string FormatStrategyActions(StrategyDefinition strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        List<string> output = [];
        foreach (BlobDefinition blob in strategy.Blobs
                     .OrderBy(blob => blob.Name, StringComparer.Ordinal))
        {
            output.Add(FormatOption("blob", $"{blob.Name}:{blob.Source}"));
        }
        AppendStrategyActions(output, strategy, strategyNumber: null, isFinal: false);
        return string.Join(' ', output);
    }

    public Zapret2WriteResult Write(
        IEnumerable<Zapret2ProfilePlan> profiles,
        Zapret2WriterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        options ??= new Zapret2WriterOptions();

        Zapret2ProfilePlan[] profileArray = profiles.ToArray();
        List<BlockCheckIssue> issues = [];
        if (profileArray.Length == 0)
        {
            issues.Add(new BlockCheckIssue(
                BlockCheckIssueSeverity.Error,
                "NO_PROFILES",
                "No Zapret2 profiles were generated."));
            return new Zapret2WriteResult { Issues = issues };
        }

        Dictionary<string, BlobDefinition> blobs = CollectBlobs(profileArray, issues);
        if (issues.Any(issue => issue.Severity == BlockCheckIssueSeverity.Error))
        {
            return new Zapret2WriteResult { Issues = issues };
        }

        List<string> output = [];
        AppendWinDivertFilters(output, profileArray);

        foreach (BlobDefinition blob in blobs.Values.OrderBy(blob => blob.Name, StringComparer.Ordinal))
        {
            output.Add(FormatOption("blob", $"{blob.Name}:{blob.Source}"));
        }

        output.Add(FormatOption("lua-init", $"@{options.ZapretLibraryPath}"));
        output.Add(FormatOption("lua-init", $"@{options.ZapretAntiDpiLibraryPath}"));
        if (profileArray.Any(profile => profile.UsesCircular))
        {
            output.Add(FormatOption("lua-init", $"@{options.ZapretAutoLibraryPath}"));
        }

        for (int index = 0; index < profileArray.Length; index++)
        {
            if (index > 0)
            {
                output.Add("--new");
            }
            AppendProfile(output, profileArray[index], options, issues);
        }

        return new Zapret2WriteResult
        {
            CommandLine = string.Join(' ', output),
            Issues = issues,
        };
    }

    private static Dictionary<string, BlobDefinition> CollectBlobs(
        IEnumerable<Zapret2ProfilePlan> profiles,
        ICollection<BlockCheckIssue> issues)
    {
        Dictionary<string, BlobDefinition> blobs = new(StringComparer.Ordinal);
        foreach (BlobDefinition blob in profiles
                     .SelectMany(profile => profile.EnumerateStrategies())
                     .SelectMany(strategy => strategy.Blobs))
        {
            if (blobs.TryGetValue(blob.Name, out BlobDefinition? existing) &&
                !string.Equals(existing.Source, blob.Source, StringComparison.Ordinal))
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Error,
                    "BLOB_CONFLICT",
                    $"Blob '{blob.Name}' has conflicting sources.",
                    blob.Name));
                continue;
            }
            blobs[blob.Name] = blob;
        }
        return blobs;
    }

    private static void AppendWinDivertFilters(
        ICollection<string> output,
        IReadOnlyList<Zapret2ProfilePlan> profiles)
    {
        int[] tcpOut = profiles
            .Where(profile => profile.Filter.Transport == BlockCheckTransport.Tcp)
            .Select(profile => profile.Filter.Port)
            .Distinct()
            .Order()
            .ToArray();
        int[] udpOut = profiles
            .Where(profile => profile.Filter.Transport == BlockCheckTransport.Udp)
            .Select(profile => profile.Filter.Port)
            .Distinct()
            .Order()
            .ToArray();
        int[] tcpIn = profiles
            .Where(profile =>
                profile.Filter.Transport == BlockCheckTransport.Tcp &&
                (profile.UsesCircular || profile.EnumerateStrategies().Any(strategy => strategy.RequiresInboundTraffic)))
            .Select(profile => profile.Filter.Port)
            .Distinct()
            .Order()
            .ToArray();
        int[] udpIn = profiles
            .Where(profile =>
                profile.Filter.Transport == BlockCheckTransport.Udp &&
                (profile.UsesCircular || profile.EnumerateStrategies().Any(strategy => strategy.RequiresInboundTraffic)))
            .Select(profile => profile.Filter.Port)
            .Distinct()
            .Order()
            .ToArray();

        AppendPorts(output, "wf-tcp-out", tcpOut);
        AppendPorts(output, "wf-tcp-in", tcpIn);
        AppendPorts(output, "wf-udp-out", udpOut);
        AppendPorts(output, "wf-udp-in", udpIn);
    }

    private static void AppendPorts(ICollection<string> output, string name, IReadOnlyList<int> ports)
    {
        if (ports.Count > 0)
        {
            output.Add(FormatOption(name, string.Join(',', ports)));
        }
    }

    private static void AppendProfile(
        ICollection<string> output,
        Zapret2ProfilePlan profile,
        Zapret2WriterOptions options,
        ICollection<BlockCheckIssue> issues)
    {
        output.Add(FormatOption(
            "filter-l3",
            profile.Filter.IpVersion == BlockCheckIpVersion.IPv4 ? "ipv4" : "ipv6"));
        output.Add(FormatOption(
            profile.Filter.Transport == BlockCheckTransport.Tcp ? "filter-tcp" : "filter-udp",
            profile.Filter.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        output.Add(FormatOption("filter-l7", profile.Filter.Layer7Protocol));
        foreach (string hostListPath in profile.Filter.HostListPaths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            output.Add(FormatOption("hostlist", hostListPath, forceQuote: true));
        }
        if (profile.Filter.Domains.Count > 0)
        {
            output.Add(FormatOption("hostlist-domains", string.Join(',', profile.Filter.Domains)));
        }
        output.Add(FormatOption("name", SanitizeProfileName(profile.Name)));

        if (!profile.UsesCircular)
        {
            AppendStrategyActions(output, profile.Primary, strategyNumber: null, isFinal: false);
            return;
        }

        output.Add("--payload=all");
        output.Add("--in-range=-s5556");
        output.Add("--out-range=-s34228");
        output.Add(FormatLuaDesync(
            "circular",
            [
                new LuaArgumentDefinition { Name = "fails", Value = Math.Max(1, options.CircularFailureThreshold).ToString() },
                new LuaArgumentDefinition { Name = "maxseq", Value = "32768" },
                new LuaArgumentDefinition { Name = "inseq", Value = "4096" },
            ]));

        StrategyDefinition[] strategies = profile.EnumerateStrategies().ToArray();
        for (int index = 0; index < strategies.Length; index++)
        {
            bool isFinal = options.MakeLastFallbackFinal && index == strategies.Length - 1 && index > 0;
            if (strategies[index].Actions.Count == 0)
            {
                issues.Add(new BlockCheckIssue(
                    BlockCheckIssueSeverity.Error,
                    "CIRCULAR_STRATEGY_EMPTY",
                    "A circular strategy contains no Lua actions.",
                    strategies[index].Id));
                continue;
            }
            AppendStrategyActions(output, strategies[index], index + 1, isFinal);
        }
    }

    private static void AppendStrategyActions(
        ICollection<string> output,
        StrategyDefinition strategy,
        int? strategyNumber,
        bool isFinal)
    {
        for (int index = 0; index < strategy.Actions.Count; index++)
        {
            LuaActionDefinition action = strategy.Actions[index];

            // Emit all sticky filters for every action. The command is longer, but no
            // action can accidentally inherit payload/range state from another plan.
            output.Add(FormatOption("payload", string.Join(',', action.Payloads)));
            output.Add(FormatOption("in-range", action.InRange));
            output.Add(FormatOption("out-range", action.OutRange));

            List<LuaArgumentDefinition> arguments = [.. action.Arguments];
            if (strategyNumber.HasValue)
            {
                arguments.Add(new LuaArgumentDefinition
                {
                    Name = "strategy",
                    Value = strategyNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
                if (isFinal && index == 0)
                {
                    arguments.Add(new LuaArgumentDefinition { Name = "final" });
                }
            }

            output.Add(FormatLuaDesync(action.Function, arguments));
        }
    }

    private static string FormatLuaDesync(
        string function,
        IEnumerable<LuaArgumentDefinition> arguments)
    {
        string[] values = arguments
            .Select(argument => argument.Value == null
                ? argument.Name
                : $"{argument.Name}={argument.Value}")
            .ToArray();

        string value = values.Length == 0
            ? function
            : $"{function}:{string.Join(':', values)}";
        return FormatOption("lua-desync", value);
    }

    private static string FormatOption(string name, string value, bool forceQuote = false)
    {
        bool mustQuote = forceQuote || value.Any(char.IsWhiteSpace) || value.Contains('"');
        string escaped = value.Replace("\"", "\\\"");
        return mustQuote ? $"--{name}=\"{escaped}\"" : $"--{name}={escaped}";
    }

    private static string SanitizeProfileName(string name)
    {
        string sanitized = ProfileNameRegex().Replace(name ?? string.Empty, "_").Trim('_');
        return string.IsNullOrEmpty(sanitized) ? "bc_profile" : sanitized;
    }

    [GeneratedRegex("[^A-Za-z0-9_.-]+", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileNameRegex();
}
