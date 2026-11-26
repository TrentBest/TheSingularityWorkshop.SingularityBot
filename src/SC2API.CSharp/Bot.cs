using System;
using System.Collections.Generic;
using SC2APIProtocol;

namespace SC2API_CSharp
{
    public interface Bot
    {
        IEnumerable<SC2APIProtocol.Action> OnFrame(ResponseObservation observation);
        void OnActionResult(IReadOnlyList<SC2APIProtocol.Action> submittedActions, ResponseAction response);
        void OnEnd(ResponseObservation observation, Result result);
        void OnStart(ResponseGameInfo gameInfo, ResponseData data, ResponsePing pingResponse, ResponseObservation observation, uint playerId, String opponentId);
    }
}

namespace SC2API_CSharp
{
    /// <summary>Optional SC2 query round-trip supported by bots that need authoritative placement or ability queries.</summary>
    public interface QueryAwareBot
    {
        RequestQuery TakeQueryRequest();
        void OnQueryResult(ResponseQuery response);
    }
}
