using CDPIUI.AddOns.BlockCheck2.Analysis;
using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Synthesis;

public sealed class CircularFallbackPlanner
{
    public void Apply(
        IEnumerable<Zapret2ProfilePlan> profiles,
        IEnumerable<CandidateEvaluation> candidates,
        IEnumerable<BlockCheckTarget> targets,
        BlockCheckSynthesisOptions options)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.EnableCircularFallback || options.MaximumFallbacksPerProfile <= 0)
        {
            return;
        }

        CandidateEvaluation[] allCandidates = candidates.ToArray();
        Dictionary<string, BlockCheckTarget> targetsById = targets
            .ToDictionary(target => target.Id, StringComparer.Ordinal);

        foreach (Zapret2ProfilePlan profile in profiles)
        {
            if (!profile.Primary.SupportsCircular || profile.Primary.RequiresPreHost)
            {
                continue;
            }

            BlockCheckTarget[] profileTargets = profile.TargetIds
                .Where(targetsById.ContainsKey)
                .Select(targetId => targetsById[targetId])
                .ToArray();

            string primarySignature = CandidateNormalizer.GetPlanSignature(profile.Primary);
            HashSet<string> usedSignatures = new(StringComparer.Ordinal) { primarySignature };

            CandidateEvaluation[] eligible = allCandidates
                .Where(candidate =>
                    candidate.Definition.SupportsCircular &&
                    !candidate.Definition.RequiresPreHost &&
                    profileTargets.All(target =>
                        candidate.Definition.AppliesTo(target) &&
                        candidate.IsSuccessful(target.Id, options)))
                .OrderBy(candidate => candidate.GetEffectiveCost(options, profile.TargetIds))
                .ThenByDescending(candidate => candidate.GetMinimumSuccessRate(profile.TargetIds))
                .ThenBy(candidate => candidate.Definition.Id, StringComparer.Ordinal)
                .ToArray();

            foreach (CandidateEvaluation candidate in eligible)
            {
                string signature = CandidateNormalizer.GetPlanSignature(candidate.Definition);
                if (!usedSignatures.Add(signature))
                {
                    continue;
                }

                profile.Fallbacks.Add(candidate.Definition);
                if (profile.Fallbacks.Count >= options.MaximumFallbacksPerProfile)
                {
                    break;
                }
            }
        }
    }
}
