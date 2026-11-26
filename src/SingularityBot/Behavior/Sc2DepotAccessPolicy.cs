namespace SingularityBot.Behavior;

/// <summary>Physical command selected for an observed Supply Depot access state.</summary>
public enum Sc2DepotAccessCommand
{
    None,
    Lower,
    Raise
}

/// <summary>
/// Deterministic access policy. An explicit, live passage request takes
/// precedence over threat posture; otherwise an enemy presence closes the
/// choke and an uncontested Depot remains lowered.
/// </summary>
public static class Sc2DepotAccessPolicy
{
    public static Sc2DepotAccessCommand Select(
        bool observedRaised,
        bool observedLowered,
        bool hasNearbyEnemy,
        bool passageRequested)
    {
        // Unknown or contradictory observations must never trigger a guessed command.
        if (observedRaised == observedLowered)
            return Sc2DepotAccessCommand.None;

        var shouldBeLowered = passageRequested || !hasNearbyEnemy;
        if (shouldBeLowered && observedRaised)
            return Sc2DepotAccessCommand.Lower;

        if (!shouldBeLowered && observedLowered)
            return Sc2DepotAccessCommand.Raise;

        return Sc2DepotAccessCommand.None;
    }
}
