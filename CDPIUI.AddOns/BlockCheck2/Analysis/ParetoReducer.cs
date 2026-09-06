using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Analysis;

public sealed class ParetoReducer
{
    private const double Epsilon = 0.000001d;

    public IReadOnlyList<CandidateEvaluation> Reduce(
        IEnumerable<CandidateEvaluation> candidates,
        BlockCheckSynthesisOptions options)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(options);

        CandidateEvaluation[] all = candidates.ToArray();
        List<CandidateEvaluation> result = [];

        foreach (CandidateEvaluation candidate in all)
        {
            bool dominated = all.Any(other =>
                !ReferenceEquals(candidate, other) &&
                Dominates(other, candidate, options));

            if (!dominated)
            {
                result.Add(candidate);
            }
        }

        return result
            .OrderBy(candidate => candidate.Definition.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool Dominates(
        CandidateEvaluation left,
        CandidateEvaluation right,
        BlockCheckSynthesisOptions options)
    {
        IReadOnlySet<string> leftCoverage = left.GetCoveredTargetIds(options);
        IReadOnlySet<string> rightCoverage = right.GetCoveredTargetIds(options);

        if (rightCoverage.Count == 0 || !rightCoverage.IsSubsetOf(leftCoverage))
        {
            return false;
        }

        double leftSuccess = left.GetMinimumSuccessRate(rightCoverage);
        double rightSuccess = right.GetMinimumSuccessRate(rightCoverage);
        double leftP95 = left.GetAggregateP95(rightCoverage);
        double rightP95 = right.GetAggregateP95(rightCoverage);
        double leftCost = left.Definition.BaseCost;
        double rightCost = right.Definition.BaseCost;

        bool noWorse =
            leftSuccess + Epsilon >= rightSuccess &&
            leftP95 <= rightP95 + Epsilon &&
            leftCost <= rightCost + Epsilon;

        bool strictlyBetter =
            leftCoverage.Count > rightCoverage.Count ||
            leftSuccess > rightSuccess + Epsilon ||
            leftP95 + Epsilon < rightP95 ||
            leftCost + Epsilon < rightCost;

        if (!strictlyBetter && noWorse)
        {
            string leftSignature = CandidateNormalizer.GetPlanSignature(left.Definition);
            string rightSignature = CandidateNormalizer.GetPlanSignature(right.Definition);
            strictlyBetter = leftSignature == rightSignature &&
                string.CompareOrdinal(left.Definition.Id, right.Definition.Id) < 0;
        }

        return noWorse && strictlyBetter;
    }
}
