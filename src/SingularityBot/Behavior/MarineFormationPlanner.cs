namespace SingularityBot.Behavior;

/// <summary>
/// Converts squad formations into deterministic offsets in world space.
/// Offsets are centered on the squad anchor and oriented to the travel vector.
/// </summary>
public enum MarineFormation
{
    Box,
    Wedge,
    Line,
    Column,
    HalfCircle
}

/// <summary>
/// Tactical depth assigned by unit role. Lower tiers are closer to the
/// direction of travel; higher tiers remain protected behind the line.
/// </summary>
public enum CombatFormationRole
{
    Vanguard,
    Line,
    Support,
    Artillery,
    AirCover
}

public static class MarineFormationPlanner
{
    private const float SlotSpacing = 2f;

    public static IReadOnlyList<(float X, float Y)> GetOffsets(
        MarineFormation formation,
        int unitCount,
        float directionX,
        float directionY)
    {
        if (unitCount < 0)
            throw new ArgumentOutOfRangeException(nameof(unitCount));
        if (unitCount == 0)
            return Array.Empty<(float X, float Y)>();

        var length = MathF.Sqrt(directionX * directionX + directionY * directionY);
        if (!float.IsFinite(length) || length < 0.001f)
            throw new ArgumentException("A finite, non-zero travel direction is required.");
        var forwardX = directionX / length;
        var forwardY = directionY / length;
        var sideX = -forwardY;
        var sideY = forwardX;

        var local = formation switch
        {
            MarineFormation.Box => Box(unitCount),
            MarineFormation.Wedge => Wedge(unitCount),
            MarineFormation.Line => Enumerable.Range(0, unitCount)
                .Select(index => ((index - (unitCount - 1) / 2f) * SlotSpacing, 0f)).ToArray(),
            MarineFormation.Column => Enumerable.Range(0, unitCount)
                .Select(index => (0f, (index - (unitCount - 1) / 2f) * SlotSpacing)).ToArray(),
            MarineFormation.HalfCircle => HalfCircle(unitCount),
            _ => throw new ArgumentOutOfRangeException(nameof(formation))
        };

        return local.Select(point => (
            X: sideX * point.Item1 + forwardX * point.Item2,
            Y: sideY * point.Item1 + forwardY * point.Item2)).ToArray();
    }

    /// <summary>
    /// Assigns deterministic, role-aware slots while preserving the input
    /// order in the returned list. Vanguard units occupy the forward line,
    /// infantry the main line, support behind them, and artillery at the rear.
    /// Air cover is staged above/behind the ground force in a separate rear tier.
    /// </summary>
    public static IReadOnlyList<(float X, float Y)> GetRoleAwareOffsets(
        IReadOnlyList<CombatFormationRole> roles,
        float directionX,
        float directionY,
        float slotSpacing = SlotSpacing)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (!float.IsFinite(slotSpacing) || slotSpacing <= 0)
            throw new ArgumentOutOfRangeException(nameof(slotSpacing));
        if (roles.Count == 0)
            return Array.Empty<(float X, float Y)>();

        var length = MathF.Sqrt(directionX * directionX + directionY * directionY);
        if (!float.IsFinite(length) || length < 0.001f)
            throw new ArgumentException("A finite, non-zero travel direction is required.");
        var forwardX = directionX / length;
        var forwardY = directionY / length;
        var sideX = -forwardY;
        var sideY = forwardX;
        var result = new (float X, float Y)[roles.Count];

        foreach (var roleGroup in roles
                     .Select((role, index) => (Role: role, Index: index))
                     .GroupBy(item => item.Role))
        {
            var tier = roleGroup.Key switch
            {
                CombatFormationRole.Vanguard => 1,
                CombatFormationRole.Line => 0,
                CombatFormationRole.Support => -1,
                CombatFormationRole.Artillery => -2,
                CombatFormationRole.AirCover => -3,
                _ => throw new ArgumentOutOfRangeException(nameof(roles), "Unknown combat formation role.")
            };
            var members = roleGroup.OrderBy(item => item.Index).ToArray();
            for (var index = 0; index < members.Length; index++)
            {
                var lateral = (index - (members.Length - 1) / 2f) * slotSpacing;
                var forward = tier * slotSpacing;
                result[members[index].Index] = (
                    sideX * lateral + forwardX * forward,
                    sideY * lateral + forwardY * forward);
            }
        }

