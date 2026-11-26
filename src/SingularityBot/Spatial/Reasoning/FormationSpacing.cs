using System;
using System.Collections.Generic;

namespace SingularityBot.Spatial.Reasoning;

/// <summary>Measures how many friendly units a single area-effect strike could touch.</summary>
public readonly record struct FormationSpacingScore(
    int UnitCount,
    int WorstCaseUnitsHit,
    float MinimumSeparation,
    float SplashRadius)
{
    public int AdditionalUnitsHit => Math.Max(0, WorstCaseUnitsHit - 1);
    public bool IsSingleHitOnly => UnitCount <= 1 || WorstCaseUnitsHit <= 1;
}

public static class FormationSpacing
{
    /// <summary>
    /// Treats each friendly unit as a possible impact center. The worst-case count
    /// includes the center unit and every other unit at distance <= splashRadius.
    /// </summary>
    public static FormationSpacingScore Evaluate(
        IReadOnlyList<SpatialPoint> positions,
        float splashRadius)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (splashRadius < 0 || float.IsNaN(splashRadius) || float.IsInfinity(splashRadius))
            throw new ArgumentOutOfRangeException(nameof(splashRadius));

        var worst = 0;
        var minimum = float.PositiveInfinity;
        var radiusSquared = splashRadius * splashRadius;

        for (var i = 0; i < positions.Count; i++)
        {
            var hit = 0;
            for (var j = 0; j < positions.Count; j++)
            {
                if (i == j) continue;
                var distanceSquared = (positions[i] - positions[j]).LengthSquared;
                if (distanceSquared < minimum * minimum) minimum = MathF.Sqrt(distanceSquared);
                if (distanceSquared <= radiusSquared) hit++;
            }
            worst = Math.Max(worst, hit + 1);
        }

        if (positions.Count < 2) minimum = float.PositiveInfinity;
        return new FormationSpacingScore(positions.Count, worst, minimum, splashRadius);
    }

    /// <summary>
    /// Picks the candidate with greatest distance from its nearest formation member.
    /// This is geometry only; callers must filter candidates for pathability and safety.
    /// </summary>
    public static SpatialPoint? FindMostSeparatedPosition(
        SpatialPoint current,
        IReadOnlyList<SpatialPoint> candidatePositions,
        IReadOnlyList<SpatialPoint> otherFormationMembers)
    {
        ArgumentNullException.ThrowIfNull(candidatePositions);
        ArgumentNullException.ThrowIfNull(otherFormationMembers);

        SpatialPoint? best = null;
        var bestNearestDistance = float.NegativeInfinity;
        foreach (var candidate in candidatePositions)
        {
            var nearest = float.PositiveInfinity;
            foreach (var member in otherFormationMembers)
                nearest = MathF.Min(nearest, candidate.DistanceTo(member));

            if (otherFormationMembers.Count == 0)
                nearest = candidate.DistanceTo(current);

            if (best is null || nearest > bestNearestDistance)
            {
                best = candidate;
                bestNearestDistance = nearest;
            }
        }
        return best;
    }
}
