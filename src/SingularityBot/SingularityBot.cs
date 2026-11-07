using System.Collections.Generic;
using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Spatial;

namespace SingularityBot;

/// <summary>
/// Protocol-facing bot shell. Strategic decisions belong to FSM behavior
/// definitions; this class owns the game-loop boundary.
/// </summary>
public sealed class SingularityBot : SC2API_CSharp.Bot
{
    private readonly UnitBehaviorRuntime _unitBehaviors = new();
    private bool _behaviorCatalogRegistered;

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
        if (SpatialMapFactory.TryCreate(gameInfo, observation, out var spatialMap))
        {
            SpatialMap = spatialMap;
            SpatialIntelligence = new SpatialIntelligence(spatialMap);
        }

        if (_behaviorCatalogRegistered)
            return;

        UnitBehaviorCatalog.Register();
        _behaviorCatalogRegistered = true;
    }

    public IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseObservation observation)
    {
        UpdateSpatialIntelligence(observation);

        // The FSM runtime receives exactly one processing pass per observation.
        _unitBehaviors.Update();

        // Intent-to-action translation remains a separate adapter step.
        // No unvalidated action is sent.
        return [];
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
    }

    public void OnEnd(ResponseObservation observation, Result result)
    {
        _unitBehaviors.RemoveAll();
    }
}