        return result;
    }

    /// <summary>
    /// <summary>
    /// Finds a safe step for a Marine intersecting a finite line-attack corridor.
    /// The result lies perpendicular to the attack line, at least
    /// <paramref name="minimumClearance"/> from its centerline. The candidate
    /// side is chosen by proximity to the supplied open formation gap.
    /// Returns false when the Marine is already outside the corridor.
    /// </summary>
    public static bool TryGetLineThreatEscapeTarget(
        (float X, float Y) marinePosition,
        (float X, float Y) lineStart,
        (float X, float Y) lineEnd,
        float minimumClearance,
        (float X, float Y) preferredGapCenter,
        out (float X, float Y) escapeTarget)
    {
        escapeTarget = default;
        if (!float.IsFinite(minimumClearance) || minimumClearance <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumClearance));
        if (!IsFinite(marinePosition) || !IsFinite(lineStart) ||
            !IsFinite(lineEnd) || !IsFinite(preferredGapCenter))
            throw new ArgumentException("Line-threat geometry must contain finite coordinates.");

        var lineX = lineEnd.X - lineStart.X;
        var lineY = lineEnd.Y - lineStart.Y;
        var lineLengthSquared = lineX * lineX + lineY * lineY;
        if (!float.IsFinite(lineLengthSquared) || lineLengthSquared < 0.000001f)
            throw new ArgumentException("A finite, non-zero attack line is required.");

        var projection = ((marinePosition.X - lineStart.X) * lineX +
                          (marinePosition.Y - lineStart.Y) * lineY) / lineLengthSquared;
        projection = Math.Clamp(projection, 0f, 1f);
        var closestX = lineStart.X + projection * lineX;
        var closestY = lineStart.Y + projection * lineY;
        var offsetX = marinePosition.X - closestX;
        var offsetY = marinePosition.Y - closestY;
        var distanceSquared = offsetX * offsetX + offsetY * offsetY;
        if (distanceSquared >= minimumClearance * minimumClearance)
            return false;

        var inverseLength = 1f / MathF.Sqrt(lineLengthSquared);
        var normalX = -lineY * inverseLength;
        var normalY = lineX * inverseLength;
        if (distanceSquared > 0.000001f)
        {
            var signedSide = offsetX * normalX + offsetY * normalY;
            if (signedSide < 0)
            {
                normalX = -normalX;
                normalY = -normalY;
            }
        }

        var positive = (X: closestX + normalX * minimumClearance,
                        Y: closestY + normalY * minimumClearance);
        var negative = (X: closestX - normalX * minimumClearance,
                        Y: closestY - normalY * minimumClearance);
        escapeTarget = SquaredDistance(positive, preferredGapCenter) <=
                       SquaredDistance(negative, preferredGapCenter) ? positive : negative;
        return true;
    }

    private static bool IsFinite((float X, float Y) point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y);

    private static float SquaredDistance(
        (float X, float Y) left,
        (float X, float Y) right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        return x * x + y * y;
    }

    /// Computes marching waypoints from current Marine positions. Marines are
    /// ordered front-to-back; -1 marks a leading-edge Marine that advances by
    /// <paramref name="advanceDistance"/>, while every other Marine moves to
    /// the current position of its designated Marine in front. The references
    /// are positional only: they are never converted into unit-target orders.
    /// </summary>
    public static IReadOnlyList<(float X, float Y)> GetMarchingTargets(
        IReadOnlyList<(float X, float Y)> currentPositions,
        IReadOnlyList<int> frontMarineIndices,
        float directionX,
        float directionY,
        float advanceDistance = SlotSpacing)
    {
        ArgumentNullException.ThrowIfNull(currentPositions);
        ArgumentNullException.ThrowIfNull(frontMarineIndices);
        if (currentPositions.Count != frontMarineIndices.Count)
            throw new ArgumentException("Every Marine must have exactly one front-reference entry.", nameof(frontMarineIndices));
        if (!float.IsFinite(advanceDistance) || advanceDistance <= 0)
            throw new ArgumentOutOfRangeException(nameof(advanceDistance));

        var length = MathF.Sqrt(directionX * directionX + directionY * directionY);
        if (!float.IsFinite(length) || length < 0.001f)
            throw new ArgumentException("A finite, non-zero travel direction is required.");
        var forwardX = directionX / length;
        var forwardY = directionY / length;
        var targets = new (float X, float Y)[currentPositions.Count];

        for (var index = 0; index < currentPositions.Count; index++)
        {
            var position = currentPositions[index];
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
                throw new ArgumentException("Marine positions must be finite.", nameof(currentPositions));

            var frontIndex = frontMarineIndices[index];
            if (frontIndex == -1)
            {
                targets[index] = (
                    position.X + forwardX * advanceDistance,
                    position.Y + forwardY * advanceDistance);
                continue;
            }

            if (frontIndex < 0 || frontIndex >= index)
                throw new ArgumentOutOfRangeException(nameof(frontMarineIndices),
                    "A front Marine must have a lower index in the front-to-back ordering.");
            targets[index] = currentPositions[frontIndex];
        }

        return targets;
    }

    private static (float X, float Y)[] Box(int count)
    {
        var result = new (float X, float Y)[count];
        for (var index = 0; index < count; index++)
            // Keep earlier slots stable as the squad grows. New Marines occupy
            // successive rows instead of forcing the existing force to reshuffle.
            result[index] = (((index % 2 == 0) ? -0.5f : 0.5f) * SlotSpacing,
                (index / 2) * SlotSpacing);
        return result;
    }

    private static (float X, float Y)[] Wedge(int count)
    {
        var result = new (float X, float Y)[count];
        if (count == 0) return result;
        result[0] = (0, -SlotSpacing);
        for (var index = 1; index < count; index++)
        {
            var rank = (index + 1) / 2;
            var side = index % 2 == 1 ? -1f : 1f;
            result[index] = (side * rank * SlotSpacing, rank * SlotSpacing);
        }
        return result;
    }

    private static (float X, float Y)[] HalfCircle(int count)
    {
        var result = new (float X, float Y)[count];
        if (count == 1) { result[0] = (0, 0); return result; }
        for (var index = 0; index < count; index++)
        {
            var angle = MathF.PI * index / (count - 1);
            result[index] = (MathF.Cos(angle) * SlotSpacing * (count - 1) / 2f,
                MathF.Sin(angle) * SlotSpacing * (count - 1) / 2f);
        }
        return result;
    }
}