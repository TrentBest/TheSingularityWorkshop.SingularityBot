using System.Collections.Generic;
using SC2APIProtocol;
using SingularityBot.Behavior;

namespace SingularityBot;

/// <summary>
/// Protocol-facing bot shell. Strategic decisions belong to FSM behavior
/// definitions; this class owns the game-loop boundary.
/// </summary>
public sealed class SingularityBot : SC2API_CSharp.Bot
{
    private readonly UnitBehaviorRuntime _unitBehaviors = new();
    private bool _behaviorCatalogRegistered;

    public void OnStart(
        ResponseGameInfo gameInfo,
        ResponseData data,
        ResponsePing pingResponse,
        ResponseObservation observation,
        uint playerId,
        string opponentId)
    {
        if (_behaviorCatalogRegistered)
            return;

        UnitBehaviorCatalog.Register();
        _behaviorCatalogRegistered = true;
    }

    public IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseObservation observation)
    {
        // The FSM runtime receives exactly one processing pass per observation.
        _unitBehaviors.Update();

        // Observation-to-context reconciliation and intent-to-action translation
        // are deliberately separate adapter steps. No unvalidated action is sent.
        return [];
    }

    public void OnEnd(ResponseObservation observation, Result result)
    {
        _unitBehaviors.RemoveAll();
    }
}
