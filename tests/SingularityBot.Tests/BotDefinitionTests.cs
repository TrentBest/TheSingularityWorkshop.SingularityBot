using System.Text.Json;
using SingularityBot.Behavior;
using Xunit;

namespace SingularityBot.Tests;

public sealed class BotDefinitionTests
{
    [Fact]
    public void ValidDefinition_PassesValidationAndRoundTrips()
    {
        var definition = CreateValidDefinition();

        Assert.Empty(definition.Validate());

        var parsed = BotDefinition.Parse(definition.Serialize());

        Assert.Equal(definition.Id, parsed.Id);
        Assert.Equal(definition.Opening.Id, parsed.Opening.Id);
        Assert.Equal(1, parsed.Ai.MaxOnStartCalls);
        Assert.Equal(3, parsed.Ai.MaxInMatchCalls);
    }

    [Fact]
    public void CompiledDefaultDefinition_IsValidAndSelectedWithoutExternalInput()
    {
        var definition = BotDefinitionCatalog.CreateDefault();

        Assert.Empty(definition.Validate());
        Assert.Equal(BotDefinitionCatalog.DefaultDefinitionId, definition.Id);
        Assert.Equal(60, definition.Opening.Steps
            .Where(step => step.Target.Equals("Marine", StringComparison.OrdinalIgnoreCase))
            .Max(step => step.DesiredCount));
        Assert.Equal(GoalActionRole.Engage, definition.DefaultGoals.DefaultRole);
        Assert.Equal(0, definition.DefaultGoals.ScoutCount);

        var bot = new global::SingularityBot.SingularityBot();
        Assert.Equal(definition.Id, bot.Definition.Id);
        Assert.NotNull(bot.Manifest);
        Assert.NotNull(bot.OpeningPlan);
        Assert.Equal(2, bot.ComposedRuntime.Bundles.Count);
        Assert.Equal(BotCommandVocabulary.Actions.SetProductionTarget,
            bot.AvailableActions.Single(action => action.Name == "SetProductionTarget").Id);
        Assert.True(bot.ComposedRuntime.TryGetBundle<BotDefinitionMicroBundle>(
            BotDefinitionMicroBundle.BundleId, out var composedDefinition));
        Assert.True(composedDefinition!.IsLoaded);
    }

    [Fact]
    public void AiPolicy_RejectsMoreThanOneStartupOrThreeMatchCalls()
    {
        var definition = CreateValidDefinition();
        var tooManyStartupCalls = Copy(definition, new BotAiPolicy
        {
            Enabled = true, Provider = "test", Model = "test",
            MaxOnStartCalls = 2, MaxInMatchCalls = 3,
            AllowedTriggers = new[] { "enemy.tech.unanswered" }
        });
        var tooManyMatchCalls = Copy(definition, new BotAiPolicy
        {
            Enabled = true, Provider = "test", Model = "test",
            MaxOnStartCalls = 1, MaxInMatchCalls = 4,
            AllowedTriggers = new[] { "enemy.tech.unanswered" }
        });

        Assert.Contains(tooManyStartupCalls.Validate(), error => error.Contains("maxOnStartCalls"));
        Assert.Contains(tooManyMatchCalls.Validate(), error => error.Contains("maxInMatchCalls"));
    }

    [Fact]
    public void RuntimeIdentity_MustMatchTopLevelIdentity()
    {
        var definition = CreateValidDefinition();
        var mismatched = new BotDefinition
        {
            SchemaVersion = definition.SchemaVersion,
            Id = definition.Id,
            Name = definition.Name,
            Race = definition.Race,
            StrategyProfile = "different.profile",
            Runtime = definition.Runtime,
            Opening = definition.Opening,
            DefaultGoals = definition.DefaultGoals,
            StrategyTransitions = definition.StrategyTransitions,
            Ai = definition.Ai
        };

        Assert.Contains(mismatched.Validate(), error => error.Contains("must match $.strategyProfile"));
    }

    [Fact]
    public void Parse_RejectsUnknownProperties()
    {
        var json = definitionJson();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var invalid = json.TrimEnd('}') + ", \"unexpected\": true }";

        Assert.Throws<JsonException>(() => BotDefinition.Parse(invalid));

        string definitionJson() => CreateValidDefinition().Serialize();
    }

    [Fact]
    public void SingularityBot_UsesSelectedDefinitionAndItsGoalPlan()
    {
        var definition = CreateValidDefinition();
        var bot = new global::SingularityBot.SingularityBot(botDefinition: definition);

        Assert.Same(definition, bot.Definition);
        Assert.Same(definition.Runtime, bot.Manifest);
        Assert.Same(definition.Opening, bot.OpeningPlan);
        Assert.Same(definition.DefaultGoals, bot.GoalActionPlan);
        Assert.Equal(60, bot.DesiredMarineCount);
    }

    [Fact]
    public void SingularityBot_RejectsInvalidDefinitionBeforeMatchStartup()
    {
        var invalid = new BotDefinition { Id = "invalid" };

        Assert.Throws<ArgumentException>(() =>
            new global::SingularityBot.SingularityBot(botDefinition: invalid));
    }

    private static BotDefinition Copy(BotDefinition source, BotAiPolicy ai) => new()
    {
        SchemaVersion = source.SchemaVersion,
        Id = source.Id,
        Name = source.Name,
        Race = source.Race,
        StrategyProfile = source.StrategyProfile,
        Runtime = source.Runtime,
        Opening = source.Opening,
        DefaultGoals = source.DefaultGoals,
        StrategyTransitions = source.StrategyTransitions,
        Ai = ai
    };

    private static BotDefinition CreateValidDefinition()
    {
        using var settingsDocument = JsonDocument.Parse("{}");
        return new BotDefinition
        {
            Id = "test.bot",
            Name = "Test Bot",
            Race = "Terran",
            StrategyProfile = "terran.test.v1",
            Runtime = new BotManifest
            {
                SchemaVersion = 1,
                Bot = new BotManifestBotSettings
                {
                    Race = "Terran",
                    StrategyProfile = "terran.test.v1",
                    DeterministicScheduling = true
                },
                Advisors = new[]
                {
                    new AdvisorManifestNode
                    {
                        Id = "economy",
                        Definition = "advisor.economy.v1",
                        Enabled = true,
                        ProcessGroup = "Test.Economy",
                        Priority = 100,
                        Settings = settingsDocument.RootElement.Clone()
                    }
                }
            },
            Opening = new ProductionPlanDefinition
            {
                SchemaVersion = "0.1",
                Id = "test.opening",
                Name = "Test opening",
                Race = "Terran",
                Steps = new[]
                {
                    new ProductionStepDefinition("depot", "SupplyDepot", "SCV", 1, 0, 100, Lane: ProductionLane.Buildings)
                }
            },
            DefaultGoals = new GoalActionPlanDefinition(),
            Ai = new BotAiPolicy
            {
                Enabled = true,
                Provider = "test",
                Model = "test-model",
                MaxOnStartCalls = 1,
                MaxInMatchCalls = 3,
                AllowedTriggers = new[] { "enemy.tech.unanswered" }
            }
        };
    }
}
