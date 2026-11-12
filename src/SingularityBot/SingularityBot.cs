using System.Collections.Generic;
using Google.Protobuf;
using System.Text.Json;
using System.Text.Json.Nodes;
using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot;

/// <summary>
/// Protocol-facing bot shell. Strategic decisions belong to FSM behavior
/// definitions; this class owns the game-loop boundary.
/// </summary>
public sealed class SingularityBot : SC2API_CSharp.Bot, SC2API_CSharp.QueryAwareBot
{
    private readonly UnitBehaviorRuntime _unitBehaviors = new();
    private readonly BuildingBehaviorRuntime _buildings = new();
    private GoalActionPlanDefinition _goalActionPlan = new();
    private readonly EconomyAdvisor _economyAdvisor = new();
    private readonly AdvisorJobBoard _advisorJobBoard = new();
    private readonly StrategicPlanner _strategicPlanner = new();
    private CompositionGoalDefinition? _compositionGoal;
    private readonly GameStateContext _gameState = new();
    private readonly bool _passiveBaseline;
    private readonly bool _writeDiagnostics;
    private readonly Sc2PassiveBaselineRecorder _baselineRecorder = new();
    private readonly Sc2OpeningTraceRecorder _openingTrace = new();
    private readonly Sc2BinaryEventRecorder _binaryTrace = new();
    private uint _lastGameLoop;
    private uint _lastUnitSnapshotGameLoop;

    public SingularityBot(bool passiveBaseline = false, bool writeDiagnostics = false)
    {
        _passiveBaseline = passiveBaseline;
        _writeDiagnostics = writeDiagnostics;
    }
    private readonly WorkerResourceAssignmentPlanner _workerAssignments = new();
    private readonly Sc2WorkerProgressWatchdog _workerProgressWatchdog = new();
    private Sc2GameDataCatalog _gameData;
    private IReadOnlyDictionary<uint, CombatWeaponProfile> _combatWeapons = new Dictionary<uint, CombatWeaponProfile>();
    private uint _playerId;
    private bool _behaviorCatalogRegistered;
    private bool _spatialDiagnosticsReported;
    private readonly Sc2OpeningProductionController _openingProduction = new();
    private readonly Sc2OpeningRallyController _openingRally = new();
    private bool _openingRallyActionSubmitted;
    private bool _openingProductionActionSubmitted;
    private bool _scvTrainingOrderObserved;
    private bool _initialScvProductionObserved;
    private bool _openingBuilderActionSubmitted;
    private bool _openingSprayActionSubmitted;
    private RequestQuery _pendingQuery;
    private readonly Sc2OpeningBuilderController _openingBuilder = new();
    private readonly Sc2OpeningMarineController _openingMarine = new();
    private readonly Sc2MarineRallyController _marineRally = new();
    private readonly Sc2SecondBarracksController _secondBarracks = new();
    private readonly Sc2ExpansionCommandCenterController _expansionCommandCenter = new();
    private bool _expansionCommandCenterActionSubmitted;
    private int _expansionCommandCenterPlacementCount;
    private readonly Sc2OpeningBuilderController _supplyDepotExpansion = new();
    private bool _supplyDepotExpansionActionSubmitted;
    private int _supplyDepotExpansionPlacementCount;
    private readonly Sc2OpeningBuilderController _thirdSupplyDepot = new();
    private bool _thirdSupplyDepotActionSubmitted;
    private int _thirdSupplyDepotPlacementCount;
    private readonly BuildJobRuntime _buildJobs = new();
    private readonly Sc2BarracksProductionController _barracksProduction = new();
    private bool _barracksProductionActionSubmitted;
    private int _openingBuilderPlacementCount;
    private int _openingMarinePlacementCount;
    private int _secondBarracksPlacementCount;
    private bool _secondBarracksActionSubmitted;
    private bool _openingMarineActionSubmitted;
    private readonly Sc2OpeningSprayController _openingSpray = new();
    private readonly Sc2OrbitalSupportController _orbitalSupport = new();
    private readonly Sc2RefineryRuntime _refineries = new();
    private bool _orbitalSupportActionSubmitted;
    private string _lastOrbitalDiagnostic;
    private string _lastStartingTownHallKind;
    private uint? _firstBarracksObservedGameLoop;
    private readonly HashSet<(ulong WorkerTag, uint Generation)> _loggedWorkerAssignments = new();

    /// <summary>Immutable map geometry cached for the lifetime of this match.</summary>
    public SpatialMap SpatialMap { get; private set; }
    public SpatialIntelligence SpatialIntelligence { get; private set; }
    /// <summary>Immutable pathing, placement and elevation layers cached from SC2 OnStart data.</summary>
    public SpatialTerrain SpatialTerrain { get; private set; }
    public IReadOnlyList<string> ProcessGroupsThisFrame => _unitBehaviors.ProcessGroupsThisFrame;
    public GamePhase CurrentGamePhase => _gameState.Phase;
    public IReadOnlyDictionary<AdvisorDomain, int> AdvisorComputeWeights => AdvisorWorkScheduler.GetWeights(_gameState.Phase);
    public IReadOnlyList<StrategicJobPosting> CurrentStrategicPlan => _strategicPlanner.GetPlan(_advisorJobBoard, _lastGameLoop);

    /// <summary>Current FSM_API states for every capability attached to one observed unit.</summary>
    public IReadOnlyDictionary<string, string> GetUnitFsmStates(ulong unitTag) =>
        _unitBehaviors.GetStates(unitTag);

    /// <summary>Live build and production jobs, including their authoritative FSM_API states and evidence.</summary>
    public IReadOnlyCollection<BuildJobContext> CurrentBuildJobs => _buildJobs.Jobs;

    /// <summary>Current SCV production request state.</summary>
    public string OpeningScvProductionState => _openingProduction.CurrentState;
    public string OpeningDepotBuildJobState => _openingBuilder.BuildJobState;
    public string OpeningDepotBuildFailureReason => _openingBuilder.BuildJobFailureReason;
    public string OpeningDepotMovementState => _openingBuilder.MovementState;
    public bool OpeningDepotMoveRetryReady => _openingBuilder.IsMoveRetryReady;
    public string OpeningBarracksBuildJobState => _openingMarine.BarracksBuildJobState;
    public string OpeningMarineProductionJobState => _openingMarine.MarineProductionJobState;
    public IReadOnlyCollection<BuildingBehaviorContext> Buildings => _buildings.Buildings;

    public IReadOnlyDictionary<string, string> GetBuildingFsmStates(ulong buildingTag) =>
        _buildings.TryGet(buildingTag, out var building) && building is not null
            ? building.CurrentStates
            : new Dictionary<string, string>();

    /// <summary>Publishes an explicit passage request; this overrides nearby-enemy Depot closure.</summary>
    public bool RequestSupplyDepotPassage(ulong buildingTag, bool requested = true) =>
        _buildings.RequestDepotPassage(buildingTag, requested);

    /// <summary>Queues a Scanner Sweep request on an Orbital Command at the supplied map position.</summary>
    public bool RequestOrbitalScan(ulong orbitalTag, float targetX, float targetY) =>
        _buildings.RequestOrbitalScan(orbitalTag, targetX, targetY);

    /// <summary>Defines the exclusion zone in which a landed Command Center must not morph to Orbital.</summary>
    public void ConfigureDesignatedCommandCenterConstructionArea(float centerX, float centerY, float radius) =>
        _buildings.ConfigureDesignatedCommandCenterConstructionArea(centerX, centerY, radius);

    public void ClearDesignatedCommandCenterConstructionArea() =>
        _buildings.ClearDesignatedCommandCenterConstructionArea();

    /// <summary>Sets the user-authored goal plan before or during a match.</summary>
    public IReadOnlyList<string> ConfigureGoalActionPlanJson(string json)
    {
        var plan = GoalActionPlanDefinition.Parse(json);
        var errors = plan.Validate();
        if (errors.Count == 0) _goalActionPlan = plan;
        return errors;
    }

    /// <summary>Sets user-authored economy policy before or during a match.</summary>
    public IReadOnlyList<string> ConfigureEconomyAdvisorJson(string json) =>
        _economyAdvisor.ConfigureJson(json);

    /// <summary>Current economy policy exposed to the development GUI.</summary>
    public EconomyAdvisorSettings EconomySettings => _economyAdvisor.Settings;

