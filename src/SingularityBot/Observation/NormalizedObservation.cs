namespace SingularityBot.Observation;

/// <summary>Protocol-independent visibility classification for a unit observation.</summary>
public enum UnitVisibility
{
    Unknown = 0,
    Visible = 1,
    Snapshot = 2,
    Hidden = 3
}

/// <summary>Protocol-independent alliance classification.</summary>
public enum UnitAlliance
{
    Unknown = 0,
    Self = 1,
    Ally = 2,
    Neutral = 3,
    Enemy = 4
}

/// <summary>A normalized unit fact set. Snapshot/hidden entries are never live command targets.</summary>
public sealed record UnitObservationFacts(
    ulong Tag,
    uint UnitTypeId,
    string UnitKind,
    int OwnerId,
    UnitAlliance Alliance,
    UnitVisibility Visibility,
    float Health,
    float HealthMax,
    float Shield,
    float ShieldMax,
    float BuildProgress,
    float PositionX,
    float PositionY,
    bool HasPosition,
    bool HasCargo,
    bool HasOrders,
    bool IsFlying,
    bool IsBurrowed,
    bool IsHallucination,
    uint GameLoop)
{
    /// <summary>Remaining weapon cooldown in game-loop units, as reported by SC2.</summary>
    public float WeaponCooldownRemaining { get; init; }

    /// <summary>Current unit energy as reported by the SC2 observation.</summary>
    public float Energy { get; init; }

    public IReadOnlyList<UnitOrderObservationFacts> Orders { get; init; } = Array.Empty<UnitOrderObservationFacts>();

    public bool IsLiveVisible => Visibility == UnitVisibility.Visible;
}

/// <summary>Protocol-independent target data for one observed SC2 order.</summary>
public sealed record UnitOrderObservationFacts(
    uint AbilityId,
    ulong? TargetUnitTag,
    float? TargetPositionX,
    float? TargetPositionY);

/// <summary>Immutable normalized view of one SC2 observation.</summary>
public sealed class NormalizedObservation
{
    public NormalizedObservation(
        uint gameLoop,
        uint playerId,
        IReadOnlyList<UnitObservationFacts> units,
        IReadOnlySet<ulong> deadUnitTags)
    {
        GameLoop = gameLoop;
        PlayerId = playerId;
        Units = units ?? throw new ArgumentNullException(nameof(units));
        DeadUnitTags = deadUnitTags ?? throw new ArgumentNullException(nameof(deadUnitTags));
    }

    public uint GameLoop { get; }
    public uint PlayerId { get; }
    public IReadOnlyList<UnitObservationFacts> Units { get; }
    public IReadOnlySet<ulong> DeadUnitTags { get; }
    public uint Minerals { get; init; }
    public uint FoodUsed { get; init; }
    public uint FoodCap { get; init; }
}
