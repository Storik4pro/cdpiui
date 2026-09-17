using CDPIUI.AddOns.BlockCheck2.Models;

namespace CDPIUI.AddOns.BlockCheck2.Presentation;

public enum BlockCheckRunPreset
{
    Quick,
    Balanced,
    Exhaustive,
}

public static class BlockCheckRunPresetFactory
{
    public static BlockCheckRunOptions Create(
        BlockCheckRunPreset preset,
        bool testAllStrategies = false,
        int? attemptsPerTarget = null)
    {
        int attempts = attemptsPerTarget ?? GetRecommendedAttemptsPerTarget(preset);
        return preset switch
        {
            BlockCheckRunPreset.Quick => new BlockCheckRunOptions
            {
                Scan = new BlockCheckScanOptions
                {
                    AttemptsPerTarget = attempts,
                    MaximumTier = BlockCheckScanTier.Quick,
                    EnableRouteEarlyStop = !testAllStrategies,
                    SuccessfulStrategiesPerRoute = 2,
                    SuccessfulStrategyRate = 1d,
                },
                Synthesis = new BlockCheckSynthesisOptions
                {
                    MinimumAttempts = attempts,
                    MinimumSuccessRate = 1d,
                    Preference = BlockCheckPreference.Speed,
                    MaximumFallbacksPerProfile = 1,
                    CircularFailureThreshold = 2,
                },
                Validation = new BlockCheckPresetValidationOptions
                {
                    AttemptsPerTarget = attempts,
                    MinimumSuccessRate = 1d,
                    MaximumRepairIterations = 1,
                },
            },
            BlockCheckRunPreset.Balanced => new BlockCheckRunOptions
            {
                Scan = new BlockCheckScanOptions
                {
                    AttemptsPerTarget = attempts,
                    MaximumTier = BlockCheckScanTier.Balanced,
                    EnableRouteEarlyStop = !testAllStrategies,
                    SuccessfulStrategiesPerRoute = 2,
                    SuccessfulStrategyRate = 0.8d,
                },
                Synthesis = new BlockCheckSynthesisOptions
                {
                    MinimumAttempts = attempts,
                    MinimumSuccessRate = 0.8d,
                    Preference = BlockCheckPreference.Balanced,
                    MaximumFallbacksPerProfile = 1,
                    CircularFailureThreshold = 3,
                },
                Validation = new BlockCheckPresetValidationOptions
                {
                    AttemptsPerTarget = attempts,
                    MinimumSuccessRate = 0.8d,
                    MaximumRepairIterations = 3,
                },
            },
            BlockCheckRunPreset.Exhaustive => new BlockCheckRunOptions
            {
                Scan = new BlockCheckScanOptions
                {
                    AttemptsPerTarget = attempts,
                    MaximumTier = BlockCheckScanTier.Exhaustive,
                    EnableRouteEarlyStop = false,
                    SuccessfulStrategiesPerRoute = 4,
                    SuccessfulStrategyRate = 0.8d,
                },
                Synthesis = new BlockCheckSynthesisOptions
                {
                    MinimumAttempts = attempts,
                    MinimumSuccessRate = 0.8d,
                    Preference = BlockCheckPreference.Stability,
                    MaximumFallbacksPerProfile = 2,
                    CircularFailureThreshold = 3,
                },
                Validation = new BlockCheckPresetValidationOptions
                {
                    AttemptsPerTarget = attempts,
                    MinimumSuccessRate = 0.8d,
                    MaximumRepairIterations = 5,
                },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(preset)),
        };
    }

    public static int GetRecommendedAttemptsPerTarget(BlockCheckRunPreset preset) => preset switch
    {
        BlockCheckRunPreset.Quick => 2,
        BlockCheckRunPreset.Balanced => 3,
        BlockCheckRunPreset.Exhaustive => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };
}
