using System.Collections.Generic;
using SC2APIProtocol;
using SingularityBot.Behavior;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot;

/// <summary>
/// Protocol-facing bot shell. Strategic decisions belong to FSM behavior
/// definitions; this class owns the game-loop boundary.
/// </summary>
public sealed class SingularityBot : SC2API_CSharp.Bot
{
    private bool _behaviorCatalogRegistered;

    public void OnStart(
        ResponseGameInfo gameInfo,
        ResponseData data,
        ResponsePing pingResponse,
        ResponseObservation observation,
        uint playerId,
        string opponentId)
    {
        if (!_behaviorCatalogRegistered)
        {
            UnitBehaviorCatalog.Register();
            _behaviorCatalogRegistered = true;
        }
    }

    public IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseObservation observation)
    {
        // One deterministic FSM processing pass per SC2 observation.
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProcessingGroup);

        // The observation-to-context and intent-to-action adapters are the
        // next integration step. Until then, no unvalidated actions are sent.
        return [];
    }

    public void OnEnd(ResponseObservation observation, Result result)
    {
        // Unit contexts will be invalidated and detached during match teardown.
    }
}
