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
    private GoalActionPlanDefinition _goalActionPlan = new();
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
    private readonly Sc2SecondBarracksController _secondBarracks = new();
    private readonly Sc2ExpansionCommandCenterController _expansionCommandCenter = new();
    private bool _expansionCommandCenterActionSubmitted;
    private int _expansionCommandCenterPlacementCount;
    private readonly Sc2OpeningBuilderController _supplyDepotExpansion = new();
    private bool _supplyDepotExpansionActionSubmitted;
    private int _supplyDepotExpansionPlacementCount;
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
    private bool _orbitalSupportActionSubmitted;
    private readonly HashSet<(ulong WorkerTag, uint Generation)> _loggedWorkerAssignments = new();

    /// <summary>Immutable map geometry cached for the lifetime of this match.</summary>
    public SpatialMap SpatialMap { get; private set; }
    public SpatialIntelligence SpatialIntelligence { get; private set; }
    /// <summary>Immutable pathing, placement and elevation layers cached from SC2 OnStart data.</summary>
    public SpatialTerrain SpatialTerrain { get; private set; }
    public IReadOnlyList<string> ProcessGroupsThisFrame => _unitBehaviors.ProcessGroupsThisFrame;

    /// <summary>Sets the user-authored goal plan before or during a match.</summary>
    public IReadOnlyList<string> ConfigureGoalActionPlanJson(string json)
    {
        var plan = GoalActionPlanDefinition.Parse(json);
        var errors = plan.Validate();
        if (errors.Count == 0) _goalActionPlan = plan;
        return errors;
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
        if (scvTrainingOrderObserved && !_scvTrainingOrderObserved)
            Console.WriteLine("[Opening] OBSERVED: Command Center has an SCV training order.");
        _scvTrainingOrderObserved = scvTrainingOrderObserved;
        _initialScvProductionObserved |= scvTrainingOrderObserved;

        var initialWorkerPathingQuery = _workerAssignments.PrepareInitialPathingQuery(normalized);
        var assignments = _workerAssignments.Reconcile(normalized);
        var actions = new List<SC2APIProtocol.Action>();
        SC2APIProtocol.Action sprayAction = null;
        SC2APIProtocol.Action builderAction = null;
        SC2APIProtocol.Action marineAction = null;
        SC2APIProtocol.Action orbitalSupportAction = null;
        SC2APIProtocol.Action secondBarracksAction = null;
        SC2APIProtocol.Action supplyDepotExpansionAction = null;
        SC2APIProtocol.Action expansionCommandCenterAction = null;
        SC2APIProtocol.Action barracksProductionAction = null;
        RequestQuery sprayQuery = null;
        var allowWorkerActions = true;
        var reservedInfrastructureMinerals = 0u;
        // Opening production and mineral routing are independent commands: the
        // first SCV request must not suppress the starting workers' first-frame spread.
        var allowWorkerBehaviorActions = true;

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
        // Worker rally is an optimization, not a production prerequisite. If
        // the optional rally ability is unavailable or rejected, the opening
        // must still produce its first SCV and continue toward construction.
        if (allowWorkerActions)
        {
            var reservedProductionMinerals = 0u;
            var observedBarracks = normalized.Units.Where(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                !normalized.DeadUnitTags.Contains(unit.Tag)).ToArray();
            var secondBarracksUnderConstruction = observedBarracks.Length >= 2 &&
                observedBarracks.Any(unit => unit.BuildProgress < 1f);
            if (_secondBarracks.RequiresCommandCenter)
                reservedProductionMinerals = _gameData.CommandCenterMineralCost;
            else if (_secondBarracks.RequiresSupplyDepot || secondBarracksUnderConstruction)
                reservedProductionMinerals = _gameData.SupplyDepotMineralCost;
            else if (!_openingBuilder.IsCompleted && _openingBuilder.BuilderTag is null)
                reservedProductionMinerals = _gameData.SupplyDepotMineralCost;
            else if (_openingBuilder.IsCompleted && !_openingMarine.BarracksComplete)
                reservedProductionMinerals = _gameData.BarracksMineralCost;
            else if (_openingMarine.BarracksComplete &&
                     normalized.Units.Any(unit =>
                         unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                         unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) &&
                         !unit.HasOrders && !normalized.DeadUnitTags.Contains(unit.Tag)))
                reservedProductionMinerals = _gameData.MarineMineralCost;

            reservedInfrastructureMinerals = reservedProductionMinerals;
            var productionAction = _openingProduction.TryCreateScvTrainingAction(
                normalized, _gameData, reservedProductionMinerals);
            _openingProductionActionSubmitted = productionAction is not null;
            if (productionAction is not null)
            {
                var command = productionAction.ActionRaw.UnitCommand;
                Console.WriteLine($"[Opening] Requesting first SCV: Command Center={command.UnitTags[0]}, ability={command.AbilityId}, minerals={normalized.Minerals}, supply={normalized.FoodUsed}/{normalized.FoodCap}.");
                actions.Add(productionAction);
                _pendingQuery = null;
                allowWorkerActions = false;
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
                _buildJobs);
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
            assignments = _workerAssignments.Reconcile(normalized);
            if (builderAction is null && _openingBuilder.IsCompleted)
            {
                marineAction = _openingMarine.TryCreateAction(
                    normalized, SpatialMap, _gameData, _workerAssignments, _openingBuilder.BuilderTag, _buildJobs);
                _openingMarineActionSubmitted = marineAction is not null;
            }
            else
            {
                _openingMarineActionSubmitted = false;
            }

            if (_openingMarine.BarracksComplete && marineAction is null)
            {
                // Always reconcile the current Barracks job. The controller can
                // discover Barracks two starting/finishing while the second Depot
                // is being built by the recycled first-Barracks SCV.
                secondBarracksAction = _secondBarracks.TryCreateAction(
                    normalized, SpatialMap, _gameData, _workerAssignments, constructionCrewTags, _buildJobs);
                _secondBarracksActionSubmitted = secondBarracksAction is not null;
                assignments = _workerAssignments.Reconcile(normalized);

                if (_secondBarracks.RequiresSupplyDepot)
                {
                    supplyDepotExpansionAction = _supplyDepotExpansion.TryCreateAction(
                        normalized, SpatialMap, _gameData, _workerAssignments, constructionCrewTags,
                        _buildJobs, _openingMarine.ReleasedBuilderTag, releaseBuilderOnCompletion: true);
                    _supplyDepotExpansionActionSubmitted = supplyDepotExpansionAction is not null;
                    if (_supplyDepotExpansion.IsCompleted)
                        _secondBarracks.NotifySupplyDepotCompleted(_supplyDepotExpansion.BuilderTag);
                    assignments = _workerAssignments.Reconcile(normalized);
                }
                else
                {
                    _supplyDepotExpansionActionSubmitted = false;
                }

                if (_secondBarracks.RequiresCommandCenter)
                {
                    expansionCommandCenterAction = _expansionCommandCenter.TryCreateAction(
                        normalized, SpatialMap, _gameData, _workerAssignments, constructionCrewTags,
                        _buildJobs, _secondBarracks.PreferredBuilderTag);
                    _expansionCommandCenterActionSubmitted = expansionCommandCenterAction is not null;
                    if (_expansionCommandCenter.IsStarted)
                        _secondBarracks.NotifyCommandCenterStarted();
                    assignments = _workerAssignments.Reconcile(normalized);
                }
                else
                {
                    _expansionCommandCenterActionSubmitted = false;
                }
            }
            else
            {
                _secondBarracksActionSubmitted = false;
            }

            // Secondary Barracks are persistent Marine factories. Keep them ahead
            // of optional Orbital spending so the Marine economy gets first claim.
            if (secondBarracksAction is null && expansionCommandCenterAction is null && builderAction is null && marineAction is null && sprayAction is null)
            {
                barracksProductionAction = _barracksProduction.TryCreateMarineTrainingAction(normalized, _gameData, _buildJobs, reservedInfrastructureMinerals);
                _barracksProductionActionSubmitted = barracksProductionAction is not null;
            }
            else
            {
                _barracksProductionActionSubmitted = false;
            }

            // Orbital upgrades/MULEs are lower priority than SCV, construction,
            // and Marine production. This avoids spending the same minerals twice.
            if (builderAction is null && marineAction is null && sprayAction is null && barracksProductionAction is null && expansionCommandCenterAction is null)
            {
                orbitalSupportAction = _orbitalSupport.TryCreateAction(normalized, _gameData, reservedInfrastructureMinerals);
                _orbitalSupportActionSubmitted = orbitalSupportAction is not null;
            }
            else
            {
                _orbitalSupportActionSubmitted = false;
            }

            var placementQuery = _openingBuilder.TakePlacementQuery(_gameData);
            var marinePlacementQuery = _openingBuilder.IsCompleted
                ? _openingMarine.TakePlacementQuery(_gameData)
                : null;
            var secondBarracksPlacementQuery = _secondBarracks.TakePlacementQuery(_gameData);
            var supplyDepotExpansionPlacementQuery = _secondBarracks.RequiresSupplyDepot
                ? _supplyDepotExpansion.TakePlacementQuery(_gameData)
                : null;
            var expansionCommandCenterPlacementQuery = _secondBarracks.RequiresCommandCenter
                ? _expansionCommandCenter.TakePlacementQuery(_gameData)
                : null;
            _openingBuilderPlacementCount = placementQuery?.Placements.Count ?? 0;
            _openingMarinePlacementCount = marinePlacementQuery?.Placements.Count ?? 0;
            _secondBarracksPlacementCount = secondBarracksPlacementQuery?.Placements.Count ?? 0;
            _supplyDepotExpansionPlacementCount = supplyDepotExpansionPlacementQuery?.Placements.Count ?? 0;
            _expansionCommandCenterPlacementCount = expansionCommandCenterPlacementQuery?.Placements.Count ?? 0;
            if (sprayQuery is not null || placementQuery is not null || marinePlacementQuery is not null || secondBarracksPlacementQuery is not null || supplyDepotExpansionPlacementQuery is not null || expansionCommandCenterPlacementQuery is not null)
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

        // The scheduler advances every registered group in declared order even
        // when an opening gate suppresses outgoing actions for this frame.
        try
        {
            foreach (var pg in ProcessGroupsThisFrame)
                FSM_API.Interaction.Update(pg);
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
        if (secondBarracksAction is not null)
            actions.Add(secondBarracksAction);
        if (expansionCommandCenterAction is not null)
            actions.Add(expansionCommandCenterAction);
        if (barracksProductionAction is not null)
            actions.Add(barracksProductionAction);
        if (orbitalSupportAction is not null)
            actions.Add(orbitalSupportAction);

        if (allowWorkerBehaviorActions)
        {
            var workerIntents = intents.Drain()
                .Where(intent => !_workerAssignments.ShouldSuppressIntent(intent.Key, intent.Value))
                .ToArray();
            actions.AddRange(Sc2ActionTranslator.Translate(
                workerIntents,
                normalized,
                _gameData.SmartAbilityId,
                assignments));
        }

        // The opening controller owns the first Marine's cross-map deployment.
        // Once that milestone is observed, tactical control transfers to the
        // deterministic combat planner for every visible friendly Marine.
        if (_openingMarine.MarineDeployed)
        {
            var combatDecisions = MarineCombatPlanner.Plan(normalized, _combatWeapons);
            actions.AddRange(Sc2MarineCombatActionTranslator.Translate(
                combatDecisions, normalized, _gameData, _combatWeapons));
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
                    $"marines={combatDecisions.Count};fire={firing};reposition={repositioning};withdraw={withdrawing}");
            }
        }

        if (_writeDiagnostics)
        {
            _openingTrace.RecordFrame(normalized, _openingBuilder, builderAction,
                marineAction is not null, SpatialMap is not null, _buildJobs.ActiveCount);
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
                _openingMarine.OnActionResult(null, _buildJobs);
            if (_supplyDepotExpansionActionSubmitted)
                _supplyDepotExpansion.OnActionResult(null, _buildJobs);
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
            _openingMarine.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null, _buildJobs);
            resultIndex++;
        }
        if (_supplyDepotExpansionActionSubmitted)
        {
            _supplyDepotExpansion.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null, _buildJobs);
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
        if (_expansionCommandCenterPlacementCount > 0)
        {
            _expansionCommandCenter.OnPlacementQueryResult(SlicePlacementResults(response, placementOffset, _expansionCommandCenterPlacementCount));
            placementOffset += _expansionCommandCenterPlacementCount;
        }
        _openingBuilderPlacementCount = 0;
        _openingMarinePlacementCount = 0;
        _secondBarracksPlacementCount = 0;
        _supplyDepotExpansionPlacementCount = 0;
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
        _buildJobs.Clear();
        _workerAssignments.Clear();
        _loggedWorkerAssignments.Clear();
        _openingTrace.Dispose();
        _binaryTrace.Dispose();
    }
}
