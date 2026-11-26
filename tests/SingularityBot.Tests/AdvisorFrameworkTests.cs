using SingularityBot.Behavior;
using SingularityBot.Observation;
using Xunit;

namespace SingularityBot.Tests;

public sealed class AdvisorFrameworkTests
{
    [Fact]
    public void GameStateUsesObservedTechAndBasesRatherThanElapsedTime()
    {
        var state = new GameStateContext();
        state.Apply(new NormalizedObservation(9000, 1, new[]
        {
            Unit(1, "CommandCenter"),
            Unit(2, "OrbitalCommand"),
            Unit(3, "Factory")
        }, new HashSet<ulong>()));

        Assert.Equal(2, state.FriendlyBaseCount);
        Assert.True(state.HasAdvancedTech);
        Assert.False(state.HasLateGameTech);
    }

    [Fact]
    public void AdvisorComputeAllocationChangesAcrossGamePhases()
    {
        var early = AdvisorWorkScheduler.GetWeights(GamePhase.Early);
        var mid = AdvisorWorkScheduler.GetWeights(GamePhase.Mid);
        var late = AdvisorWorkScheduler.GetWeights(GamePhase.Late);

        Assert.True(early[AdvisorDomain.Economy] > late[AdvisorDomain.Economy]);
        Assert.True(early[AdvisorDomain.Intelligence] > mid[AdvisorDomain.Intelligence]);
        Assert.True(late[AdvisorDomain.Military] > early[AdvisorDomain.Military]);
        Assert.True(late[AdvisorDomain.Science] > early[AdvisorDomain.Science]);
        Assert.Equal(early.Values.Sum(), AdvisorWorkScheduler.GetFrameSchedule(GamePhase.Early).Count);
    }

    [Fact]
    public void AdvisorScheduleContainsEveryDomain()
    {
        foreach (var phase in Enum.GetValues<GamePhase>())
        {
            var schedule = AdvisorWorkScheduler.GetFrameSchedule(phase);
            Assert.Contains(AdvisorWorkScheduler.EconomyGroup, schedule);
            Assert.Contains(AdvisorWorkScheduler.MilitaryGroup, schedule);
            Assert.Contains(AdvisorWorkScheduler.ScienceGroup, schedule);
            Assert.Contains(AdvisorWorkScheduler.BaseEngineeringGroup, schedule);
            Assert.Contains(AdvisorWorkScheduler.IntelligenceGroup, schedule);
        }
    }

    private static UnitObservationFacts Unit(ulong tag, string kind) =>
        new(tag, 1, kind, 1, UnitAlliance.Self, UnitVisibility.Visible,
            100, 100, 0, 0, 1, 0, 0, true, false, false, false, false, false, 9000);
}
