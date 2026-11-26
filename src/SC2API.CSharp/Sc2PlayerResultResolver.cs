using SC2APIProtocol;

namespace SC2API_CSharp;

/// <summary>
/// Correlates terminal player results by the protocol's explicit player_id,
/// never by repeated-field position.
/// </summary>
public static class Sc2PlayerResultResolver
{
    public static Result GetRequiredResult(ResponseObservation observation, uint playerId)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (playerId == 0)
            throw new ArgumentOutOfRangeException(nameof(playerId), "SC2 participant player IDs must be non-zero.");

        PlayerResult match = null;
        foreach (var result in observation.PlayerResult)
        {
            if (result.PlayerId != playerId)
                continue;

            if (match is not null)
                throw new InvalidOperationException($"SC2 returned duplicate terminal results for player {playerId}.");

            match = result;
        }

        if (match is null)
            throw new InvalidOperationException($"SC2 did not return a terminal result for player {playerId}.");

        return match.Result;
    }
}
