using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>What happens to a worker after it starts constructing a structure.</summary>
public enum WorkerConstructionDisposition
{
    /// <summary>The worker remains committed to the structure until construction completes.</summary>
    RemainsUntilComplete,
    /// <summary>The worker resumes independent work immediately after issuing construction.</summary>
    ResumesImmediately,
    /// <summary>The worker is consumed by the construction order.</summary>
    Consumed
}

/// <summary>
/// Race-specific construction constraints layered over shared worker/mining logic.
/// These policies describe game mechanics; they do not issue SC2 commands.
/// </summary>
public sealed record WorkerConstructionPolicy(
    Race Race,
    WorkerConstructionDisposition Disposition,
    bool MustDepositCargoBeforeConstruction)
{
    public static WorkerConstructionPolicy For(Race race) => race switch
    {
        Race.Terran => new(race,
            WorkerConstructionDisposition.RemainsUntilComplete,
            MustDepositCargoBeforeConstruction: true),
        Race.Protoss => new(race,
            WorkerConstructionDisposition.ResumesImmediately,
            MustDepositCargoBeforeConstruction: false),
        Race.Zerg => new(race,
            WorkerConstructionDisposition.Consumed,
            MustDepositCargoBeforeConstruction: true),
        _ => throw new ArgumentOutOfRangeException(nameof(race), race,
            "A concrete Terran, Protoss, or Zerg race is required for worker construction.")
    };

    /// <summary>
    /// Rejects a cargo-bearing Drone unconditionally. Terran builders also finish
    /// their return trip before dispatch; Protoss Probes may begin construction
    /// without being held at the site.
    /// </summary>
    public bool CanBeginConstruction(bool isCarryingResources) =>
        !MustDepositCargoBeforeConstruction || !isCarryingResources;
}
