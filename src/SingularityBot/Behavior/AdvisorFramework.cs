using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

public enum GamePhase
{
    Early,
    Mid,
    Late
}

public enum AdvisorDomain
{
    Economy,
    Military,
    Science,
    BaseEngineering,
    Intelligence
}

/// <summary>
/// Match-level strategic context. Phase evidence is derived only from the
/// current normalized observation; the FSM owns phase transitions.
/// </summary>
public sealed class GameStateContext : IStateContext
{
    public string Name { get; set; } = "SingularityBot.GameState";
    public bool IsValid { get; set; } = true;
    public GamePhase Phase { get; private set; } = GamePhase.Early;
    public uint GameLoop { get; private set; }
    public int FriendlyBaseCount { get; private set; }
    public bool HasAdvancedTech { get; private set; }
    public bool HasLateGameTech { get; private set; }

    public void Apply(NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        GameLoop = observation.GameLoop;
        var friendly = observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();

        FriendlyBaseCount = friendly.Count(unit => IsTownHall(unit.UnitKind));
        HasAdvancedTech = friendly.Any(unit => IsAdvancedTechStructure(unit.UnitKind));
        HasLateGameTech = friendly.Any(unit => IsLateGameTechStructure(unit.UnitKind));
    }

    public void SetPhase(GamePhase phase) => Phase = phase;

    private static bool IsTownHall(string kind) =>
        kind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase);

    private static bool IsAdvancedTechStructure(string kind) =>
        kind.Equals("Factory", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Starport", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("EngineeringBay", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Armory", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("GhostAcademy", StringComparison.OrdinalIgnoreCase);

    private static bool IsLateGameTechStructure(string kind) =>
        kind.Equals("FusionCore", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Battlecruiser", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Phase-aware advisor compute allocation. Weights are relative work slots,
/// not elapsed-time assumptions. Each slot advances that advisor's FSM group
/// once, allowing expensive domains to receive more evaluations in the phases
/// where they matter most.
/// </summary>
public static class AdvisorWorkScheduler
{
    public const string EconomyGroup = "SingularityBot.Advisor.Economy";
    public const string MilitaryGroup = "SingularityBot.Advisor.Military";
    public const string ScienceGroup = "SingularityBot.Advisor.Science";
    public const string BaseEngineeringGroup = "SingularityBot.Advisor.BaseEngineering";
    public const string IntelligenceGroup = "SingularityBot.Advisor.Intelligence";

    private static readonly AdvisorDomain[] Domains =
    [
        AdvisorDomain.Economy, AdvisorDomain.Military, AdvisorDomain.Science,
        AdvisorDomain.BaseEngineering, AdvisorDomain.Intelligence
    ];

    public static IReadOnlyDictionary<AdvisorDomain, int> GetWeights(GamePhase phase) =>
        phase switch
        {
            GamePhase.Early => new Dictionary<AdvisorDomain, int>
            {
                [AdvisorDomain.Economy] = 5, [AdvisorDomain.Military] = 2,
                [AdvisorDomain.Science] = 1, [AdvisorDomain.BaseEngineering] = 3,
                [AdvisorDomain.Intelligence] = 4
            },
            GamePhase.Mid => new Dictionary<AdvisorDomain, int>
            {
                [AdvisorDomain.Economy] = 3, [AdvisorDomain.Military] = 4,
                [AdvisorDomain.Science] = 3, [AdvisorDomain.BaseEngineering] = 3,
                [AdvisorDomain.Intelligence] = 3
            },
            GamePhase.Late => new Dictionary<AdvisorDomain, int>
            {
                [AdvisorDomain.Economy] = 2, [AdvisorDomain.Military] = 5,
                [AdvisorDomain.Science] = 4, [AdvisorDomain.BaseEngineering] = 2,
                [AdvisorDomain.Intelligence] = 3
            },
            _ => throw new ArgumentOutOfRangeException(nameof(phase))
        };

    public static IReadOnlyList<string> GetFrameSchedule(GamePhase phase)
    {
        var weights = GetWeights(phase);
        return Domains.SelectMany(domain =>
            Enumerable.Repeat(GroupFor(domain), weights[domain])).ToArray();
    }

    public static string GroupFor(AdvisorDomain domain) => domain switch
    {
        AdvisorDomain.Economy => EconomyGroup,
        AdvisorDomain.Military => MilitaryGroup,
        AdvisorDomain.Science => ScienceGroup,
        AdvisorDomain.BaseEngineering => BaseEngineeringGroup,
        AdvisorDomain.Intelligence => IntelligenceGroup,
        _ => throw new ArgumentOutOfRangeException(nameof(domain))
    };

    public static void RegisterProcessingGroups()
    {
        foreach (var domain in Domains)
            FSM_API.Create.CreateProcessingGroup(GroupFor(domain));
    }

    public static void Advance(GamePhase phase)
    {
        foreach (var group in GetFrameSchedule(phase))
            FSM_API.Interaction.Update(group);
    }
}

/// <summary>
/// Shared policy contract for domain advisors. Advisors produce recommendations
/// and work requests; Task Master arbitration remains the only assignment owner.
/// </summary>
public interface IStrategicAdvisor
{
    AdvisorDomain Domain { get; }
    GamePhase CurrentPhase { get; }
    int ComputeWeight { get; }
}
