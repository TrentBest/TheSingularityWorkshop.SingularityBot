using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class CommandAuthorityTests
{
    [Fact]
    public void UnitBehaviorContext_IsTheUnitController()
    {
        IController controller = new UnitBehaviorContext(1, "SCV 1", "SCV", true, _ => { });

        Assert.Equal(1UL, controller.UnitTag);
        Assert.Equal("SCV", controller.UnitKind);
        Assert.True(controller.IsWorker);
        Assert.True(controller.IsValid);
        Assert.Empty(controller.Capabilities);
    }

    [Theory]
    [InlineData("Zerg", "The Overmind", "Cerebrates")]
    [InlineData("Terran", "Fleet Admiral", "Command Staff")]
    [InlineData("Protoss", "The Enclave", "Conclaves")]
    public void RacePresentation_ProvidesDistinctStrategicAndOperationalVocabulary(
        string race, string strategyTitle, string coordinatorTitle)
    {
        Assert.True(RaceCommandPresentationCatalog.TryGet(race, out var profile));
        Assert.Equal(strategyTitle, profile.StrategyTitle);
        Assert.Equal(coordinatorTitle, profile.CoordinatorTitle);
        Assert.NotEmpty(profile.CoordinatorExamples);
    }
}
