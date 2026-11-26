namespace SingularityBot.Behavior;

/// <summary>
/// Strategic identity of a StarCraft civilization. This is deliberately
/// independent of unit, structure, ability, cost, and production mechanics.
/// </summary>
public enum Civilization
{
    Terran,
    Zerg,
    Protoss
}

/// <summary>
/// Shared conceptual responsibilities used by every civilization.
/// Race-specific leadership interprets these domains; it does not redefine them.
/// </summary>
public enum StrategicDomain
{
    Economy,
    Military,
    Intelligence,
    Technology,
    Expansion,
    Defense,
    Logistics
}

/// <summary>
/// Campaign-inspired command identity for a civilization. Authority names are
/// presentation/domain concepts, not executable policy or game mechanics.
/// </summary>
public sealed record CivilizationLeadership(
    Civilization Civilization,
    string SupremeAuthority,
    string SubordinateAuthority,
    string SubordinateAuthorityPlural);

/// <summary>
/// Canonical civilization leadership vocabulary. The catalog intentionally
/// contains no SC2 unit IDs, abilities, costs, prerequisites, or build orders.
/// </summary>
public static class CivilizationLeadershipCatalog
{
    private static readonly IReadOnlyDictionary<Civilization, CivilizationLeadership> Models =
        new Dictionary<Civilization, CivilizationLeadership>
        {
            [Civilization.Terran] = new(
                Civilization.Terran, "Admiral", "Commander", "Commanders"),
            [Civilization.Zerg] = new(
                Civilization.Zerg, "Overmind", "Cerebrate", "Cerebrates"),
            [Civilization.Protoss] = new(
                Civilization.Protoss, "Conclave", "Judicator", "Judicators")
        };

    public static CivilizationLeadership Get(Civilization civilization) =>
        Models.TryGetValue(civilization, out var leadership)
            ? leadership
            : throw new ArgumentOutOfRangeException(nameof(civilization), civilization, "Unknown civilization.");
}
