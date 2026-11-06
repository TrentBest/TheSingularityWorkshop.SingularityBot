using System.Collections.Generic;
using SC2APIProtocol;

namespace SingularityBot;

/// <summary>
/// Minimal protocol-facing bot shell. Decision-making belongs in the FSM runtime,
/// not in the SC2 transport adapter.
/// </summary>
public sealed class SingularityBot : SC2API_CSharp.Bot
{
    public void OnStart(
        ResponseGameInfo gameInfo,
        ResponseData data,
        ResponsePing pingResponse,
        ResponseObservation observation,
        uint playerId,
        string opponentId)
    {
        // Match and entity FSMs are initialized in the next implementation milestone.
    }

    public IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseObservation observation)
    {
        // Intentionally no-op until the action validation pipeline is implemented.
        return [];
    }

    public void OnEnd(ResponseObservation observation, Result result)
    {
        // Match teardown will be owned by the FSM runtime.
    }
}
