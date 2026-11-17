#nullable enable annotations

using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>Independently opt-in history for selected live-observation fields.</summary>
[Flags]
public enum UnitObservationHistoryFields
{
    None = 0,
    Health = 1 << 0,
    Position = 1 << 1,
    WeaponCooldown = 1 << 2
}

/// <summary>
/// Stores the previous live-visible sample for explicitly enabled fields.
/// Fog-of-war snapshots never advance history or overwrite its last live sample.
/// </summary>
public sealed class UnitObservationHistory
{
    private UnitObservationHistoryFields _fields;

    public UnitObservationHistoryFields EnabledFields
    {
        get => _fields;
        set => _fields = value;
    }

    public bool HasPreviousHealth { get; private set; }
    public float PreviousHealth { get; private set; }
    public float HealthDelta { get; private set; }

    public bool HasPreviousPosition { get; private set; }
    public float PreviousPositionX { get; private set; }
    public float PreviousPositionY { get; private set; }
    public float PositionDeltaX { get; private set; }
    public float PositionDeltaY { get; private set; }

    public bool HasPreviousWeaponCooldown { get; private set; }
    public float PreviousWeaponCooldown { get; private set; }
    public float WeaponCooldownDelta { get; private set; }

    internal void Record(UnitBehaviorContext current, UnitObservationFacts facts)
    {
        if (!facts.IsLiveVisible) return;

        if ((_fields & UnitObservationHistoryFields.Health) != 0)
        {
            if (HasPreviousHealth)
                HealthDelta = facts.Health - PreviousHealth;
            PreviousHealth = facts.Health;
            HasPreviousHealth = true;
        }

        if ((_fields & UnitObservationHistoryFields.Position) != 0 && facts.HasPosition)
        {
            if (HasPreviousPosition)
            {
                PositionDeltaX = facts.PositionX - PreviousPositionX;
                PositionDeltaY = facts.PositionY - PreviousPositionY;
            }
            PreviousPositionX = facts.PositionX;
            PreviousPositionY = facts.PositionY;
            HasPreviousPosition = true;
        }

        if ((_fields & UnitObservationHistoryFields.WeaponCooldown) != 0)
        {
            if (HasPreviousWeaponCooldown)
                WeaponCooldownDelta = facts.WeaponCooldownRemaining - PreviousWeaponCooldown;
            PreviousWeaponCooldown = facts.WeaponCooldownRemaining;
            HasPreviousWeaponCooldown = true;
        }
    }
}
