using System.Collections.Generic;
using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot;

/// <summary>
/// Protocol-facing bot shell. Strategic decisions belong to FSM behavior
/// definitions; this class owns the game-loop boundary.
/// </summary>
public sealed class SingularityBot : SC2API_CSharp.Bot, SC2API_CSharp.QueryAwareBot
{
    private readonly UnitBehaviorRuntime _unitBehaviors = new();
    private readonly WorkerResourceAssignmentPlanner _workerAssignments = new();
    private Sc2GameDataCatalog _gameData;
    private uint _playerId;
    private bool _behaviorCatalogRegistered;
    private bool _spatialDiagnosticsReported;
    private readonly Sc2OpeningProductionController _openingProduction = new();
    private bool _openingProductionActionSubmitted;
    private bool _scvTrainingOrderObserved;
    private bool _initialScvProductionObserved;
    private bool _openingBuilderActionSubmitted;
    private RequestQuery _pendingQuery;
    private readonly Sc2OpeningBuilderController _openingBuilder = new();
    private readonly HashSet<(ulong WorkerTag, uint Generation)> _loggedWorkerAssignments = new();

    /// <summary>Immutable map geometry cached for the lifetime of this match.</summary>
    public SpatialMap SpatialMap { get; private set; }
    public SpatialIntelligence SpatialIntelligence { get; private set; }

    public void OnStart(
        ResponseGameInfo gameInfo,
        ResponseData data,
        ResponsePing pingResponse,
        ResponseObservation observation,
        uint playerId,
        string opponentId)
    {
        _playerId = playerId;
        _gameData = Sc2GameDataCatalog.Create(data);

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
            Console.WriteLine($"[Spatial] Map geometry unresolved for {gameInfo?.MapName ?? "unknown map"}; the bot needs playable bounds, own starting units, and exactly two start locations.");
        }

        if (_behaviorCatalogRegistered)
            return;

        UnitBehaviorCatalog.Register();
        _behaviorCatalogRegistered = true;

        Console.WriteLine($"[Runtime] SC2 game data: units={_gameData.UnitNames.Count}, buffs={_gameData.BuffNames.Count}, gather={_gameData.HarvestGatherAbilityId?.ToString() ?? "unavailable"}, return={_gameData.HarvestReturnAbilityId?.ToString() ?? "unavailable"}, train-SCV={_gameData.ScvTrainAbilityId?.ToString() ?? "unavailable"} (minerals={_gameData.ScvMineralCost}, supply={_gameData.ScvFoodRequired}).");
    }

    public IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseObservation observation)
    {
        UpdateSpatialIntelligence(observation);

        // Normalize once and use the same authoritative frame for reconciliation,
        // FSM evaluation, target selection, and action translation.
        var normalized = Sc2ObservationNormalizer.Normalize(
            observation,
            _playerId,
            _gameData.UnitNames,
            _gameData.BuffNames);

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

        // The opening's first protocol action is isolated: train one SCV before
        // issuing worker-route commands. The next observation must show the order.
        var productionAction = _openingProduction.TryCreateScvTrainingAction(normalized, _gameData);
        _openingProductionActionSubmitted = productionAction is not null;
        if (productionAction is not null)
        {
            var command = productionAction.ActionRaw.UnitCommand;
            Console.WriteLine($"[Opening] Requesting first SCV: Command Center={command.UnitTags[0]}, ability={command.AbilityId}, minerals={normalized.Minerals}, supply={normalized.FoodUsed}/{normalized.FoodCap}.");
            return new[] { productionAction };
        }

        var builderAction = _openingBuilder.TryCreateAction(
            normalized,
            SpatialMap,
            _gameData,
            _initialScvProductionObserved,
            _workerAssignments);
        _openingBuilderActionSubmitted = builderAction is not null;
        if (builderAction is not null)
        {
            var command = builderAction.ActionRaw.UnitCommand;
            Console.WriteLine($"[Opening] Builder SCV {_openingBuilder.BuilderTag}: ability={command.AbilityId}, target=({command.TargetWorldSpacePos.X:F1},{command.TargetWorldSpacePos.Y:F1}).");
        }

        // Refresh after a builder reservation so the same frame cannot emit a stale gather intent.
        assignments = _workerAssignments.Reconcile(normalized);
        foreach (var assignment in assignments.Values.OrderBy(item => item.WorkerTag))
        {
            if (_loggedWorkerAssignments.Add((assignment.WorkerTag, assignment.Generation)))
                Console.WriteLine($"[Economy] SCV {assignment.WorkerTag} -> mineral {assignment.MineralTag}; town hall {assignment.TownHallTag}; generation {assignment.Generation}.");
        }

        _pendingQuery = _openingBuilder.TakePlacementQuery(_gameData);

        var intents = new BehaviorIntentBuffer();
        _unitBehaviors.Reconcile(normalized, intents.Add);
        _unitBehaviors.ApplyWorkerAssignments(assignments, normalized.GameLoop);
        _unitBehaviors.Update();

        var actions = new List<SC2APIProtocol.Action>();
        if (builderAction is not null)
            actions.Add(builderAction);
        var workerIntents = intents.Drain()
            .Where(intent => !_workerAssignments.IsReserved(intent.Key))
            .ToArray();
        actions.AddRange(Sc2ActionTranslator.Translate(
            workerIntents,
            normalized,
            _gameData.HarvestGatherAbilityId,
            _gameData.HarvestReturnAbilityId,
            assignments));
        return actions;
    }

    public void OnActionResult(IReadOnlyList<SC2APIProtocol.Action> submittedActions, ResponseAction response)
    {
        if (response is null) return;
        if (response.Result.Count == 0)
        {
            Console.WriteLine("[SC2 API] Action request returned no per-action result.");
            if (_openingProductionActionSubmitted)
                _openingProduction.OnActionResult(null);
            if (_openingBuilderActionSubmitted)
                _openingBuilder.OnActionResult(null);
            _openingProductionActionSubmitted = false;
            _openingBuilderActionSubmitted = false;
            return;
        }

        for (var index = 0; index < response.Result.Count; index++)
        {
            var result = response.Result[index];
            var command = index < submittedActions.Count ? submittedActions[index].ActionRaw?.UnitCommand : null;
            var unitTags = command is null ? string.Empty : string.Join(",", command.UnitTags);
            Console.WriteLine($"[SC2 API] Action {index}: ability={command?.AbilityId.ToString() ?? "unknown"}, units=[{unitTags}], result={result}.");
        }
        if (_openingProductionActionSubmitted)
            _openingProduction.OnActionResult(response.Result[0]);
        if (_openingBuilderActionSubmitted)
            _openingBuilder.OnActionResult(response.Result[0]);
        _openingProductionActionSubmitted = false;
        _openingBuilderActionSubmitted = false;
    }

    public RequestQuery TakeQueryRequest()
    {
        var query = _pendingQuery;
        _pendingQuery = null;
        return query;
    }

    public void OnQueryResult(ResponseQuery response)
    {
        _openingBuilder.OnPlacementQueryResult(response);
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
        _unitBehaviors.RemoveAll();
        _workerAssignments.Clear();
        _loggedWorkerAssignments.Clear();
    }
}
