using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// The immutable match-start dependencies supplied to an opener. Openers resolve
/// their own race-specific requirements from the authoritative SC2 game data.
/// </summary>
public sealed record OpeningContext(
    Race ActualRace,
    BotModeDescriptor Mode,
    Sc2GameDataCatalog GameData,
    Sc2RaceProductionProfile ProductionProfile);

/// <summary>
/// A mode-specific early-game controller. Initialization happens once from the
/// bot's OnStart callback; frame advancement and asynchronous SC2 callbacks are
/// then routed to the same instance for the lifetime of the match.
/// </summary>
public interface IOpener
{
    string ModeId { get; }
    string State { get; }
    bool IsComplete { get; }

    bool Initialize(OpeningContext context, out string error);
    IReadOnlyList<SC2APIProtocol.Action> Advance(NormalizedObservation observation);
    RequestQuery TakeQueryRequest();
    void OnQueryResult(ResponseQuery response);
    void OnActionResult(ResponseAction response);
}

/// <summary>
/// Zergling opener adapter. The existing runtime remains responsible for the
/// detailed build/production state machine; this adapter gives it the shared
/// IOpener lifecycle without changing its proven query/result semantics.
/// </summary>
public sealed class ZerglingOpener : IOpener
{
    public const string Id = "zerg.zergling.v1";
    private ZerglingOpeningRuntime _runtime;

    public string ModeId => Id;
    public string State => _runtime?.State ?? "Uninitialized";
    public bool IsComplete => _runtime?.IsComplete ?? false;

    public bool Initialize(OpeningContext context, out string error)
    {
        _runtime = null;
        if (context is null || context.Mode is null || context.GameData is null)
        {
            error = "Opening context, selected mode and authoritative game data are required.";
            return false;
        }
        if (context.ActualRace != Race.Zerg || context.Mode.Race != Race.Zerg ||
            !string.Equals(context.Mode.Id, Id, StringComparison.Ordinal))
        {
            error = $"Zergling opener requires mode '{Id}' and actual race Zerg.";
            return false;
        }
        if (context.ProductionProfile is null)
        {
            error = "Resolved Zergling production profile is unavailable.";
            return false;
        }
        if (!context.GameData.TryGetUniqueUnit("Drone", out var drone) ||
            !context.GameData.TryGetUniqueUnit("SpawningPool", out var pool) ||
            !context.GameData.TryGetUniqueUnit("Zergling", out var zergling) ||
            !context.GameData.TryGetUniqueUnit("Larva", out var larva) ||
            !context.GameData.TryGetAbility(pool.AbilityId, out var buildAbility) ||
            !buildAbility.IsBuilding)
        {
            error = "Required Drone, SpawningPool, Zergling, Larva or building ability game data is unavailable.";
            return false;
        }

        var gatherAbilityIds = context.GameData.AbilitiesById.Values
            .Where(ability =>
                ability.LinkName.Contains("Harvest", StringComparison.OrdinalIgnoreCase) ||
                ability.LinkName.Contains("Gather", StringComparison.OrdinalIgnoreCase))
            .Select(ability => ability.AbilityId)
            .Where(id => id > 0)
            .ToHashSet();

        try
        {
            _runtime = new ZerglingOpeningRuntime(
                drone, pool, buildAbility, context.ProductionProfile,
                zergling, larva, gatherAbilityIds);
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            _runtime = null;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public IReadOnlyList<SC2APIProtocol.Action> Advance(NormalizedObservation observation) =>
        _runtime?.OnFrame(observation) ?? Array.Empty<SC2APIProtocol.Action>();

    public RequestQuery TakeQueryRequest() => _runtime?.TakeQueryRequest();

    public void OnQueryResult(ResponseQuery response) => _runtime?.OnQueryResult(response);

    public void OnActionResult(ResponseAction response) => _runtime?.OnActionResult(response);
}

 
/// <summary>Protoss adapter for the shared Pylon/Gateway/first-unit opening runtime.</summary>
public sealed class ProtossZealotOpener : IOpener
{
    public const string Id = "protoss.zealot.v1";
    private ProtossZealotOpeningRuntime _runtime;
    public string ModeId => Id;
    public string State => _runtime?.State ?? "Uninitialized";
    public bool IsComplete => _runtime?.IsComplete ?? false;

    public bool Initialize(OpeningContext context, out string error)
    {
        _runtime = null;
        if (context is null || context.Mode is null || context.GameData is null ||
            context.ActualRace != Race.Protoss || context.Mode.Race != Race.Protoss ||
            !string.Equals(context.Mode.Id, Id, StringComparison.Ordinal))
        {
            error = $"Protoss Zealot opener requires mode '{Id}' and actual race Protoss.";
            return false;
        }
        if (context.ProductionProfile is null ||
            !context.GameData.TryGetUniqueUnit("Probe", out var probe) ||
            !context.GameData.TryGetUniqueUnit("Pylon", out var pylon) ||
            !context.GameData.TryGetUniqueUnit("Gateway", out var gateway) ||
            !context.GameData.TryGetUniqueUnit("Zealot", out var zealot) ||
            !context.GameData.TryGetAbility(pylon.AbilityId, out var pylonBuild) || !pylonBuild.IsBuilding ||
            !context.GameData.TryGetAbility(gateway.AbilityId, out var gatewayBuild) || !gatewayBuild.IsBuilding)
        {
            error = "Required Probe, Pylon, Gateway, Zealot or authoritative building abilities are unavailable.";
            return false;
        }
        var gatherAbilityIds = context.GameData.AbilitiesById.Values
            .Where(ability =>
                ability.LinkName.Contains("Harvest", StringComparison.OrdinalIgnoreCase) ||
                ability.LinkName.Contains("Gather", StringComparison.OrdinalIgnoreCase))
            .Select(ability => ability.AbilityId)
            .Where(id => id > 0)
            .ToHashSet();
        try
        {
            _runtime = new ProtossZealotOpeningRuntime(
                probe, pylon, gateway, zealot, pylonBuild, gatewayBuild,
                context.ProductionProfile, gatherAbilityIds);
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            _runtime = null;
            return false;
        }
        error = string.Empty;
        return true;
    }

    public IReadOnlyList<SC2APIProtocol.Action> Advance(NormalizedObservation observation) =>
        _runtime?.OnFrame(observation) ?? Array.Empty<SC2APIProtocol.Action>();
    public RequestQuery TakeQueryRequest() => _runtime?.TakeQueryRequest();
    public void OnQueryResult(ResponseQuery response) => _runtime?.OnQueryResult(response);
    public void OnActionResult(ResponseAction response) => _runtime?.OnActionResult(response);
}
