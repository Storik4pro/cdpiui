using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Synthesis;

public sealed class ProfilePlanner
{
    public IReadOnlyList<Zapret2ProfilePlan> Plan(
        CoverageSelectionResult selection,
        IEnumerable<BlockCheckTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(targets);

        Dictionary<string, BlockCheckTarget> targetsById = targets
            .ToDictionary(target => target.Id, StringComparer.Ordinal);

        var groups = selection.Assignments
            .SelectMany(assignment => assignment.TargetIds
                .Where(targetsById.ContainsKey)
                .Select(targetId => new AssignedTarget(
                    assignment.Candidate.Definition,
                    CandidateNormalizer.GetPlanSignature(assignment.Candidate.Definition),
                    targetsById[targetId],
                    assignment.IsBestEffort)))
            .GroupBy(item => new ProfileShape(
                item.PlanSignature,
                item.Target.IpVersion,
                item.Target.Transport,
                item.Target.Port,
                item.Target.Layer7Protocol,
                item.IsBestEffort));

        List<Zapret2ProfilePlan> profiles = [];
        foreach (var group in groups)
        {
            StrategyDefinition strategy = group
                .Select(item => item.Strategy)
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .First();
            string[] domains = group
                .Where(item => item.Target.HostListPaths.Count == 0)
                .Select(item => BlockCheckTarget.NormalizeHost(item.Target.Host))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(domain => domain, StringComparer.Ordinal)
                .ToArray();
            string[] hostListPaths = group
                .SelectMany(item => item.Target.HostListPaths)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            profiles.Add(new Zapret2ProfilePlan
            {
                Filter = new Zapret2ProfileFilter
                {
                    IpVersion = group.Key.IpVersion,
                    Transport = group.Key.Transport,
                    Port = group.Key.Port,
                    Layer7Protocol = group.Key.Layer7Protocol,
                    Domains = domains,
                    HostListPaths = hostListPaths,
                },
                Primary = strategy,
                TargetIds = group
                    .Select(item => item.Target.Id)
                    .ToHashSet(StringComparer.Ordinal),
                IsBestEffort = group.Key.IsBestEffort,
            });
        }

        Zapret2ProfilePlan[] ordered = profiles
            .OrderByDescending(GetDomainSpecificity)
            .ThenBy(profile => profile.Filter.Transport)
            .ThenBy(profile => profile.Filter.Port)
            .ThenBy(profile => profile.Filter.Layer7Protocol, StringComparer.Ordinal)
            .ThenBy(profile => profile.Filter.IpVersion)
            .ThenBy(profile => profile.Primary.Id, StringComparer.Ordinal)
            .ToArray();

        Dictionary<string, int> counters = new(StringComparer.Ordinal);
        foreach (Zapret2ProfilePlan profile in ordered)
        {
            string prefix = $"bc_{profile.Filter.Layer7Protocol}_{profile.Filter.IpVersion.ToString().ToLowerInvariant()}";
            counters.TryGetValue(prefix, out int count);
            counters[prefix] = ++count;
            profile.Name = $"{prefix}_{count}";
        }

        return ordered;
    }

    private static int GetDomainSpecificity(Zapret2ProfilePlan profile) =>
        profile.Filter.Domains.Count == 0
            ? 0
            : profile.Filter.Domains.Max(domain => domain.Count(character => character == '.') + 1);

    private readonly record struct ProfileShape(
        string PlanSignature,
        BlockCheckIpVersion IpVersion,
        BlockCheckTransport Transport,
        int Port,
        string Layer7Protocol,
        bool IsBestEffort);

    private readonly record struct AssignedTarget(
        StrategyDefinition Strategy,
        string PlanSignature,
        BlockCheckTarget Target,
        bool IsBestEffort);
}
