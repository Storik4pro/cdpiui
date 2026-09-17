using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Analysis;

internal sealed record BlockCheckTargetGroup(
    string Id,
    BlockCheckTarget[] Targets);

/// <summary>
/// Builds units that must receive one strategy. Exact TLS 1.2/automatic HTTPS probes
/// for the same host share a unit. All targets from the same selected hostlist
/// also share a unit, because an emitted --hostlist refers to the complete file.
/// Intersecting hostlists are joined transitively to prevent profile overlap.
/// </summary>
internal static class BlockCheckTargetGroupBuilder
{
    public static IReadOnlyList<BlockCheckTargetGroup> Build(IEnumerable<BlockCheckTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        List<BlockCheckTargetGroup> result = [];

        var shapes = targets.GroupBy(target => new TargetShape(
            target.IpVersion,
            target.Transport,
            target.Port,
            target.Layer7Protocol));
        foreach (var shape in shapes)
        {
            foreach (IGrouping<RuntimeRouteKey, BlockCheckTarget> manual in shape
                         .Where(target => target.HostListPaths.Count == 0)
                         .GroupBy(target => target.GetRuntimeRouteKey()))
            {
                result.Add(new BlockCheckTargetGroup(
                    $"manual:{manual.Key}",
                    manual.ToArray()));
            }

            BlockCheckTarget[] listed = shape
                .Where(target => target.HostListPaths.Count > 0)
                .ToArray();
            if (listed.Length == 0)
            {
                continue;
            }

            DisjointSet components = new(listed.Length);
            Dictionary<string, int> firstTargetByList = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < listed.Length; index++)
            {
                foreach (string path in listed[index].HostListPaths)
                {
                    if (firstTargetByList.TryGetValue(path, out int first))
                    {
                        components.Union(first, index);
                    }
                    else
                    {
                        firstTargetByList[path] = index;
                    }
                }
            }

            foreach (IGrouping<int, (BlockCheckTarget Target, int Index)> component in listed
                         .Select((target, index) => (Target: target, Index: index))
                         .GroupBy(item => components.Find(item.Index)))
            {
                BlockCheckTarget[] componentTargets = component
                    .Select(item => item.Target)
                    .ToArray();
                string lists = string.Join('|', componentTargets
                    .SelectMany(target => target.HostListPaths)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
                result.Add(new BlockCheckTargetGroup(
                    $"lists:{shape.Key}:{lists}",
                    componentTargets));
            }
        }

        return result;
    }

    private readonly record struct TargetShape(
        BlockCheckIpVersion IpVersion,
        BlockCheckTransport Transport,
        int Port,
        string Layer7Protocol);

    private sealed class DisjointSet
    {
        private readonly int[] parent;

        public DisjointSet(int count)
        {
            parent = Enumerable.Range(0, count).ToArray();
        }

        public int Find(int value)
        {
            if (parent[value] != value)
            {
                parent[value] = Find(parent[value]);
            }
            return parent[value];
        }

        public void Union(int left, int right)
        {
            int leftRoot = Find(left);
            int rightRoot = Find(right);
            if (leftRoot != rightRoot)
            {
                parent[rightRoot] = leftRoot;
            }
        }
    }
}
