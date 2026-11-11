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
            Y: sideY * point.X + forwardY * point.Y)).ToArray();
    }

    private static (float X, float Y)[] Box(int count)
    {
        var result = new (float X, float Y)[count];
        for (var index = 0; index < count; index++)
            result[index] = (((index % 2 == 0) ? -0.5f : 0.5f) * SlotSpacing,
                ((index / 2 == 0) ? -0.5f : 0.5f) * SlotSpacing);
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