    /// <summary>Publish or replace an advisor-owned job posting.</summary>
    public StrategicJobPosting PublishAdvisorJob(
        AdvisorDomain owner, string id, StrategicJobKind kind, string target,
        int quantity, int priority, uint? expiresAtGameLoop = null,
        IReadOnlyDictionary<string, string>? constraints = null) =>
        _advisorJobBoard.Upsert(owner, id, kind, target, quantity, priority,
            _lastGameLoop, expiresAtGameLoop, constraints);

    /// <summary>Withdraw one job posting owned by the specified advisor.</summary>
    public bool WithdrawAdvisorJob(AdvisorDomain owner, string id) =>
        _advisorJobBoard.Withdraw(owner, id);

    /// <summary>
    /// Compile a declarative force target into standing unit, producer, add-on
    /// and prerequisite postings. Recipe metadata must be supplied explicitly.
    /// </summary>
    public CompositionPlanResult ConfigureCompositionGoal(
        CompositionGoalDefinition goal,
        IReadOnlyDictionary<string, ProductionRecipe> recipes,
        int priority = 50) =>
        CompositionGoalCompiler.Compile(goal, recipes, _advisorJobBoard, _lastGameLoop, priority);

    /// <summary>Current declarative composition target, if configured.</summary>
    public CompositionGoalDefinition? CompositionGoal => _compositionGoal;

