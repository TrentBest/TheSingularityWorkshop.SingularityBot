using System;

namespace SingularityBot.Spatial.Reasoning;

/// <summary>
/// Measured map features for a candidate platform. Continuous features are normalized
/// to [0,1]; AccessRouteCount is an integer count supplied by map/path analysis.
/// </summary>
public readonly record struct PlatformProfile(
    int AccessRouteCount,
    float ElevationAdvantage,
    float OpenArea,
    float TightestChokeWidth,
    float EnemyApproachDirectness);

/// <summary>Doctrine supplies weights; spatial analysis supplies measurements.</summary>
public readonly record struct PlatformScoreWeights(
    float FewAccessRoutes,
    float Elevation,
    float OpenArea,
    float NarrowChoke,
    float DirectEnemyApproach)
{
    public static PlatformScoreWeights DefensiveTerran => new(0.25f, 0.25f, 0.10f, 0.25f, 0.15f);
}

public readonly record struct PlatformAssessment(float Score, PlatformProfile Profile);

public static class PlatformReasoning
{
    /// <summary>
    /// Scores a platform using caller-selected doctrine weights. Fewer routes,
    /// higher ground, useful open area, a narrowest choke, and a direct enemy
    /// approach can all contribute. Directness is deliberately weighted rather
    /// than assumed: it is a prior, not a certainty about opponent behavior.
    /// </summary>
    public static PlatformAssessment Assess(
        PlatformProfile profile,
        int maximumAccessRoutes,
        PlatformScoreWeights weights)
    {
        if (maximumAccessRoutes < 1) throw new ArgumentOutOfRangeException(nameof(maximumAccessRoutes));
        ValidateUnit(profile.ElevationAdvantage, nameof(profile.ElevationAdvantage));
        ValidateUnit(profile.OpenArea, nameof(profile.OpenArea));
        ValidateUnit(profile.TightestChokeWidth, nameof(profile.TightestChokeWidth));
        ValidateUnit(profile.EnemyApproachDirectness, nameof(profile.EnemyApproachDirectness));
        ValidateWeight(weights.FewAccessRoutes, nameof(weights.FewAccessRoutes));
        ValidateWeight(weights.Elevation, nameof(weights.Elevation));
        ValidateWeight(weights.OpenArea, nameof(weights.OpenArea));
        ValidateWeight(weights.NarrowChoke, nameof(weights.NarrowChoke));
        ValidateWeight(weights.DirectEnemyApproach, nameof(weights.DirectEnemyApproach));

        var routeScarcity = 1f - Math.Clamp(profile.AccessRouteCount, 0, maximumAccessRoutes) / (float)maximumAccessRoutes;
        var narrowness = 1f - profile.TightestChokeWidth;
        var totalWeight = weights.FewAccessRoutes + weights.Elevation + weights.OpenArea +
                          weights.NarrowChoke + weights.DirectEnemyApproach;
        var score = totalWeight <= 0 ? 0 :
            (routeScarcity * weights.FewAccessRoutes +
             profile.ElevationAdvantage * weights.Elevation +
             profile.OpenArea * weights.OpenArea +
             narrowness * weights.NarrowChoke +
             profile.EnemyApproachDirectness * weights.DirectEnemyApproach) / totalWeight;

        return new PlatformAssessment(score, profile);
    }

    private static void ValidateUnit(float value, string name)
    {
        if (float.IsNaN(value) || value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(name, "Feature values must be normalized to [0,1].");
    }

    private static void ValidateWeight(float value, string name)
    {
        if (float.IsNaN(value) || value < 0 || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, "Weights must be finite and non-negative.");
    }
}
