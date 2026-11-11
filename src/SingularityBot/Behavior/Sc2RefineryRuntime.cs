#nullable enable annotations

using SC2APIProtocol;
using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>
/// Persistent state for one physical Vespene geyser site. The site survives
/// Refinery destruction so depletion is not confused with a lost structure.
/// </summary>
public sealed class RefinerySiteContext : IStateContext
{
    public RefinerySiteContext(ulong geyserTag, float x, float y, uint gameLoop)
    {
        if (geyserTag == 0) throw new ArgumentOutOfRangeException(nameof(geyserTag));
        GeyserTag = geyserTag;
        PositionX = x;
        PositionY = y;
        LastObservedGameLoop = gameLoop;
        Name = $"RefinerySite:{geyserTag}";
        IsValid = true;
    }

    public ulong GeyserTag { get; }
    public string Name { get; set; }
    public bool IsValid { get; set; }
    public float PositionX { get; private set; }
    public float PositionY { get; private set; }
    public uint LastObservedGameLoop { get; private set; }
    public ulong? RefineryTag { get; private set; }
    public uint RemainingVespene { get; private set; }
    public bool HasObservedGeyser { get; private set; }
    public bool IsBuilding { get; private set; }
    public bool IsOperational { get; private set; }
    public bool IsDepleted { get; private set; }
    public bool IsDestroyed { get; private set; }
    public ulong? BuilderTag { get; private set; }
    public HashSet<ulong> GasWorkerTags { get; } = new();

    public void ObserveGeyser(UnitObservationFacts geyser, uint gameLoop)
    {
        PositionX = geyser.PositionX;
        PositionY = geyser.PositionY;
        RemainingVespene = geyser.VespeneContents;
        HasObservedGeyser = true;
        IsDestroyed = false;
        LastObservedGameLoop = gameLoop;
    }

    public void BeginConstruction(ulong builderTag, uint gameLoop)
    {
        BuilderTag = builderTag;
        IsBuilding = true;
        IsOperational = false;
        IsDestroyed = false;
        LastObservedGameLoop = gameLoop;
    }

    public void ObserveRefinery(UnitObservationFacts refinery, uint gameLoop)
    {
        RefineryTag = refinery.Tag;
        PositionX = refinery.PositionX;
        PositionY = refinery.PositionY;
        RemainingVespene = refinery.VespeneContents;
        IsBuilding = refinery.BuildProgress < 1f;
        IsOperational = refinery.BuildProgress >= 1f && RemainingVespene > 0;
        IsDepleted = RemainingVespene == 0;
        IsDestroyed = false;
        LastObservedGameLoop = gameLoop;
        if (!IsBuilding) BuilderTag = null;
    }

    public void ObserveRefineryLost(uint gameLoop)
    {
        RefineryTag = null;
        IsBuilding = false;
        IsOperational = false;
        IsDestroyed = true;
        LastObservedGameLoop = gameLoop;
        if (RemainingVespene == 0) IsDepleted = true;
    }

    public void MarkBuildRejected(uint gameLoop)
    {
        IsBuilding = false;
        BuilderTag = null;
        LastObservedGameLoop = gameLoop;
    }

    public void MarkDepleted(uint gameLoop)
    {
        RemainingVespene = 0;
        IsDepleted = true;
        IsOperational = false;
        LastObservedGameLoop = gameLoop;
    }
}

/// <summary>
/// Owns one FSM per geyser site, issues Terran Refinery construction against
/// the geyser unit tag, and assigns up to three SCVs to each completed Refinery.
/// </summary>
public sealed class Sc2RefineryRuntime
{
    public const string FsmName = "SingularityBot.RefinerySite";
    private const float SiteMatchDistanceSquared = 6.25f;
    private const int GasWorkerCapacity = 3;
    private readonly Dictionary<ulong, RefinerySiteContext> _sites = new();
    private readonly Dictionary<ulong, FSMHandle> _handles = new();
    private readonly Dictionary<ulong, uint> _buildSubmittedAt = new();
    private static bool _registered;

    public IReadOnlyCollection<RefinerySiteContext> Sites => _sites.Values.ToArray();

    public static void RegisterDefinition()
    {
        if (_registered) return;
        FSM_API.Create.CreateFiniteStateMachine(FsmName, processRate: -1,
                processingGroup: UnitBehaviorCatalog.ProductionProcessingGroup)
            .State("Available", null, null, null)
            .State("Building", null, null, null)
            .State("Operational", null, null, null)
            .State("Depleted", null, null, null)
            .Transition("Available", "Building", ctx => ctx is RefinerySiteContext site && site.IsBuilding)
            .Transition("Available", "Depleted", ctx => ctx is RefinerySiteContext site && site.IsDepleted)
            .Transition("Building", "Operational", ctx => ctx is RefinerySiteContext site && site.IsOperational)
            .Transition("Building", "Available", ctx => ctx is RefinerySiteContext site && !site.IsBuilding && !site.IsDepleted)
            .Transition("Building", "Depleted", ctx => ctx is RefinerySiteContext site && site.IsDepleted)
            .Transition("Operational", "Depleted", ctx => ctx is RefinerySiteContext site && site.IsDepleted)
            .Transition("Operational", "Available", ctx => ctx is RefinerySiteContext site && site.IsDestroyed && !site.IsDepleted)
            .WithInitialState("Available")
            .BuildDefinition();
        _registered = true;
    }