    /// <summary>
    /// JSON authoring surface for the development GUI. Recipe metadata is
    /// explicit input until the normalized game-data tech graph is complete.
    /// </summary>
    public CompositionPlanResult ConfigureCompositionGoalJson(
        string goalJson,
        string recipesJson,
        int priority = 50)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var goal = JsonSerializer.Deserialize<CompositionGoalDefinition>(goalJson, options)
                ?? throw new JsonException("Composition goal JSON was empty.");
            var recipes = JsonSerializer.Deserialize<Dictionary<string, ProductionRecipe>>(recipesJson, options)
                ?? throw new JsonException("Production recipe JSON was empty.");
            var result = CompositionGoalCompiler.Compile(goal, recipes, _advisorJobBoard, _lastGameLoop, priority);
            if (result.Errors.Count == 0)
                _compositionGoal = goal;
            return result;
        }
        catch (JsonException exception)
        {
            return new CompositionPlanResult(Array.Empty<StrategicJobPosting>(),
                new[] { "$: invalid composition configuration: " + exception.Message });
        }
    }

    /// <summary>Withdraw every posting currently owned by the configured goal.</summary>
    public int ClearCompositionGoal()
    {
        if (_compositionGoal is null) return 0;
        var removed = _advisorJobBoard.WithdrawComposition(_compositionGoal.GoalId);
        _compositionGoal = null;
        return removed;
    }

    public void OnStart(
        ResponseGameInfo gameInfo,
        ResponseData data,
        ResponsePing pingResponse,
        ResponseObservation observation,
        uint playerId,
        string opponentId)
    {
        if (_writeDiagnostics)
        {
            WriteOnStartSnapshot(gameInfo, data, pingResponse, observation, playerId, opponentId);
            Sc2Diagnostics.WriteTerrainCalibration(gameInfo);
        }
        if (_passiveBaseline || _writeDiagnostics)
            _baselineRecorder.Start();
        if (_writeDiagnostics)
        {
            _openingTrace.Start(Sc2Diagnostics.OutputDirectory);
            _binaryTrace.Start(Sc2Diagnostics.OutputDirectory);
        }
        _playerId = playerId;
        _gameData = Sc2GameDataCatalog.Create(data);
        _combatWeapons = CombatWeaponProfile.FromCatalog(_gameData);

        if (Sc2SpatialTerrainFactory.TryCreate(gameInfo, out var spatialTerrain))
        {
            SpatialTerrain = spatialTerrain;
            Console.WriteLine($"[Spatial] Terrain layers: pathing={spatialTerrain.Pathing != null}, placement={spatialTerrain.Placement != null}, height={spatialTerrain.TerrainHeight != null}.");
        }
        else
        {
            SpatialTerrain = null;
            Console.WriteLine("[Spatial] SC2 terrain image layers unavailable or invalid; terrain-dependent reasoning remains disabled.");
        }

        if (SpatialMapFactory.TryCreate(gameInfo, observation, out var spatialMap))
        {
            SpatialMap = spatialMap;
            SpatialIntelligence = new SpatialIntelligence(spatialMap);
            Console.WriteLine($"[Spatial] {gameInfo.MapName}: bounds=({spatialMap.Bounds.MinX},{spatialMap.Bounds.MinY})-({spatialMap.Bounds.MaxX},{spatialMap.Bounds.MaxY}), center=({spatialMap.Center.X:F1},{spatialMap.Center.Y:F1}), sectors={spatialMap.Sectors.Count}.");
            Console.WriteLine($"[Spatial] Friendly start=({spatialMap.Side.FriendlyStart.X:F1},{spatialMap.Side.FriendlyStart.Y:F1}); enemy start=({spatialMap.Side.EnemyStart.X:F1},{spatialMap.Side.EnemyStart.Y:F1}).");
        }
        else
        {
            SpatialMap = null;
            SpatialIntelligence = null;
            Console.WriteLine($"[Spatial] Map geometry unresolved for {gameInfo?.MapName ?? "unknown map"}; the bot needs valid playable bounds, a visible starting town hall, and one or two start locations.");
        }

        if (_behaviorCatalogRegistered)
            return;

        UnitBehaviorCatalog.Register();
        BuildingBehaviorRuntime.RegisterDefinitions();
        FSM_API.Create.CreateInstance("SingularityBot.GameState", _gameState, UnitBehaviorCatalog.StrategyProcessingGroup);
        _behaviorCatalogRegistered = true;

        Console.WriteLine($"[Runtime] SC2 game data: units={_gameData.UnitNames.Count}, buffs={_gameData.BuffNames.Count}, smart={_gameData.SmartAbilityId?.ToString() ?? "unavailable"}, spray={_gameData.SprayAbilityId?.ToString() ?? "unavailable"}, rally-workers={_gameData.RallyWorkersAbilityId?.ToString() ?? "unavailable"}, train-SCV={_gameData.ScvTrainAbilityId?.ToString() ?? "unavailable"} (minerals={_gameData.ScvMineralCost}, supply={_gameData.ScvFoodRequired}), move={_gameData.MoveAbilityId?.ToString() ?? "unavailable"}, build-Depot={_gameData.SupplyDepotBuildAbilityId?.ToString() ?? "unavailable"} (minerals={_gameData.SupplyDepotMineralCost}), build-Barracks={_gameData.BarracksBuildAbilityId?.ToString() ?? "unavailable"} (minerals={_gameData.BarracksMineralCost}), train-Marine={_gameData.MarineTrainAbilityId?.ToString() ?? "unavailable"} (minerals={_gameData.MarineMineralCost}, supply={_gameData.MarineFoodRequired}).");
    }

    /// <summary>
    /// Persists the complete protocol payload supplied to OnStart so map and
    /// game-data assumptions can be checked against the authoritative inputs.
    /// </summary>
    private static void WriteOnStartSnapshot(
        ResponseGameInfo gameInfo,
        ResponseData data,
        ResponsePing pingResponse,
        ResponseObservation observation,
        uint playerId,
        string opponentId)
    {
        try
        {
            var snapshot = new JsonObject
            {
                ["capturedAtUtc"] = DateTime.UtcNow.ToString("O"),
                ["playerId"] = playerId,
                ["opponentId"] = opponentId,
                ["gameInfo"] = SerializeProtocolMessage(gameInfo),
                ["data"] = SerializeProtocolMessage(data),
                ["pingResponse"] = SerializeProtocolMessage(pingResponse),
                ["initialObservation"] = SerializeProtocolMessage(observation)
            };

            var diagnosticsDirectory = Sc2Diagnostics.OutputDirectory;
            Directory.CreateDirectory(diagnosticsDirectory);

            var fileName = $"on-start-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.json";
            var path = Path.Combine(diagnosticsDirectory, fileName);
            File.WriteAllText(path, snapshot.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            Console.WriteLine($"[Diagnostics] Complete OnStart protocol snapshot: {path}.");
        }
        catch (Exception exception)
        {
            // Diagnostics must never prevent the bot from entering the match.
            Console.WriteLine($"[Diagnostics] Could not serialize OnStart protocol inputs: {exception.Message}");
        }
    }

    private static JsonNode SerializeProtocolMessage(IMessage message)
    {
        return message == null ? null : JsonNode.Parse(JsonFormatter.Default.Format(message));
    }

    public IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseObservation observation)
    {
        if (_passiveBaseline)
        {
            var baselineObservation = Sc2ObservationNormalizer.Normalize(
                observation,
                _playerId,
                _gameData.UnitNames,
                _gameData.BuffNames);
            _baselineRecorder.Record(baselineObservation);
            return Array.Empty<SC2APIProtocol.Action>();
        }

        // DataSync starts immediately after normalization. Every later decision
        // consumes the same persistent entity registry and frame facts.
        var normalized = Sc2ObservationNormalizer.Normalize(
            observation,
            _playerId,
            _gameData.UnitNames,
            _gameData.BuffNames);
        _lastGameLoop = normalized.GameLoop;
        var intents = new BehaviorIntentBuffer();
        _unitBehaviors.Reconcile(normalized, intents.Add);
        _gameState.Apply(normalized);
        UpdateSpatialIntelligence(observation);
        if (SpatialMap is not null)
            GoalActionPlanner.Apply(_goalActionPlan, _unitBehaviors, SpatialMap);

        if (_writeDiagnostics)
            _baselineRecorder.Record(normalized);

        foreach (var actionError in observation.ActionErrors)
            Console.WriteLine($"[SC2 API] Action error: unit={actionError.UnitTag}, ability={actionError.AbilityId}, result={actionError.Result}.");

        var scvTrainingOrderObserved = _gameData.ScvTrainAbilityId is > 0 && normalized.Units.Any(unit =>
            unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible &&
            (unit.UnitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
             unit.UnitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase)) &&
            unit.Orders.Any(order => order.AbilityId == _gameData.ScvTrainAbilityId.Value));
        var startingTownHall = normalized.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                !normalized.DeadUnitTags.Contains(unit.Tag) &&
                (unit.UnitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(unit => unit.Tag)
            .FirstOrDefault();
        var startingTownHallKind = startingTownHall?.UnitKind ?? "missing";
        if (!string.Equals(_lastStartingTownHallKind, startingTownHallKind, StringComparison.Ordinal))
        {
            Console.WriteLine($"[Orbital] OBSERVED starting town hall: tag={startingTownHall?.Tag.ToString() ?? "none"}, kind={startingTownHallKind}, loop={normalized.GameLoop}.");
            _lastStartingTownHallKind = startingTownHallKind;
        }
        if (scvTrainingOrderObserved && !_scvTrainingOrderObserved)
            Console.WriteLine("[Opening] OBSERVED: Command Center has an SCV training order.");
        _scvTrainingOrderObserved = scvTrainingOrderObserved;
        _initialScvProductionObserved |= scvTrainingOrderObserved;

        var initialWorkerPathingQuery = _workerAssignments.PrepareInitialPathingQuery(normalized);
        var assignments = _workerAssignments.Reconcile(
            normalized, _economyAdvisor.Settings.MineralSaturation);
        var actions = new List<SC2APIProtocol.Action>();
        var buildingAccessActions = new List<SC2APIProtocol.Action>();
        SC2APIProtocol.Action sprayAction = null;
        SC2APIProtocol.Action builderAction = null;
        SC2APIProtocol.Action marineAction = null;
        SC2APIProtocol.Action orbitalSupportAction = null;
        SC2APIProtocol.Action secondBarracksAction = null;
        SC2APIProtocol.Action supplyDepotExpansionAction = null;
        SC2APIProtocol.Action thirdSupplyDepotPendingAction = null;
        SC2APIProtocol.Action expansionCommandCenterAction = null;
        SC2APIProtocol.Action barracksProductionAction = null;
        IReadOnlyList<SC2APIProtocol.Action> refineryActions = Array.Empty<SC2APIProtocol.Action>();
        RequestQuery sprayQuery = null;
        var allowWorkerActions = true;
        var reservedInfrastructureMinerals = 0u;
        uint reservedProductionMinerals = 0;
        // Opening production and mineral routing are independent commands: the
        // first SCV request must not suppress the starting workers' first-frame spread.
        var allowWorkerBehaviorActions = true;
        // Compute this once per frame at the enclosing scope: the Orbital morph
        // observation gates infrastructure below and must be visible outside
        // the optional worker-action block.
        var orbitalAbility = _gameData.OrbitalUpgradeAbilityId;
        var orbitalMorphingObserved = startingTownHall is not null &&
            (startingTownHallKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
             orbitalAbility is > 0 && startingTownHall.Orders.Any(order => order.AbilityId == orbitalAbility.Value));

        // Existing opening controllers are being migrated into FSM capabilities.
        // Until that migration is complete, their gates suppress competing actions,
        // but never suppress the frame's ordered FSM processing groups.
        var rallyAction = _openingRally.TryCreateAction(
            normalized,
            SpatialMap,
            _gameData,
            assignments,
            _openingBuilder.IsDispatched);
        _openingRallyActionSubmitted = rallyAction is not null;
        if (rallyAction is not null)
        {
            actions.Add(rallyAction);
            // Rallying the Command Center does not conflict with explicit
            // starting-worker mineral assignments; keep first-frame routing live.
            _pendingQuery = null;
        }
        var orbitalBarracksReady = normalized.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible && unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
            unit.BuildProgress >= 1f);
        uint orbitalMineralReservation = 0;

        // Worker rally is an optimization, not a production prerequisite. If
        // the optional rally ability is unavailable or rejected, the opening
        // must still produce its first SCV and continue toward construction.
        if (allowWorkerActions)
        {
            var observedBarracks = normalized.Units.Where(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                !normalized.DeadUnitTags.Contains(unit.Tag)).ToArray();
            var secondBarracksUnderConstruction = observedBarracks.Length >= 2 &&
                observedBarracks.Any(unit => unit.BuildProgress < 1f);
            var observedDepotCount = normalized.Units.Count(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)) &&
                !normalized.DeadUnitTags.Contains(unit.Tag));
            var secondBarracksComplete = observedBarracks.Length >= 2 &&
                observedBarracks.All(unit => unit.BuildProgress >= 1f);
            var designatedAreaStreaming = _buildings.HasDesignatedCommandCenterConstructionArea &&
                _buildings.Buildings.Any(building =>
                    building.TownHallExpansionState == "StreamingCommandCenters");
            var expansionProductionEnabledForBudget = _buildings.HasDesignatedCommandCenterConstructionArea
                ? designatedAreaStreaming
                : orbitalMorphingObserved && _secondBarracks.IsComplete;
            // Reserve for each next expansion while the designated Town Hall is
            // in streaming mode. A completed cycle immediately becomes eligible
            // for the next one rather than imposing a two-town-hall ceiling.
            var expansionCommandCenterPending = expansionProductionEnabledForBudget &&
                (!_expansionCommandCenter.IsStarted || _expansionCommandCenter.IsComplete);

            reservedProductionMinerals = _economyAdvisor.GetReservedMinerals(
                new EconomyAdvisorFrame(
                    expansionCommandCenterPending,
                    _secondBarracks.RequiresSupplyDepot || secondBarracksUnderConstruction,
                    !_openingBuilder.IsCompleted && _openingBuilder.BuilderTag is null,
                    _openingBuilder.IsCompleted && !_openingMarine.BarracksComplete,
                    _openingMarine.BarracksComplete && normalized.Units.Any(unit =>
                        unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                        unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                        !unit.HasOrders && !normalized.DeadUnitTags.Contains(unit.Tag)),
                    _initialScvProductionObserved && !_openingBuilder.IsBuildSubmitted && !_openingBuilder.IsCompleted),
                new EconomyAdvisorCosts(
                    _gameData.CommandCenterMineralCost,
                    _gameData.SupplyDepotMineralCost,
                    _gameData.ScvMineralCost,
                    _gameData.BarracksMineralCost,
                    _gameData.MarineMineralCost));

            // Keep the active follow-up Depot affordable before refilling SCVs
            // or optional Marine queues. The third Depot is not represented by
            // the older advisor frame, so reserve its cost explicitly as soon
            // as Depot two is complete and until Depot three is observed complete.
            var followupDepotPending = _secondBarracks.RequiresSupplyDepot && !_thirdSupplyDepot.IsCompleted;
            if (followupDepotPending)
                reservedProductionMinerals = Math.Max(
                    reservedProductionMinerals, _gameData.SupplyDepotMineralCost);

            reservedInfrastructureMinerals = reservedProductionMinerals;
            // The Orbital is the first post-Barracks mineral commitment. Do not let
            // optional Marine or follow-on infrastructure reservations starve it.
            var fsmReadyTownHall = _buildings.Buildings
                .Where(building => building.TownHallUpgradeState == "UpgradeReady")
                .OrderBy(building => building.UnitTag)
                .FirstOrDefault();
            var orbitalReservedCommandCenter = _orbitalSupport.GetCommandCenterReservedForUpgrade(normalized, _gameData);
            var orbitalDepotReady = normalized.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                unit.IsLiveVisible &&
                (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)) &&
                unit.BuildProgress >= 1f);
            var orbitalUpgradeCost = _gameData.OrbitalUpgradeMineralCost;
            orbitalMineralReservation = fsmReadyTownHall is not null &&
                orbitalAbility is > 0 && orbitalUpgradeCost > 0
                    ? orbitalUpgradeCost
                    : 0u;
            var orbitalReservationReason = fsmReadyTownHall is null ? "town-hall-fsm-not-ready" :
                orbitalAbility is not > 0 ? "orbital-ability-unavailable-or-ambiguous" :
                orbitalUpgradeCost == 0 ? "orbital-cost-unavailable" :
                normalized.Minerals < orbitalUpgradeCost ? "accumulating-orbital-minerals" :
                "town-hall-fsm-ready";
            var firstRefineryPending = orbitalMineralReservation > 0 &&
                _gameData.RefineryMineralCost > 0 &&
                !normalized.Units.Any(unit => unit.Alliance == UnitAlliance.Self &&
                    unit.IsLiveVisible && unit.UnitKind.Equals("Refinery", StringComparison.OrdinalIgnoreCase) &&
                    !normalized.DeadUnitTags.Contains(unit.Tag)) &&
                normalized.Units.Any(unit => unit.Alliance == UnitAlliance.Neutral &&
                    unit.IsLiveVisible && unit.UnitKind.Contains("Geyser", StringComparison.OrdinalIgnoreCase) &&
                    !normalized.DeadUnitTags.Contains(unit.Tag));
            var firstRefineryMineralReservation = firstRefineryPending
                ? _gameData.RefineryMineralCost
                : 0u;
            var reservedScvMinerals = (uint)Math.Min(
                uint.MaxValue,
                (ulong)reservedProductionMinerals + orbitalMineralReservation + firstRefineryMineralReservation);
            var orbitalDiagnostic = $"reservation={orbitalReservedCommandCenter?.ToString() ?? "none"};reason={orbitalReservationReason};firstTag={startingTownHall?.Tag.ToString() ?? "none"};firstKind={startingTownHallKind};depot={orbitalDepotReady};barracks={orbitalBarracksReady};ability={orbitalAbility?.ToString() ?? "none"};cost={orbitalUpgradeCost};minerals={normalized.Minerals};reserved={reservedProductionMinerals};refineryReserve={firstRefineryMineralReservation};scvReserve={reservedScvMinerals};loop={normalized.GameLoop}";
            if (!string.Equals(_lastOrbitalDiagnostic, orbitalDiagnostic, StringComparison.Ordinal))
            {
                Console.WriteLine("[Orbital] " + orbitalDiagnostic + ".");
                _lastOrbitalDiagnostic = orbitalDiagnostic;
            }
            var productionAction = _openingProduction.TryCreateScvTrainingAction(
                normalized, _gameData, reservedScvMinerals, orbitalReservedCommandCenter);
            _openingProductionActionSubmitted = productionAction is not null;
            if (productionAction is not null)
            {
                var command = productionAction.ActionRaw.UnitCommand;
                Console.WriteLine($"[Opening] Requesting first SCV: Command Center={command.UnitTags[0]}, ability={command.AbilityId}, minerals={normalized.Minerals}, supply={normalized.FoodUsed}/{normalized.FoodCap}.");
                actions.Add(productionAction);
                _pendingQuery = null;
                // SCV production uses the Command Center; construction, gas,
                // and Orbital controllers use separate units. Do not let one
                // production request suppress the rest of the opening schedule.
            }
        }
        else
        {
            _openingProductionActionSubmitted = false;
        }

        if (allowWorkerActions)
        {
            sprayQuery = _openingSpray.PrepareAbilityQuery(
                normalized,
                assignments,
                _gameData,
                _initialScvProductionObserved,
                _workerAssignments);
            sprayAction = _openingSpray.TryCreateAction(normalized, _gameData, _workerAssignments);
            _openingSprayActionSubmitted = sprayAction is not null;

            var supplementalWorkerTags = assignments.Values
                .Where(assignment => assignment.IsSupplementalWorker)
                .Select(assignment => assignment.WorkerTag)
                .ToHashSet();
            var constructionCrewTags = _unitBehaviors.Contexts
                .Where(context => context.IsWorker &&
                                  context.Capabilities.Contains(UnitBehaviorCatalog.ConstructionFsm) &&
                                  supplementalWorkerTags.Contains(context.UnitTag))
                .Select(context => context.UnitTag)
                .ToHashSet();

            // If the opening has not yet produced any supplemental mineral slots,
            // retain a bootstrap crew rather than blocking the first construction.
            if (constructionCrewTags.Count == 0)
            {
                constructionCrewTags = _unitBehaviors.Contexts
                    .Where(context => context.IsWorker &&
                                      context.Capabilities.Contains(UnitBehaviorCatalog.ConstructionFsm))
                    .Select(context => context.UnitTag)
                    .ToHashSet();
            }

            builderAction = _openingBuilder.TryCreateAction(
                normalized,
                SpatialMap,
                _gameData,
                _workerAssignments,
                constructionCrewTags,
                _buildJobs,
                workerPullMinerals: _economyAdvisor.Settings.SupplyDepotWorkerPullMinerals);
            _openingBuilderActionSubmitted = builderAction is not null;
            if (builderAction is not null)
            {
                var command = builderAction.ActionRaw.UnitCommand;
                var target = command.TargetWorldSpacePos is null
                    ? "none"
                    : $"({command.TargetWorldSpacePos.X:F1},{command.TargetWorldSpacePos.Y:F1})";
                Console.WriteLine($"[Opening] Builder SCV {_openingBuilder.BuilderTag}: ability={command.AbilityId}, target={target}.");
            }

            // Refresh after a builder reservation so this frame cannot emit a
            // stale gather intent for the worker now committed to construction.
            assignments = _workerAssignments.Reconcile(normalized, allowLongDistanceMining: _economyAdvisor.Settings.AllowLongDistanceMining);
            if (_openingBuilder.IsBuildSubmitted || _openingBuilder.IsCompleted)
            {
                // Stage a DIFFERENT SCV at the Barracks site while the Depot is
                // building. The Barracks Build command remains gated on the Depot
                // being observed complete, removing the old Orbital/Barracks cycle.
                var firstMarinePriority = !_openingMarine.MarineTrainingStarted && !_openingMarine.MarineObserved;
                var marineReservedMinerals = firstMarinePriority ? 0u : reservedInfrastructureMinerals;
                marineAction = _openingMarine.TryCreateAction(
                    normalized, SpatialMap, _gameData, _workerAssignments,
                    preferredBuilderTag: null,
                    buildJobs: _buildJobs,
                    reservedMinerals: marineReservedMinerals,
                    excludedBuilderTag: _openingBuilder.BuilderTag,
                    allowConstruction: _openingBuilder.IsCompleted,
                    allowMarineTraining: true);
                _openingMarineActionSubmitted = marineAction is not null;
            }
            else
            {
                _openingMarineActionSubmitted = false;
            }

            // Do not leave the original Depot SCV reserved while another worker
            // constructs the first Barracks. Recycle it immediately when it is not
            // the Barracks builder; if it is the same worker, release after handoff.
            _openingBuilder.ReleaseBuilderIfNotAssignedToBarracks(
                _workerAssignments, _openingMarine.BuilderTag);
            if (_openingMarine.BarracksComplete)
                _openingBuilder.ReleaseBuilderAfterBarracksHandoff(_workerAssignments);

            var firstBarracksObserved = normalized.Units.Any(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                !normalized.DeadUnitTags.Contains(unit.Tag));

            if (firstBarracksObserved)
            {
                _firstBarracksObservedGameLoop ??= normalized.GameLoop;
                // Give the first Barracks opening room to settle before pulling
                // another SCV. Four and a half seconds at SC2's 22.4 loops/sec.
                const uint secondBarracksWorkerPullDelayLoops = 101;
                if (normalized.GameLoop >= _firstBarracksObservedGameLoop.Value + secondBarracksWorkerPullDelayLoops)
                    secondBarracksAction = _secondBarracks.TryCreateAction(
                        normalized, SpatialMap, _gameData, _workerAssignments, constructionCrewTags, _buildJobs);
                _secondBarracksActionSubmitted = secondBarracksAction is not null;
                assignments = _workerAssignments.Reconcile(normalized, allowLongDistanceMining: _economyAdvisor.Settings.AllowLongDistanceMining);

                // The third Depot is part of the post-Orbital infrastructure
                // wave. Unlock it only after the starting Command Center's Orbital
                // morph order is actually observed (not merely when the upgrade is
                // affordable or requested).
                if (_secondBarracks.RequiresSupplyDepot && !_supplyDepotExpansion.IsCompleted)
                {
                    supplyDepotExpansionAction = _supplyDepotExpansion.TryCreateAction(
                        normalized, SpatialMap, _gameData, _workerAssignments, constructionCrewTags,
                        _buildJobs, _openingMarine.ReleasedBuilderTag, releaseBuilderOnCompletion: true,
                        workerPullMinerals: _economyAdvisor.Settings.SupplyDepotWorkerPullMinerals);
                    _supplyDepotExpansionActionSubmitted = supplyDepotExpansionAction is not null;
                    // Keep Barracks three gated until both sequential follow-up Depots are observed complete.
                    _thirdSupplyDepotActionSubmitted = false;
                    assignments = _workerAssignments.Reconcile(normalized, allowLongDistanceMining: _economyAdvisor.Settings.AllowLongDistanceMining);
                }
                else if (_secondBarracks.RequiresSupplyDepot && _supplyDepotExpansion.IsCompleted && !_thirdSupplyDepot.IsCompleted)
                {
                    // Depot two is the post-Orbital expansion Depot. Build Depot
                    // three next, reusing the released Depot-two worker when possible;
                    // the second Refinery remains gated on Depot three completing.
                    supplyDepotExpansionAction = null;
                    _thirdSupplyDepotActionSubmitted = false;
                    var thirdDepotAction = _thirdSupplyDepot.TryCreateAction(
                        normalized, SpatialMap, _gameData, _workerAssignments, constructionCrewTags,
                        _buildJobs, _supplyDepotExpansion.BuilderTag, releaseBuilderOnCompletion: true,
                        workerPullMinerals: _economyAdvisor.Settings.SupplyDepotWorkerPullMinerals);
                    _thirdSupplyDepotActionSubmitted = thirdDepotAction is not null;
                    thirdSupplyDepotPendingAction = thirdDepotAction;
                    if (_thirdSupplyDepot.IsCompleted)
                        _secondBarracks.NotifySupplyDepotCompleted(_thirdSupplyDepot.BuilderTag);
                    assignments = _workerAssignments.Reconcile(normalized, allowLongDistanceMining: _economyAdvisor.Settings.AllowLongDistanceMining);
                }
                else
                {
                    _supplyDepotExpansionActionSubmitted = false;
                    _thirdSupplyDepotActionSubmitted = false;
                }

                // Do not spend on the four-Barracks expansion or a second town hall
                // until SC2 has observed the starting Command Center's Orbital morph
                // order. This keeps the 150-mineral upgrade ahead of optional builds.
                var designatedAreaStreaming = _buildings.HasDesignatedCommandCenterConstructionArea &&
                    _buildings.Buildings.Any(building =>
                        building.TownHallExpansionState == "StreamingCommandCenters");
                var expansionProductionEnabled = _buildings.HasDesignatedCommandCenterConstructionArea
                    ? designatedAreaStreaming
                    : orbitalMorphingObserved && _secondBarracks.IsComplete;
                if (expansionProductionEnabled)
                {
                    expansionCommandCenterAction = _expansionCommandCenter.TryCreateAction(
                        normalized, SpatialMap, _gameData, _workerAssignments, constructionCrewTags,
                        _buildJobs, _secondBarracks.PreferredBuilderTag,
                        _buildings.DesignatedCommandCenterConstructionAreaCenter,
                        _buildings.DesignatedCommandCenterConstructionAreaRadius,
                        orbitalMineralReservation);
                    _expansionCommandCenterActionSubmitted = expansionCommandCenterAction is not null;
                    if (_expansionCommandCenter.IsStarted)
                        _secondBarracks.NotifyCommandCenterStarted();
                    assignments = _workerAssignments.Reconcile(normalized, allowLongDistanceMining: _economyAdvisor.Settings.AllowLongDistanceMining);
                }
                else
                {
                    _expansionCommandCenterActionSubmitted = false;
                }
            }
            else
            {
                _secondBarracksActionSubmitted = false;
                _supplyDepotExpansionActionSubmitted = false;
                _thirdSupplyDepotActionSubmitted = false;
            }

            // The Orbital upgrade is the first post-prerequisite mineral commitment.
            // Hold the first Marine and secondary Barracks until SC2 observes the
            // morph order, so neither can consume the exact 150 minerals first.
            var orbitalCoordinatorBlockers = string.Join(",",
                new[] {
                    builderAction is not null ? "builder" : null
                }.Where(value => value is not null));
            // The Orbital morph is an independent Command Center action. Do not
            // suppress it because an SCV is moving/building elsewhere: doing so
            // allowed the construction worker's repeated commands to starve the
            // one-time 150-mineral upgrade indefinitely. The controller still
            // verifies the observed Depot/Barracks prerequisites and SCV queue.
            orbitalSupportAction = _orbitalSupport.TryCreateAction(
                normalized, _gameData,
                allowOrbitalUpgrade: startingTownHall is not null && startingTownHallKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase),
                permittedCommandCenterTag: startingTownHall?.Tag,
                reservedOrbitalTags: _buildings.ScanReservedOrbitalTags);
            _orbitalSupportActionSubmitted = orbitalSupportAction is not null;
            if (orbitalSupportAction is not null)
            {
                var command = orbitalSupportAction.ActionRaw.UnitCommand;
                Console.WriteLine($"[Orbital] SUBMIT: unit={string.Join(",", command.UnitTags)}, ability={command.AbilityId}, minerals={normalized.Minerals}, reserved={reservedInfrastructureMinerals}, loop={normalized.GameLoop}.");
            }

            // Gas infrastructure starts once the first Barracks is complete; an
            // unavailable Orbital must not hold the gas economy hostage.
            // Each geyser owns an independent FSM; construction targets the geyser
            // unit tag and completed Refineries receive up to three SCVs.
            var friendlyTownHallCount = normalized.Units.Count(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                !normalized.DeadUnitTags.Contains(unit.Tag) &&
                unit.BuildProgress >= 1f &&
                (unit.UnitKind.Equals("CommandCenter", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase) ||
                 unit.UnitKind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase)));
            var completedRefineryCount = normalized.Units.Count(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                !normalized.DeadUnitTags.Contains(unit.Tag) &&
                unit.UnitKind.Equals("Refinery", StringComparison.OrdinalIgnoreCase));
            var refineryUnderConstruction = normalized.Units.Any(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Refinery", StringComparison.OrdinalIgnoreCase) &&
                unit.BuildProgress < 1f && !normalized.DeadUnitTags.Contains(unit.Tag));
            // The first Refinery may begin during the Orbital morph. The
            // third Supply Depot must be completed before we take the second geyser;
            // this keeps the supply investment ahead of the second gas commitment.
            var refineryConstructionAllowed = Sc2RefineryProgressionPolicy.ShouldConstruct(
                orbitalBarracksReady,
                orbitalMorphingObserved,
                completedRefineryCount,
                _thirdSupplyDepot.IsCompleted,
                refineryUnderConstruction);
            refineryActions = _refineries.ReconcileAndCreateActions(
                normalized, _gameData, _workerAssignments, constructionCrewTags,
                allowConstruction: refineryConstructionAllowed,
                reservedMinerals: orbitalMineralReservation);
            assignments = _workerAssignments.Reconcile(normalized, allowLongDistanceMining: _economyAdvisor.Settings.AllowLongDistanceMining);

            // Secondary Barracks produce Marines whenever no higher-priority
            // infrastructure or Orbital action claimed this frame.
            if (orbitalSupportAction is null && secondBarracksAction is null &&
                expansionCommandCenterAction is null && builderAction is null &&
                marineAction is null && sprayAction is null)
            {
                barracksProductionAction = _barracksProduction.TryCreateMarineTrainingAction(
                    normalized, _gameData, _buildJobs, reservedInfrastructureMinerals);
                _barracksProductionActionSubmitted = barracksProductionAction is not null;
            }
            else
            {
                _barracksProductionActionSubmitted = false;
            }

            var placementQuery = _openingBuilder.TakePlacementQuery(_gameData);
            var marinePlacementQuery = _openingBuilder.IsBuildSubmitted || _openingBuilder.IsCompleted
                ? _openingMarine.TakePlacementQuery(_gameData)
                : null;
            var secondBarracksPlacementQuery = _secondBarracks.TakePlacementQuery(_gameData);
            var supplyDepotExpansionPlacementQuery = _secondBarracks.RequiresSupplyDepot && !_supplyDepotExpansion.IsCompleted
                ? _supplyDepotExpansion.TakePlacementQuery(_gameData)
                : null;
            var thirdSupplyDepotPlacementQuery = _secondBarracks.RequiresSupplyDepot && _supplyDepotExpansion.IsCompleted && !_thirdSupplyDepot.IsCompleted
                ? _thirdSupplyDepot.TakePlacementQuery(_gameData)
                : null;
            var expansionCommandCenterPlacementQuery = _secondBarracks.IsComplete && !_expansionCommandCenter.IsComplete
                ? _expansionCommandCenter.TakePlacementQuery(_gameData)
                : null;
            _openingBuilderPlacementCount = placementQuery?.Placements.Count ?? 0;
            _openingMarinePlacementCount = marinePlacementQuery?.Placements.Count ?? 0;
            _secondBarracksPlacementCount = secondBarracksPlacementQuery?.Placements.Count ?? 0;
            _supplyDepotExpansionPlacementCount = supplyDepotExpansionPlacementQuery?.Placements.Count ?? 0;
            _thirdSupplyDepotPlacementCount = thirdSupplyDepotPlacementQuery?.Placements.Count ?? 0;
            _expansionCommandCenterPlacementCount = expansionCommandCenterPlacementQuery?.Placements.Count ?? 0;
            if (sprayQuery is not null || placementQuery is not null || marinePlacementQuery is not null || secondBarracksPlacementQuery is not null || supplyDepotExpansionPlacementQuery is not null || thirdSupplyDepotPlacementQuery is not null || expansionCommandCenterPlacementQuery is not null)
            {
                _pendingQuery = new RequestQuery();
                if (sprayQuery is not null)
                    _pendingQuery.Abilities.Add(sprayQuery.Abilities);
                if (placementQuery is not null)
                    _pendingQuery.Placements.Add(placementQuery.Placements);
                if (marinePlacementQuery is not null)
                    _pendingQuery.Placements.Add(marinePlacementQuery.Placements);
                if (secondBarracksPlacementQuery is not null)
                    _pendingQuery.Placements.Add(secondBarracksPlacementQuery.Placements);
                if (supplyDepotExpansionPlacementQuery is not null)
                    _pendingQuery.Placements.Add(supplyDepotExpansionPlacementQuery.Placements);
                if (thirdSupplyDepotPlacementQuery is not null)
                    _pendingQuery.Placements.Add(thirdSupplyDepotPlacementQuery.Placements);
                if (expansionCommandCenterPlacementQuery is not null)
                    _pendingQuery.Placements.Add(expansionCommandCenterPlacementQuery.Placements);
            }
            else
            {
                _pendingQuery = null;
            }
        }
        else
        {
            _openingSprayActionSubmitted = false;
            _openingBuilderActionSubmitted = false;
            _openingMarineActionSubmitted = false;
            _secondBarracksActionSubmitted = false;
            _supplyDepotExpansionActionSubmitted = false;
            _thirdSupplyDepotActionSubmitted = false;
            _expansionCommandCenterActionSubmitted = false;
            _orbitalSupportActionSubmitted = false;
            _barracksProductionActionSubmitted = false;
        }

        if (initialWorkerPathingQuery is not null)
        {
            _pendingQuery ??= new RequestQuery();
            _pendingQuery.Pathing.Add(initialWorkerPathingQuery.Pathing);
        }

        foreach (var assignment in assignments.Values.OrderBy(item => item.WorkerTag))
        {
            if (_loggedWorkerAssignments.Add((assignment.WorkerTag, assignment.Generation)))
                Console.WriteLine($"[Economy] SCV {assignment.WorkerTag} -> mineral {assignment.MineralTag}; town hall {assignment.TownHallTag}; generation {assignment.Generation}.");
        }

        _unitBehaviors.ApplyWorkerAssignments(assignments, normalized.GameLoop);
        // Associate observed structures with their construction jobs after all
        // construction controllers have reconciled this frame's result evidence.
        _buildings.Reconcile(normalized, _buildJobs.Jobs, IsResearchAbility,
            _gameData.ScvTrainAbilityId, _gameData.OrbitalUpgradeAbilityId);

        // The scheduler advances every registered group in declared order even
        // when an opening gate suppresses outgoing actions for this frame.
        try
        {
            foreach (var pg in ProcessGroupsThisFrame)
            {
                FSM_API.Interaction.Update(pg);
                if (pg == UnitBehaviorCatalog.StrategyProcessingGroup)
                {
                    AdvisorWorkScheduler.Advance(_gameState.Phase);
                    _economyAdvisor.CurrentPhase = _gameState.Phase;
                }
            }
            foreach (var buildingEvent in _buildings.DrainEvents())
            {
                if (buildingEvent.Kind == BuildingEventKind.Operational &&
                    buildingEvent.BuilderWorkerTag is ulong builderTag)
                {
                    _workerAssignments.Unreserve(builderTag);
                    Console.WriteLine($"[Building] {buildingEvent.BuildingId} is operational; SCV {builderTag} returned to worker tasking.");
                }
                else if (buildingEvent.Kind is BuildingEventKind.RaiseSupplyDepot or BuildingEventKind.LowerSupplyDepot)
                {
                    var accessAction = CreateDepotAccessAction(buildingEvent);
                    if (accessAction is not null)
                        buildingAccessActions.Add(accessAction);
                }
                else if (buildingEvent.Kind == BuildingEventKind.ScanRequested)
                {
                    var scanAction = CreateOrbitalScanAction(buildingEvent);
                    if (scanAction is not null && _buildings.MarkScanSubmitted(buildingEvent.BuildingTag, normalized.GameLoop))
                        buildingAccessActions.Add(scanAction);
                }
            }
            if (_writeDiagnostics)
                foreach (var job in _buildJobs.Jobs)
                    _binaryTrace.RecordBuildJob(job, normalized.GameLoop);
            _buildJobs.RetireTerminalJobs();
        }
        finally
        {
            _unitBehaviors.EndFrame();
        }

        if (sprayAction is not null)
            actions.Add(sprayAction);
        if (builderAction is not null)
            actions.Add(builderAction);
        if (marineAction is not null)
            actions.Add(marineAction);
        if (supplyDepotExpansionAction is not null)
            actions.Add(supplyDepotExpansionAction);
        if (thirdSupplyDepotPendingAction is not null)
            actions.Add(thirdSupplyDepotPendingAction);
        if (secondBarracksAction is not null)
            actions.Add(secondBarracksAction);
        if (expansionCommandCenterAction is not null)
            actions.Add(expansionCommandCenterAction);
        if (barracksProductionAction is not null)
            actions.Add(barracksProductionAction);
        if (orbitalSupportAction is not null)
            actions.Add(orbitalSupportAction);
        actions.AddRange(refineryActions);

        if (allowWorkerBehaviorActions)
        {
            var frameIntents = intents.Drain()
                .Where(intent => !_workerAssignments.ShouldSuppressIntent(intent.Key, intent.Value))
                .ToArray();
            actions.AddRange(Sc2ActionTranslator.Translate(
                frameIntents,
                normalized,
                _gameData.SmartAbilityId,
                assignments));
            actions.AddRange(Sc2MarineFsmActionTranslator.Translate(
                frameIntents, _unitBehaviors.Contexts, normalized, _gameData));
        }

        // The opening controller owns the first Marine's cross-map deployment.
        // Once that milestone is observed, tactical control transfers to the
        // deterministic combat planner for every visible friendly Marine.
        if (_openingMarine.MarineDeployed)
        {
            var combatDecisions = MarineCombatPlanner.Plan(normalized, _combatWeapons);
            actions.AddRange(Sc2MarineCombatActionTranslator.Translate(
                combatDecisions, normalized, _gameData, _combatWeapons));
            var liveMarineCount = normalized.Units.Count(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase) &&
                !normalized.DeadUnitTags.Contains(unit.Tag));
            var fourthMarineInProduction = liveMarineCount >= 3 &&
                _gameData.MarineTrainAbilityId is > 0 &&
                normalized.Units.Any(unit =>
                    unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                    unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                    unit.Orders.Any(order => order.AbilityId == _gameData.MarineTrainAbilityId.Value));
            var marineRallyActions = _marineRally.TryCreateActions(
                normalized, SpatialMap, combatDecisions, _gameData, fourthMarineInProduction);
            actions.AddRange(marineRallyActions);
            if (_writeDiagnostics)
                foreach (var decision in combatDecisions)
                    _binaryTrace.RecordMarineDecision(normalized.GameLoop, decision.MarineTag,
                        decision.TargetTag ?? 0, decision.Action.ToString());
            if (_writeDiagnostics && combatDecisions.Count > 0)
            {
                var firing = combatDecisions.Count(item => item.Action == MarineTacticalAction.Fire);
                var repositioning = combatDecisions.Count(item =>
                    item.Action == MarineTacticalAction.RepositionDuringCooldown);
                var withdrawing = combatDecisions.Count(item => item.Action == MarineTacticalAction.Withdraw);
                _openingTrace.RecordEvent("MarineCombat", normalized.GameLoop,
                    $"marines={combatDecisions.Count};fire={firing};reposition={repositioning};withdraw={withdrawing};rally={marineRallyActions.Count}");
                foreach (var decision in combatDecisions)
                    _openingTrace.RecordEvent("MarineDecision", normalized.GameLoop,
                        $"marine={decision.MarineTag};target={decision.TargetTag?.ToString() ?? "none"};action={decision.Action};friendlyDps={decision.FriendlyDps:F2};threatDps={decision.ThreatDps:F2};reason={decision.Reason}");
            }
        }

        // Verify that live SCVs are physically progressing toward their current
        // Move destinations. Do not issue a second command for a worker already
        // commanded by another controller this frame.
        var commandedUnitTags = actions
            .Where(action => action.ActionRaw?.UnitCommand is not null)
            .SelectMany(action => action.ActionRaw.UnitCommand.UnitTags)
            .ToHashSet();
        foreach (var reservedWorker in normalized.Units.Where(unit =>
                     unit.Alliance == UnitAlliance.Self &&
                     unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) &&
                     _workerAssignments.IsReserved(unit.Tag)))
            commandedUnitTags.Add(reservedWorker.Tag);
        actions.AddRange(_workerProgressWatchdog.TryCreateActions(
            normalized, _gameData.MoveAbilityId, commandedUnitTags));

        // Append building access actions after the existing controller action
        // sequence so per-action result correlation for opening controllers stays stable.
        actions.AddRange(buildingAccessActions);

        if (_writeDiagnostics)
        {
            _openingTrace.RecordFrame(normalized, _openingBuilder, builderAction,
                marineAction is not null, SpatialMap is not null, _buildJobs.ActiveCount, _expansionCommandCenter,
                _openingMarine, _secondBarracks, _supplyDepotExpansion, _thirdSupplyDepot);
            _binaryTrace.RecordFrame(normalized.GameLoop, normalized.Minerals,
                normalized.FoodUsed, normalized.FoodCap, normalized.Units.Count, actions.Count);
            _binaryTrace.RecordEconomySnapshot(normalized, _buildJobs.ActiveCount);
            if (_lastUnitSnapshotGameLoop == 0 || normalized.GameLoop >= _lastUnitSnapshotGameLoop + 8)
            {
                _binaryTrace.RecordUnitSnapshots(normalized);
                _lastUnitSnapshotGameLoop = normalized.GameLoop;
            }
            foreach (var action in actions)
                _binaryTrace.RecordAction(normalized.GameLoop, action);
        }

        return actions;
    }

    public void OnActionResult(IReadOnlyList<SC2APIProtocol.Action> submittedActions, ResponseAction response)
    {
        if (response is null || response.Result.Count == 0)
        {
            Console.WriteLine("[SC2 API] Action request returned no per-action result.");
            if (_openingRallyActionSubmitted)
                _openingRally.OnActionResult(null);
            if (_openingProductionActionSubmitted)
                _openingProduction.OnActionResult(null);
            if (_openingSprayActionSubmitted)
                _openingSpray.OnActionResult(null, _workerAssignments);
            if (_openingBuilderActionSubmitted)
                _openingBuilder.OnActionResult(null, _buildJobs);
            if (_openingMarineActionSubmitted)
                _openingMarine.OnActionResult(null);
            if (_supplyDepotExpansionActionSubmitted)
                _supplyDepotExpansion.OnActionResult(null, _buildJobs);
            if (_thirdSupplyDepotActionSubmitted)
                _thirdSupplyDepot.OnActionResult(null, _buildJobs);
            if (_expansionCommandCenterActionSubmitted)
                _expansionCommandCenter.OnActionResult(null, _lastGameLoop);
            if (_secondBarracksActionSubmitted)
                _secondBarracks.OnActionResult(null);
            if (_barracksProductionActionSubmitted)
                _barracksProduction.OnActionResult(null, _buildJobs);
            if (_orbitalSupportActionSubmitted)
                _orbitalSupport.OnActionResult(null);
            _openingRallyActionSubmitted = false;
            _openingProductionActionSubmitted = false;
            _openingSprayActionSubmitted = false;
            _openingBuilderActionSubmitted = false;
            _openingMarineActionSubmitted = false;
            _secondBarracksActionSubmitted = false;
            _supplyDepotExpansionActionSubmitted = false;
            _thirdSupplyDepotActionSubmitted = false;
            _expansionCommandCenterActionSubmitted = false;
            _barracksProductionActionSubmitted = false;
            _orbitalSupportActionSubmitted = false;
            return;
        }

        for (var index = 0; index < response.Result.Count; index++)
        {
            var result = response.Result[index];
            var command = index < submittedActions.Count ? submittedActions[index].ActionRaw?.UnitCommand : null;
            var unitTags = command is null ? string.Empty : string.Join(",", command.UnitTags);
            Console.WriteLine($"[SC2 API] Action {index}: ability={command?.AbilityId.ToString() ?? "unknown"}, units=[{unitTags}], result={result}.");
            if (_writeDiagnostics)
                _openingTrace.RecordEvent("ActionResult", _lastGameLoop, $"index={index};ability={command?.AbilityId};units={unitTags};result={result}");
                _binaryTrace.RecordActionResult(_lastGameLoop, index, unchecked((uint)(command?.AbilityId ?? 0)), result.ToString());
        }
        var resultIndex = 0;
        if (_openingRallyActionSubmitted)
        {
            _openingRally.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null);
            resultIndex++;
        }
        if (_openingProductionActionSubmitted)
        {
            _openingProduction.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null);
            resultIndex++;
        }
        if (_openingSprayActionSubmitted)
        {
            _openingSpray.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null, _workerAssignments);
            resultIndex++;
        }
        if (_openingBuilderActionSubmitted)
        {
            _openingBuilder.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null, _buildJobs);
            resultIndex++;
        }
        if (_openingMarineActionSubmitted)
        {
            _openingMarine.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null);
            resultIndex++;
        }
        if (_supplyDepotExpansionActionSubmitted)
        {
            _supplyDepotExpansion.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null, _buildJobs);
            resultIndex++;
        }
        if (_thirdSupplyDepotActionSubmitted)
        {
            _thirdSupplyDepot.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null, _buildJobs);
            resultIndex++;
        }
        if (_secondBarracksActionSubmitted)
        {
            _secondBarracks.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null);
            resultIndex++;
        }
        if (_expansionCommandCenterActionSubmitted)
        {
            _expansionCommandCenter.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null, _lastGameLoop);
            resultIndex++;
        }
        if (_barracksProductionActionSubmitted)
        {
            _barracksProduction.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null, _buildJobs);
            resultIndex++;
        }
        if (_orbitalSupportActionSubmitted)
        {
            _orbitalSupport.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null);
            resultIndex++;
        }
        _openingRallyActionSubmitted = false;
        _openingProductionActionSubmitted = false;
        _openingSprayActionSubmitted = false;
        _openingBuilderActionSubmitted = false;
        _openingMarineActionSubmitted = false;
        _secondBarracksActionSubmitted = false;
        _supplyDepotExpansionActionSubmitted = false;
        _thirdSupplyDepotActionSubmitted = false;
        _expansionCommandCenterActionSubmitted = false;
        _barracksProductionActionSubmitted = false;
        _orbitalSupportActionSubmitted = false;
    }

    public RequestQuery TakeQueryRequest()
    {
        var query = _pendingQuery;
        _pendingQuery = null;
        return query;
    }

    public void OnQueryResult(ResponseQuery response)
    {
        var validPathingDistances = response?.Pathing
            .Select(item => item.Distance)
            .Where(distance => float.IsFinite(distance) && distance > 0)
            .ToArray() ?? Array.Empty<float>();
        if (_writeDiagnostics)
            _openingTrace.RecordEvent("QueryResult", 0,
                response is null
                    ? "response=null"
                    : $"pathingCount={response.Pathing.Count};pathingValidCount={validPathingDistances.Length};pathingMin={validPathingDistances.DefaultIfEmpty().Min():F2};pathingMax={validPathingDistances.DefaultIfEmpty().Max():F2};abilityCount={response.Abilities.Count};placementCount={response.Placements.Count};results={string.Join("|", response.Placements.Select(item => item.Result))}");
        _workerAssignments.OnInitialPathingQueryResult(response);
        var placementOffset = 0;
        if (_openingBuilderPlacementCount > 0)
        {
            _openingBuilder.OnPlacementQueryResult(SlicePlacementResults(response, placementOffset, _openingBuilderPlacementCount));
            placementOffset += _openingBuilderPlacementCount;
        }
        if (_openingMarinePlacementCount > 0)
        {
            _openingMarine.OnPlacementQueryResult(SlicePlacementResults(response, placementOffset, _openingMarinePlacementCount));
            placementOffset += _openingMarinePlacementCount;
        }
        if (_secondBarracksPlacementCount > 0)
        {
            _secondBarracks.OnPlacementQueryResult(SlicePlacementResults(response, placementOffset, _secondBarracksPlacementCount));
            placementOffset += _secondBarracksPlacementCount;
        }
        if (_supplyDepotExpansionPlacementCount > 0)
        {
            _supplyDepotExpansion.OnPlacementQueryResult(SlicePlacementResults(response, placementOffset, _supplyDepotExpansionPlacementCount));
            placementOffset += _supplyDepotExpansionPlacementCount;
        }
        if (_thirdSupplyDepotPlacementCount > 0)
        {
            _thirdSupplyDepot.OnPlacementQueryResult(SlicePlacementResults(response, placementOffset, _thirdSupplyDepotPlacementCount));
            placementOffset += _thirdSupplyDepotPlacementCount;
        }
        if (_expansionCommandCenterPlacementCount > 0)
        {
            _expansionCommandCenter.OnPlacementQueryResult(SlicePlacementResults(response, placementOffset, _expansionCommandCenterPlacementCount));
            placementOffset += _expansionCommandCenterPlacementCount;
        }
        _openingBuilderPlacementCount = 0;
        _openingMarinePlacementCount = 0;
        _secondBarracksPlacementCount = 0;
        _supplyDepotExpansionPlacementCount = 0;
        _thirdSupplyDepotPlacementCount = 0;
        _expansionCommandCenterPlacementCount = 0;
        _openingSpray.OnQueryResult(response, _workerAssignments);
    }

    private static ResponseQuery SlicePlacementResults(ResponseQuery response, int offset, int count)
    {
        var slice = new ResponseQuery();
        if (response is null || offset < 0 || count <= 0 || offset >= response.Placements.Count)
            return slice;
        slice.Placements.Add(response.Placements.Skip(offset).Take(count));
        return slice;
    }

    private static bool IsResearchAbility(uint abilityId, Sc2GameDataCatalog gameData)
    {
        if (!gameData.TryGetAbility(abilityId, out var ability))
            return false;
        return ability.LinkName.Contains("Research", StringComparison.OrdinalIgnoreCase) ||
               ability.LinkName.Contains("Upgrade", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsResearchAbility(uint abilityId) => IsResearchAbility(abilityId, _gameData);

    private SC2APIProtocol.Action? CreateOrbitalScanAction(BuildingEvent buildingEvent)
    {
        if (buildingEvent.TargetX is not float x || buildingEvent.TargetY is not float y ||
            SpatialMap is null || !SpatialMap.Bounds.Contains(new MapPoint(x, y)))
            return null;

        var abilityIds = _gameData.AbilitiesById.Values
            .Where(ability => ability.LinkName.Contains("ScannerSweep", StringComparison.OrdinalIgnoreCase))
            .Select(ability => ability.AbilityId)
            .Distinct()
            .ToArray();
        if (abilityIds.Length != 1)
            return null;

        var command = new ActionRawUnitCommand
        {
            AbilityId = checked((int)abilityIds[0]),
            TargetWorldSpacePos = new Point2D { X = x, Y = y },
            QueueCommand = false
        };
        command.UnitTags.Add(buildingEvent.BuildingTag);
        return new SC2APIProtocol.Action { ActionRaw = new ActionRaw { UnitCommand = command } };
    }

    private SC2APIProtocol.Action? CreateDepotAccessAction(BuildingEvent buildingEvent)
    {
        var linkName = buildingEvent.Kind == BuildingEventKind.RaiseSupplyDepot
            ? "RaiseSupplyDepot"
            : "LowerSupplyDepot";
        var abilityIds = _gameData.AbilitiesById.Values
            .Where(ability => ability.LinkName.Contains(linkName, StringComparison.OrdinalIgnoreCase))
            .Select(ability => ability.AbilityId)
            .Distinct()
            .ToArray();
        if (abilityIds.Length != 1)
        {
            Console.WriteLine($"[Building] {buildingEvent.BuildingId}: {linkName} ability unavailable or ambiguous; no command emitted.");
            return null;
        }

        var command = new ActionRawUnitCommand
        {
            AbilityId = checked((int)abilityIds[0]),
            QueueCommand = false
        };
        command.UnitTags.Add(buildingEvent.BuildingTag);
        return new SC2APIProtocol.Action { ActionRaw = new ActionRaw { UnitCommand = command } };
    }

    private void UpdateSpatialIntelligence(ResponseObservation response)
    {
        var intelligence = SpatialIntelligence;
        var raw = response?.Observation?.RawData;
        if (intelligence == null || raw == null)
            return;

        var visibleContacts = new List<SpatialContact>();
        foreach (var unit in raw.Units)
        {
            if (unit.DisplayType != DisplayType.Visible || unit.Pos == null)
                continue;

            SpatialAlliance alliance;
            switch (unit.Alliance)
            {
                case Alliance.Self:
                case Alliance.Ally:
                    alliance = SpatialAlliance.Friendly;
                    break;
                case Alliance.Enemy:
                    alliance = SpatialAlliance.Enemy;
                    break;
                case Alliance.Neutral:
                    alliance = SpatialAlliance.Neutral;
                    break;
                default:
                    continue;
            }

            var position = new MapPoint(unit.Pos.X, unit.Pos.Y);
            if (!SpatialMap.Bounds.Contains(position))
                continue;

            visibleContacts.Add(new SpatialContact(
                unit.Tag,
                unit.UnitType,
                position,
                alliance,
                ContactVisibility.Visible,
                response.Observation.GameLoop));
        }

        intelligence.Update(
            response.Observation.GameLoop,
            visibleContacts,
            raw.Event?.DeadUnits);

        if (!_spatialDiagnosticsReported)
        {
            Console.WriteLine($"[Spatial] First index: friendly={intelligence.VisibleFriendlyCount}, enemy={intelligence.VisibleEnemyCount}, remembered-enemy={intelligence.RememberedEnemyCount}, known={intelligence.KnownContactCount}.");
            _spatialDiagnosticsReported = true;
        }
    }

    public void OnEnd(ResponseObservation observation, Result result)
    {
        if (_writeDiagnostics)
            _baselineRecorder.Write(
                Sc2Diagnostics.OutputDirectory,
                _passiveBaseline ? "passive-baseline" : "active-run");
        _unitBehaviors.RemoveAll();
        _buildings.Clear();
        _openingProduction.Dispose();
        _openingBuilder.Dispose();
        _supplyDepotExpansion.Dispose();
        _thirdSupplyDepot.Dispose();
        _buildJobs.Clear();
        _workerAssignments.Clear();
        _loggedWorkerAssignments.Clear();
        _openingTrace.Dispose();
        _binaryTrace.Dispose();
    }
}
