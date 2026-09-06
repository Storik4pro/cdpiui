using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Execution;

public interface IBlockCheckProbePreflight
{
    Task<IReadOnlyList<BlockCheckIssue>> CheckAsync(
        IEnumerable<BlockCheckTarget> targets,
        CancellationToken cancellationToken);
}
