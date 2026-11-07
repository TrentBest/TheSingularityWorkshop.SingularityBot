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
public sealed class SingularityBot : SC2API_CSharp.Bot
{
    private readonly UnitBehaviorRuntime _unitBehaviors = new();
    private Sc2GameDataCatalog _gameData;
    private uint _playerId;
    private bool _behaviorCatalogRegistered;
    private bool _spatialDiagnosticsReported;

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

        Console.WriteLine($"[Runtime] SC2 game data: units={_gameData.UnitNames.Count}, buffs={_gameData.BuffNames.Count}, gather={_gameData.HarvestGatherAbilityId?.ToString() ?? "unavailable"}, return={_gameData.HarvestReturnAbilityId?.ToString() ?? "unavailable"}.");
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

        var intents = new BehaviorIntentBuffer();
        _unitBehaviors.Reconcile(normalized, intents.Add);
        _unitBehaviors.Update();

        return Sc2ActionTranslator.Translate(
            intents.Drain(),
            normalized,
            _gameData.HarvestGatherAbilityId,
            _gameData.HarvestReturnAbilityId);
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
    }
}