    public IReadOnlyList<SC2APIProtocol.Action> ReconcileAndCreateActions(
        NormalizedObservation observation,
        Sc2GameDataCatalog gameData,
        WorkerResourceAssignmentPlanner assignments,
        IReadOnlySet<ulong> constructionCrewTags,
        bool allowConstruction)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(constructionCrewTags);
        if (!_registered) throw new InvalidOperationException("Refinery-site FSM must be registered before the match.");

        var geysers = observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Neutral && unit.IsLiveVisible && unit.HasPosition &&
            IsGeyser(unit.UnitKind) && !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();
        foreach (var geyser in geysers)
        {
            var site = FindNearestSite(geyser.PositionX, geyser.PositionY) ??
                       FindOrCreateSite(geyser.Tag, geyser.PositionX, geyser.PositionY, observation.GameLoop);
            site.ObserveGeyser(geyser, observation.GameLoop);
        }

        var refineries = observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible && unit.HasPosition &&
            unit.UnitKind.Equals("Refinery", StringComparison.OrdinalIgnoreCase) &&
            !observation.DeadUnitTags.Contains(unit.Tag)).ToArray();
        foreach (var refinery in refineries)
        {
            var site = FindNearestSite(refinery.PositionX, refinery.PositionY) ??
                       FindOrCreateSite(refinery.Tag, refinery.PositionX, refinery.PositionY, observation.GameLoop);
            var completedBuilderTag = site.BuilderTag;
            site.ObserveRefinery(refinery, observation.GameLoop);
            _buildSubmittedAt.Remove(site.GeyserTag);
            if (completedBuilderTag is ulong builderTag)
                assignments.Unreserve(builderTag);
        }

        foreach (var site in _sites.Values)
        {
            if (site.RefineryTag is ulong refineryTag && observation.DeadUnitTags.Contains(refineryTag))
            {
                site.ObserveRefineryLost(observation.GameLoop);
                ReleaseGasWorkers(site, assignments);
                _buildSubmittedAt.Remove(site.GeyserTag);
            }
            else if (site.IsBuilding && _buildSubmittedAt.TryGetValue(site.GeyserTag, out var submittedAt) &&
                     observation.GameLoop > submittedAt + 44 &&
                     !refineries.Any(refinery => IsNear(refinery, site.PositionX, site.PositionY)))
            {
                // A command that never materializes must not permanently lock the geyser.
                site.MarkBuildRejected(observation.GameLoop);
                if (site.BuilderTag is ulong failedBuilder) assignments.Unreserve(failedBuilder);
                _buildSubmittedAt.Remove(site.GeyserTag);
            }

            if (site.IsDepleted)
                ReleaseGasWorkers(site, assignments);
            else if (site.RemainingVespene == 0 && (site.IsOperational || site.IsDestroyed))
            {
                site.MarkDepleted(observation.GameLoop);
                ReleaseGasWorkers(site, assignments);
            }
        }

        var actions = new List<SC2APIProtocol.Action>();
        var refineryAbility = gameData.RefineryBuildAbilityId;
        if (allowConstruction && refineryAbility is > 0 && gameData.RefineryMineralCost > 0)
        {
            foreach (var site in _sites.Values.OrderBy(site => DistanceToNearestTownHall(site, observation)))
            {
                if (site.IsDepleted || site.IsBuilding || site.IsOperational ||
                    site.RefineryTag.HasValue || !geysers.Any(geyser => geyser.Tag == site.GeyserTag) ||
                    _buildSubmittedAt.ContainsKey(site.GeyserTag) ||
                    observation.Minerals < gameData.RefineryMineralCost)
                    continue;

                var builder = observation.Units
                    .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                                   unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) &&
                                   unit.HasPosition && !unit.HasCargo &&
                                   !assignments.IsReserved(unit.Tag) &&
                                   constructionCrewTags.Contains(unit.Tag) &&
                                   !observation.DeadUnitTags.Contains(unit.Tag))
                    .OrderBy(unit => DistanceSquared(unit.PositionX, unit.PositionY, site.PositionX, site.PositionY))
                    .ThenBy(unit => unit.Tag)
                    .FirstOrDefault();
                if (builder is null) continue;

                assignments.Reserve(builder.Tag);
                site.BeginConstruction(builder.Tag, observation.GameLoop);
                _buildSubmittedAt[site.GeyserTag] = observation.GameLoop;
                actions.Add(new SC2APIProtocol.Action
                {
                    ActionRaw = new ActionRaw
                    {
                        UnitCommand = new UnitCommand
                        {
                            AbilityId = checked((int)refineryAbility.Value),
                            UnitTags = { builder.Tag },
                            TargetUnitTag = site.GeyserTag,
                            QueueCommand = false
                        }
                    }
                });
                Console.WriteLine($"[Refinery] SCV {builder.Tag} ordered to build Refinery on geyser tag {site.GeyserTag}.");
                break; // One new gas structure per frame; other sites remain independently eligible.
            }
        }

        if (gameData.SmartAbilityId is > 0)
        {
            foreach (var site in _sites.Values.Where(site => site.IsOperational && !site.IsDepleted))
            {
                var refinery = refineries.FirstOrDefault(unit => unit.Tag == site.RefineryTag);
                if (refinery is null) continue;

                // Adopt workers already gathering this Refinery before assigning new ones.
                foreach (var worker in observation.Units.Where(unit =>
                             unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                             unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) &&
                             unit.Orders.Any(order => order.TargetUnitTag == refinery.Tag)))
                {
                    if (site.GasWorkerTags.Add(worker.Tag))
                        assignments.Reserve(worker.Tag);
                }

                while (site.GasWorkerTags.Count < GasWorkerCapacity)
                {
                    var worker = observation.Units
                        .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                                       unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) &&
                                       unit.HasPosition && !unit.HasCargo &&
                                       !assignments.IsReserved(unit.Tag) &&
                                       !observation.DeadUnitTags.Contains(unit.Tag))
                        .OrderBy(unit => DistanceSquared(unit.PositionX, unit.PositionY, refinery.PositionX, refinery.PositionY))
                        .ThenBy(unit => unit.Tag)
                        .FirstOrDefault();
                    if (worker is null) break;

                    assignments.Reserve(worker.Tag);
                    site.GasWorkerTags.Add(worker.Tag);
                    actions.Add(new SC2APIProtocol.Action
                    {
                        ActionRaw = new ActionRaw
                        {
                            UnitCommand = new UnitCommand
                            {
                                AbilityId = checked((int)gameData.SmartAbilityId.Value),
                                UnitTags = { worker.Tag },
                                TargetUnitTag = refinery.Tag,
                                QueueCommand = false
                            }
                        }
                    });
                    Console.WriteLine($"[Refinery] Assigned SCV {worker.Tag} to Refinery {refinery.Tag} ({site.GasWorkerTags.Count}/{GasWorkerCapacity}).");
                }

                foreach (var deadWorker in site.GasWorkerTags.Where(tag => observation.DeadUnitTags.Contains(tag)).ToArray())
                {
                    site.GasWorkerTags.Remove(deadWorker);
                    assignments.Unreserve(deadWorker);
                }
            }
        }

        return actions;
    }

    private RefinerySiteContext FindOrCreateSite(ulong tag, float x, float y, uint gameLoop)
    {
        if (_sites.TryGetValue(tag, out var existing)) return existing;
        var site = new RefinerySiteContext(tag, x, y, gameLoop);
        _sites.Add(tag, site);
        _handles.Add(tag, FSM_API.Create.CreateInstance(FsmName, site, UnitBehaviorCatalog.ProductionProcessingGroup));
        return site;
    }

    private RefinerySiteContext? FindNearestSite(float x, float y) =>
        _sites.Values.Where(site => DistanceSquared(site.PositionX, site.PositionY, x, y) <= SiteMatchDistanceSquared)
            .OrderBy(site => DistanceSquared(site.PositionX, site.PositionY, x, y))
            .ThenBy(site => site.GeyserTag)
            .FirstOrDefault();

    private static bool IsGeyser(string kind) =>
        kind.Contains("VespeneGeyser", StringComparison.OrdinalIgnoreCase);

    private static bool IsNear(UnitObservationFacts unit, float x, float y) =>
        DistanceSquared(unit.PositionX, unit.PositionY, x, y) <= SiteMatchDistanceSquared;

    private static double DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return (double)dx * dx + (double)dy * dy;
    }

    private static double DistanceToNearestTownHall(RefinerySiteContext site, NormalizedObservation observation) =>
        observation.Units.Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                (unit.UnitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase)))
            .Select(unit => DistanceSquared(site.PositionX, site.PositionY, unit.PositionX, unit.PositionY))
            .DefaultIfEmpty(double.MaxValue)
            .Min();

    private static void ReleaseGasWorkers(RefinerySiteContext site, WorkerResourceAssignmentPlanner assignments)
    {
        foreach (var workerTag in site.GasWorkerTags)
            assignments.Unreserve(workerTag);
        site.GasWorkerTags.Clear();
    }
}
