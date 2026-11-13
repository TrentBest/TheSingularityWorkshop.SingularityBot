using System.Text.Json;

namespace SingularityBot.Behavior;

/// <summary>
/// User-configurable economy policy. This is intentionally policy data rather
/// than a second command controller; the bot still validates every action
/// against live SC2 observations and authoritative game data.
/// </summary>
public sealed class EconomyAdvisorSettings
{
    public int Version { get; init; } = 1;
    public int MineralSaturation { get; init; } = WorkerResourceAssignmentPlanner.DefaultMineralSaturation;
    public bool PrioritizeOpeningInfrastructure { get; init; } = true;
    public bool HoldWorkerProductionForFirstDepot { get; init; } = true;
    public bool AllowLongDistanceMining { get; init; }
    /// <summary>Mineral bank at which a Supply Depot builder is reserved and dispatched; build legality still uses live game data.</summary>
    public uint SupplyDepotWorkerPullMinerals { get; init; } = 80;
    /// <summary>Enables the opening Orbital morph and subsequent MULE call-down policy.</summary>
    public bool EnableMules { get; init; } = true;
    /// <summary>Immediate spends at 50 energy; PreserveScan defers MULEs while the Intelligence reserve is unmet.</summary>
    public string MuleCallDownPolicy { get; init; } = "PreserveScan";

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add("$.version: only version 1 is supported.");
        if (MineralSaturation is < 1 or > 4)
            errors.Add("$.mineralSaturation: must be between 1 and 4 workers per mineral node.");
        if (SupplyDepotWorkerPullMinerals > 1000)
            errors.Add("$.supplyDepotWorkerPullMinerals: must be between 0 and 1000.");
        if (!MuleCallDownPolicy.Equals("Immediate", StringComparison.OrdinalIgnoreCase) &&
            !MuleCallDownPolicy.Equals("PreserveScan", StringComparison.OrdinalIgnoreCase))
            errors.Add("$.muleCallDownPolicy: must be 'Immediate' or 'PreserveScan'.");
        return errors;
    }
}

/// <summary>Observed opening milestones consumed by the Economy Advisor.</summary>
public readonly record struct EconomyAdvisorFrame(
    bool ExpansionCommandCenterPending,
    bool FollowUpDepotPending,
    bool FirstDepotPending,
    bool FirstBarracksPending,
    bool MarineProductionReady,
    bool HoldForFirstDepot);

/// <summary>Costs are sourced from the active SC2 game-data catalog.</summary>
public readonly record struct EconomyAdvisorCosts(
    uint CommandCenter,
    uint SupplyDepot,
    uint Scv,
    uint Barracks,
    uint Marine);

/// <summary>
/// Converts observed economy milestones and user settings into a mineral
/// reservation. It never submits actions or assumes that a reservation makes
/// a build legal.
/// </summary>
public sealed class EconomyAdvisor : IStrategicAdvisor
{
    public AdvisorDomain Domain => AdvisorDomain.Economy;
    public GamePhase CurrentPhase { get; set; } = GamePhase.Early;
    public int ComputeWeight => AdvisorWorkScheduler.GetWeights(CurrentPhase)[Domain];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EconomyAdvisorSettings Settings { get; private set; } = new();

    public IReadOnlyList<string> ConfigureJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new[] { "$: economy settings JSON is required." };

        EconomyAdvisorSettings candidate;
        try
        {
            candidate = JsonSerializer.Deserialize<EconomyAdvisorSettings>(json, JsonOptions)
                ?? throw new JsonException("Economy settings JSON was empty.");
        }
        catch (JsonException exception)
        {
            return new[] { "$: invalid economy settings JSON: " + exception.Message };
        }

        var errors = candidate.Validate();
        if (errors.Count == 0)
            Settings = candidate;
        return errors;
    }

    public bool ShouldEnableOrbitalMorph(bool firstMarineTrainingOrderObserved) =>
        Settings.EnableMules && firstMarineTrainingOrderObserved;

    public uint GetMarineActionReservedMinerals(
        uint reservedMinerals,
        bool firstMarineReady,
        bool firstMarineObserved,
        uint marineMineralCost) =>
        firstMarineReady && !firstMarineObserved && reservedMinerals == marineMineralCost
            ? 0u
            : reservedMinerals;

    public uint GetReservedMinerals(EconomyAdvisorFrame frame, EconomyAdvisorCosts costs)
    {
        if (Settings.HoldWorkerProductionForFirstDepot && frame.HoldForFirstDepot)
            return uint.MaxValue;

        if (!Settings.PrioritizeOpeningInfrastructure)
            return 0;

        if (frame.ExpansionCommandCenterPending)
            return costs.CommandCenter;
        if (frame.FollowUpDepotPending)
            return checked(costs.SupplyDepot + costs.Scv);
        if (frame.FirstDepotPending)
            return costs.SupplyDepot;
        if (frame.FirstBarracksPending)
            return costs.Barracks;
        if (frame.MarineProductionReady)
            return costs.Marine;

        return 0;
    }
}
