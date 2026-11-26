namespace SingularityBot.Behavior;

/// <summary>
/// The unit-level authority boundary. A controller is the persistent unit
/// context and its attached FSM capabilities, not a second unit object.
/// </summary>
public interface IController
{
    ulong UnitTag { get; }
    string Name { get; }
    string UnitKind { get; }
    bool IsWorker { get; }
    bool IsValid { get; }
    IReadOnlyCollection<string> Capabilities { get; }
    IReadOnlyDictionary<string, string> CurrentStates { get; }
}

/// <summary>
/// Operational authority for one domain (mining, construction, scouting,
/// military, research, and so on). Coordinators own assignment policy while
/// unit controllers retain execution authority over their own FSMs.
/// </summary>
public interface ICoordinator
{
    string Id { get; }
    string Name { get; }
    string Domain { get; }
    IReadOnlyCollection<IController> Controllers { get; }
}

/// <summary>
/// Match-level strategic authority. Strategy expresses doctrine and objectives;
/// coordinators translate those objectives into bounded operational work.
/// </summary>
public interface IStrategy
{
    string Id { get; }
    string Name { get; }
    string Race { get; }
    IReadOnlyCollection<ICoordinator> Coordinators { get; }
}

/// <summary>
/// Race-specific editorial vocabulary. These are presentation presets, not
/// claims that lore characters or structures are executable game entities.
/// </summary>
public sealed record RaceCommandPresentation(
    string Race,
    string StrategyTitle,
    string StrategyDescription,
    string CoordinatorTitle,
    IReadOnlyList<string> CoordinatorExamples);

public static class RaceCommandPresentationCatalog
{
    private static readonly IReadOnlyDictionary<string, RaceCommandPresentation> Profiles =
        new Dictionary<string, RaceCommandPresentation>(StringComparer.OrdinalIgnoreCase)
        {
            ["Zerg"] = new(
                "Zerg", "The Overmind", "A distributed biological intelligence directing the swarm through its Cerebrates.",
                "Cerebrates", new[] { "Mining Brood", "Spawning Brood", "Evolution Brood", "Defense Brood", "Swarm Command" }),
            ["Terran"] = new(
                "Terran", "Fleet Admiral", "A strategic command authority issuing operational orders through subordinate commanders.",
                "Command Staff", new[] { "Mining Operations", "Construction Corps", "Production Command", "Reconnaissance", "Fleet Command" }),
            ["Protoss"] = new(
                "Protoss", "The Enclave", "A strategic council expressing the will and doctrine of the Protoss through its appointed authorities.",
                "Conclaves", new[] { "Resource Stewardship", "Construction and Warp-In", "Production", "Judgment and Defense", "Reconnaissance" })
        };

    public static IReadOnlyCollection<RaceCommandPresentation> All =>
        Array.AsReadOnly(Profiles.Values.ToArray());

    public static bool TryGet(string race, out RaceCommandPresentation presentation) =>
        Profiles.TryGetValue(race ?? string.Empty, out presentation);
}
