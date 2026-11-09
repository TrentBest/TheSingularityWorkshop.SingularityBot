namespace SingularityBot.Behavior;

/// <summary>
/// A selected domain command with optional authoritative target and world-space
/// destination. Null means the planner has not supplied that fact; consumers
/// must not substitute guessed coordinates or protocol ability identifiers.
/// </summary>
public sealed record BehaviorIntent(
    ulong UnitTag,
    BehaviorCommand Command,
    ulong? TargetUnitTag = null,
    float? DestinationX = null,
    float? DestinationY = null,
    SingularityBot.Spatial.SectorId? Sector = null);
