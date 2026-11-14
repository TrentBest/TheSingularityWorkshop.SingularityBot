using SC2API_CSharp;
using SC2APIProtocol;
using Xunit;

namespace SingularityBot.Tests;

public sealed class Sc2PlayerResultResolverTests
{
    [Fact]
    public void ResolvesByPlayerId_WhenProtocolResultsAreNotInPlayerIdOrder()
    {
        var observation = new ResponseObservation();
        observation.PlayerResult.Add(new SC2APIProtocol.PlayerResult { PlayerId = 2, Result = Result.Defeat });
        observation.PlayerResult.Add(new SC2APIProtocol.PlayerResult { PlayerId = 1, Result = Result.Victory });

        Assert.Equal(Result.Victory, Sc2PlayerResultResolver.GetRequiredResult(observation, 1));
        Assert.Equal(Result.Defeat, Sc2PlayerResultResolver.GetRequiredResult(observation, 2));
    }

    [Fact]
    public void RejectsMissingPlayerResult()
    {
        var observation = new ResponseObservation();
        observation.PlayerResult.Add(new SC2APIProtocol.PlayerResult { PlayerId = 2, Result = Result.Victory });

        Assert.Throws<InvalidOperationException>(() =>
            Sc2PlayerResultResolver.GetRequiredResult(observation, 1));
    }

    [Fact]
    public void RejectsDuplicatePlayerResult()
    {
        var observation = new ResponseObservation();
        observation.PlayerResult.Add(new SC2APIProtocol.PlayerResult { PlayerId = 1, Result = Result.Victory });
        observation.PlayerResult.Add(new SC2APIProtocol.PlayerResult { PlayerId = 1, Result = Result.Defeat });

        Assert.Throws<InvalidOperationException>(() =>
            Sc2PlayerResultResolver.GetRequiredResult(observation, 1));
    }

    [Fact]
    public void RejectsZeroParticipantPlayerId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Sc2PlayerResultResolver.GetRequiredResult(new ResponseObservation(), 0));
    }
}
