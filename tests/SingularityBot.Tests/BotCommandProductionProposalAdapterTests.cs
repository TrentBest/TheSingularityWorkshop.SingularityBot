using SC2APIProtocol;
using SingularityBot.Behavior;
using TheSingularityWorkshop.CommandAi;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotCommandProductionProposalAdapterTests
{
    [Fact]
    public void AuthorizedMarineProposalUpdatesExistingObjectiveAndIsIdempotent()
    {
        var definition = BotDefinitionCatalog.CreateDefault();
        var emitted = new List<ProductionIntent>();
        using var runtime = new ProductionPlanRuntime(emitted.Add);
        Assert.Empty(runtime.AddPlan(definition.Opening));
        Assert.True(runtime.TryGetStep("marine-core", out var marine));
        var adapter = new BotCommandProductionProposalAdapter();
        var proposal = CreateMarineProposal("5", "50");
        var gameData = CreateMarineGameData();

        Assert.True(adapter.TryApply(
            proposal, "commandai:proposal:1", true, definition, Race.Terran,
            gameData, runtime, out var error), error);
        Assert.Equal(5, marine.EffectiveDesiredCount);
        Assert.Equal(50, marine.EffectivePriority);

        // A delivery retry with the same key and same content is an acknowledged no-op.
        Assert.True(adapter.TryApply(
            proposal, "commandai:proposal:1", true, definition, Race.Terran,
            gameData, runtime, out error), error);
        Assert.Equal(5, marine.EffectiveDesiredCount);
        Assert.Equal(50, marine.EffectivePriority);
    }

    [Fact]
    public void UnauthorizedProposalDoesNotChangeProductionObjective()
    {
        var definition = BotDefinitionCatalog.CreateDefault();
        using var runtime = new ProductionPlanRuntime(_ => { });
        Assert.Empty(runtime.AddPlan(definition.Opening));
        Assert.True(runtime.TryGetStep("marine-core", out var marine));
        var adapter = new BotCommandProductionProposalAdapter();

        Assert.False(adapter.TryApply(
            CreateMarineProposal("5", "50"), "commandai:proposal:2", false,
            definition, Race.Terran, CreateMarineGameData(), runtime, out var error));
        Assert.Contains("not authorized", error);
        Assert.Equal(60, marine.EffectiveDesiredCount);
        Assert.Equal(80, marine.EffectivePriority);
    }

    [Fact]
    public void ReusedIdempotencyKeyCannotApplyDifferentProposal()
    {
        var definition = BotDefinitionCatalog.CreateDefault();
        using var runtime = new ProductionPlanRuntime(_ => { });
        Assert.Empty(runtime.AddPlan(definition.Opening));
        Assert.True(runtime.TryGetStep("marine-core", out var marine));
        var adapter = new BotCommandProductionProposalAdapter();
        var gameData = CreateMarineGameData();

        Assert.True(adapter.TryApply(
            CreateMarineProposal("5", "50"), "commandai:proposal:3", true,
            definition, Race.Terran, gameData, runtime, out var error), error);
        Assert.False(adapter.TryApply(
            CreateMarineProposal("6", "50"), "commandai:proposal:3", true,
            definition, Race.Terran, gameData, runtime, out error));
        Assert.Contains("different proposal", error);
        Assert.Equal(5, marine.EffectiveDesiredCount);
    }

    [Fact]
    public void ProposalFailsClosedWhenMatchRaceDoesNotMatchBotDefinition()
    {
        var definition = BotDefinitionCatalog.CreateDefault();
        using var runtime = new ProductionPlanRuntime(_ => { });
        Assert.Empty(runtime.AddPlan(definition.Opening));
        Assert.True(runtime.TryGetStep("marine-core", out var marine));
        var adapter = new BotCommandProductionProposalAdapter();

        Assert.False(adapter.TryApply(
            CreateMarineProposal("5", "50"), "commandai:proposal:4", true,
            definition, Race.Zerg, CreateMarineGameData(), runtime, out var error));
        Assert.Contains("does not match", error);
        Assert.Equal(60, marine.EffectiveDesiredCount);
    }

    private static CommandProposal CreateMarineProposal(string desiredCount, string priority) =>
        new CommandProposalBuilder()
            .AddCommand(BotCommandVocabulary.Actions.SetProductionTarget, command => command
                .WithIdentifier(BotCommandVocabulary.Parameters.ProductionTarget, 1)
                .WithText(BotCommandVocabulary.Parameters.ProductionDesiredCount, desiredCount)
                .WithText(BotCommandVocabulary.Parameters.ProductionPriority, priority))
            .Build();

    private static Sc2GameDataCatalog CreateMarineGameData()
    {
        var data = new ResponseData();
        data.Units.Add(new UnitTypeData
        {
            UnitId = 21, Name = "Barracks", AbilityId = 321, MineralCost = 150
        });
        data.Units.Add(new UnitTypeData
        {
            UnitId = 48, Name = "Marine", AbilityId = 560, MineralCost = 50, FoodRequired = 1
        });
        return Sc2GameDataCatalog.Create(data);
    }
}
