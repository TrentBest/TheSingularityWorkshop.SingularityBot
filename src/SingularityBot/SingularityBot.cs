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

    public SingularityBot(bool passiveBaseline = false, bool writeDiagnostics = false)
    {
        _passiveBaseline = passiveBaseline;
        _writeDiagnostics = writeDiagnostics;
    }
    private readonly WorkerResourceAssignmentPlanner _workerAssignments = new();
    private Sc2GameDataCatalog _gameData;
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
    private bool _openingMarineActionSubmitted;
    private readonly Sc2OpeningSprayController _openingSpray = new();
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
            _openingTrace.Start(Sc2Diagnostics.OutputDirectory);
        _playerId = playerId;
        _gameData = Sc2GameDataCatalog.Create(data);

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
        var intents = new BehaviorIntentBuffer();
        _unitBehaviors.Reconcile(normalized, intents.Add);
        UpdateSpatialIntelligence(observation);
        if (SpatialMap is not null)
            GoalActionPlanner.Apply(_goalActionPlan, _unitBehaviors, SpatialMap);

        if (_writeDiagnostics)
            _baselineRecorder.Record(normalized);

        _openingBuilder.ObserveWorkers(normalized);

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

        var assignments = _workerAssignments.Reconcile(normalized);
        var actions = new List<SC2APIProtocol.Action>();
        SC2APIProtocol.Action sprayAction = null;
        SC2APIProtocol.Action builderAction = null;
        SC2APIProtocol.Action marineAction = null;
        RequestQuery sprayQuery = null;
        var allowWorkerActions = true;
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
            var productionAction = _openingProduction.TryCreateScvTrainingAction(normalized, _gameData);
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

            builderAction = _openingBuilder.TryCreateAction(
                normalized,
                SpatialMap,
                _gameData,
                _initialScvProductionObserved,
                _workerAssignments);
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
                marineAction = _openingMarine.TryCreateAction(normalized, SpatialMap, _gameData, _workerAssignments);
                _openingMarineActionSubmitted = marineAction is not null;
            }
            else
            {
                _openingMarineActionSubmitted = false;
            }

            var placementQuery = _openingBuilder.TakePlacementQuery(_gameData);
            var marinePlacementQuery = _openingBuilder.IsCompleted
                ? _openingMarine.TakePlacementQuery(_gameData)
                : null;
            if (sprayQuery is not null || placementQuery is not null || marinePlacementQuery is not null)
            {
                _pendingQuery = new RequestQuery();
                if (sprayQuery is not null)
                    _pendingQuery.Abilities.Add(sprayQuery.Abilities);
                if (placementQuery is not null)
                    _pendingQuery.Placements.Add(placementQuery.Placements);
                if (marinePlacementQuery is not null)
                    _pendingQuery.Placements.Add(marinePlacementQuery.Placements);
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

        if (allowWorkerBehaviorActions)
        {
            var workerIntents = intents.Drain()
                .Where(intent => !_workerAssignments.IsReserved(intent.Key))
                .ToArray();
            actions.AddRange(Sc2ActionTranslator.Translate(
                workerIntents,
                normalized,
                _gameData.SmartAbilityId,
                assignments));
        }

        if (_writeDiagnostics)
            _openingTrace.RecordFrame(
                normalized,
                _openingBuilder,
                builderAction,
                marineAction is not null,
                SpatialMap is not null);

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
                _openingBuilder.OnActionResult(null);
            if (_openingMarineActionSubmitted)
                _openingMarine.OnActionResult(null);
            _openingRallyActionSubmitted = false;
            _openingProductionActionSubmitted = false;
            _openingSprayActionSubmitted = false;
            _openingBuilderActionSubmitted = false;
            _openingMarineActionSubmitted = false;
            return;
        }

        for (var index = 0; index < response.Result.Count; index++)
        {
            var result = response.Result[index];
            var command = index < submittedActions.Count ? submittedActions[index].ActionRaw?.UnitCommand : null;
            var unitTags = command is null ? string.Empty : string.Join(",", command.UnitTags);
            Console.WriteLine($"[SC2 API] Action {index}: ability={command?.AbilityId.ToString() ?? "unknown"}, units=[{unitTags}], result={result}.");
            if (_writeDiagnostics)
                _openingTrace.RecordEvent("ActionResult", 0, $"index={index};ability={command?.AbilityId};units={unitTags};result={result}");
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
            _openingBuilder.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null);
            resultIndex++;
        }
        if (_openingMarineActionSubmitted)
        {
            _openingMarine.OnActionResult(resultIndex < response.Result.Count ? response.Result[resultIndex] : null);
            resultIndex++;
        }
        _openingRallyActionSubmitted = false;
        _openingProductionActionSubmitted = false;
        _openingSprayActionSubmitted = false;
        _openingBuilderActionSubmitted = false;
        _openingMarineActionSubmitted = false;
    }

    public RequestQuery TakeQueryRequest()
    {
        var query = _pendingQuery;
        _pendingQuery = null;
        return query;
    }

    public void OnQueryResult(ResponseQuery response)
    {
        if (_writeDiagnostics)
            _openingTrace.RecordEvent("PlacementQueryResult", 0,
                response is null
                    ? "response=null"
                    : $"placementCount={response.Placements.Count};results={string.Join("|", response.Placements.Select(item => item.Result))}");
        _openingBuilder.OnPlacementQueryResult(response);
        if (_openingBuilder.IsCompleted)
            _openingMarine.OnPlacementQueryResult(response);
        _openingSpray.OnQueryResult(response, _workerAssignments);
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
        _workerAssignments.Clear();
        _loggedWorkerAssignments.Clear();
        _openingTrace.Dispose();
    }
}
