using SC2APIProtocol;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotCompositionMatrixTests
{
    private static readonly Race[] Races = { Race.Terran, Race.Protoss, Race.Zerg };

    private static BotCompositionMatrix CreateValidMatrix() => new()
    {
        Matchups = Races.SelectMany(own => Races.Select(opponent =>
            new BotMatchupComposition
            {
                OwnRace = own,
                OpponentRace = opponent,
                Hero = new BotHeroSelection
                {
                    ImplementationId = $"hero.{own.ToString().ToLowerInvariant()}.v1",
                    PresentationProfileId = $"profile.{own.ToString().ToLowerInvariant()}"
                },
                Layers = new CommandLayerComposition()
            })).ToArray()
    };

    [Fact]
    public void Matrix_RequiresExactlyNineUniqueConcreteMatchups()
    {
        var matrix = CreateValidMatrix();

        Assert.Empty(matrix.Validate());

        var duplicate = matrix.Matchups.ToArray();
        duplicate[8] = duplicate[0];
        var invalid = new BotCompositionMatrix { Matchups = duplicate };

        Assert.Contains(invalid.Validate(), error => error.Contains("duplicate matchup"));
        Assert.Contains(invalid.Validate(), error => error.Contains("missing required matchup"));
    }

    [Fact]
    public void Matrix_RejectsMissingMatchupsAndUnsupportedRaceKeys()
    {
        var matrix = CreateValidMatrix();
        var incomplete = new BotCompositionMatrix { Matchups = matrix.Matchups.Take(8).ToArray() };

        Assert.Contains(incomplete.Validate(), error => error.Contains("exactly nine"));
        Assert.Contains(incomplete.Validate(), error => error.Contains("missing required matchup"));

        var invalid = matrix.Matchups.ToArray();
        invalid[0] = invalid[0] with { OwnRace = Race.Random };
        var invalidMatrix = new BotCompositionMatrix { Matchups = invalid };

        Assert.Contains(invalidMatrix.Validate(), error => error.Contains("Random and Unknown"));
    }

    [Fact]
    public void Matrix_ResolvesExactMatchupWithoutCrossMatchupFallback()
    {
        var matrix = CreateValidMatrix();

        Assert.True(matrix.TryResolve(Race.Zerg, Race.Zerg, out var zergMirror));
        Assert.Equal(Race.Zerg, zergMirror.OwnRace);
        Assert.Equal(Race.Zerg, zergMirror.OpponentRace);

        Assert.False(matrix.TryResolve(Race.Zerg, Race.Random, out _));
        Assert.False(matrix.TryResolve(Race.Unknown, Race.Terran, out _));
    }

    [Fact]
    public void Matrix_ValidatesHeroIdentityAndSubordinateLayers()
    {
        var matrix = CreateValidMatrix();
        var matchups = matrix.Matchups.ToArray();
        matchups[0] = matchups[0] with
        {
            Hero = new BotHeroSelection { ImplementationId = "", PresentationProfileId = "" },
            Layers = null
        };
        var invalid = new BotCompositionMatrix { Matchups = matchups };

        Assert.Contains(invalid.Validate(), error => error.Contains("$.hero.implementationId"));
        Assert.Contains(invalid.Validate(), error => error.Contains("$.hero.presentationProfileId"));
        Assert.Contains(invalid.Validate(), error => error.Contains("$.layers: is required"));
    }

    [Fact]
    public void Matrix_KeepsHeroImplementationSeparateFromPresentationProfile()
    {
        var matrix = CreateValidMatrix();

        Assert.True(matrix.TryResolve(Race.Zerg, Race.Zerg, out var matchup));
        Assert.NotEqual(matchup.Hero.ImplementationId, matchup.Hero.PresentationProfileId);
    }
}
