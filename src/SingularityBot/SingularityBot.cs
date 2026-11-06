using System.Collections.Generic;
using SC2APIProtocol;
using SingularityBot.Behavior;
using SingularityBot.Observation;

namespace SingularityBot;

/// <summary>
/// Protocol-facing bot shell. Strategic decisions belong to FSM behavior
/// definitions; this class owns the game-loop boundary.
/// </summary>
public sealed class SingularityBot : SC2API_CSharp.Bot
{
    private readonly UnitBehaviorRuntime _unitBehaviors = new();
    private readonly Dictionary<uint, string> _unitNames = new();
    private readonly Dictionary<uint, string> _buffNames = new();
    private uint _playerId;
    private bool _behaviorCatalogRegistered;

    public void OnStart(
        ResponseGameInfo gameInfo,
        ResponseData data,
        ResponsePing pingResponse,
        ResponseObservation observation,
        uint playerId,
        string opponentId)
    {
        _playerId = playerId;
        _unitNames.Clear();
        _buffNames.Clear();

        if (data is not null)
        {
            foreach (var unitType in data.Units.OrderBy(unit => unit.UnitId))
                _unitNames[unitType.UnitId] = unitType.Name;
            foreach (var buff in data.Buffs.OrderBy(buff => buff.BuffId))
                _buffNames[buff.BuffId] = buff.Name;
        }

        if (_behaviorCatalogRegistered)
            return;

        UnitBehaviorCatalog.Register();
        _behaviorCatalogRegistered = true;
    }

    public IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseObservation observation)
    {
        var normalized = Sc2ObservationNormalizer.Normalize(observation, _playerId, _unitNames, _buffNames);

        // Issue #1 captures intents only at the domain callback boundary. They
        // are deliberately not translated or submitted as SC2 actions yet.
        _unitBehaviors.Reconcile(normalized, static (_, _) => { });
        _unitBehaviors.Update();

        return [];
    }

    public void OnEnd(ResponseObservation observation, Result result)
    {
        _unitBehaviors.RemoveAll();
    }
}
